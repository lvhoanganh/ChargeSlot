using ChargeSlot.Api.Enums;
using ChargeSlot.Api.Models;
using ChargeSlot.Api.Repositories.Interfaces;
using ChargeSlot.Api.Services.Interfaces;

using ChargeSlot.Api.Helpers;
namespace ChargeSlot.Api.BackgroundJobs
{
    /// <summary>
    /// Auto-confirm invoice sau 24h nếu Driver không xác nhận hoặc dispute.
    /// Chạy mỗi 5 phút.
    /// Flow: Invoice PendingConfirm > 24h → auto-confirm → settle payment → booking = Completed.
    /// </summary>
    public class InvoiceAutoConfirmJob : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<InvoiceAutoConfirmJob> _logger;



        public InvoiceAutoConfirmJob(IServiceProvider serviceProvider, ILogger<InvoiceAutoConfirmJob> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    List<int> expiredInvoiceIds;

                    // 1. Chỉ lấy danh sách ID
                    using (var outerScope = _serviceProvider.CreateScope())
                    {
                        var configService = outerScope.ServiceProvider.GetRequiredService<ISystemConfigService>();
                        var autoConfirmHours = await configService.GetIntAsync(Constants.SystemConfigKeys.Invoice_AutoConfirm_Hours, 24);
                        var deadline = DateTimeHelper.VietnamNow() - TimeSpan.FromHours(autoConfirmHours);
                        var invoiceRepo = outerScope.ServiceProvider.GetRequiredService<IInvoiceRepository>();
                        expiredInvoiceIds = await invoiceRepo.GetExpiredPendingConfirmIdsAsync(deadline);
                    }

                    // 2. Xử lý từng ID trong 1 scope biệt lập
                    foreach (var invoiceId in expiredInvoiceIds)
                    {
                        using var innerScope = _serviceProvider.CreateScope();
                        var invoiceRepo = innerScope.ServiceProvider.GetRequiredService<IInvoiceRepository>();
                        var unitOfWork = innerScope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                        var walletRepo = innerScope.ServiceProvider.GetRequiredService<IWalletRepository>();
                        var ledgerRepo = innerScope.ServiceProvider.GetRequiredService<ILedgerTransactionRepository>();
                        var loyaltyTxRepo = innerScope.ServiceProvider.GetRequiredService<ILoyaltyTransactionRepository>();
                        var notificationService = innerScope.ServiceProvider.GetRequiredService<INotificationService>();

                        using var transaction = await unitOfWork.BeginTransactionAsync();
                        try
                        {
                            var invoice = await invoiceRepo.GetByIdWithFullBookingDetailsAsync(invoiceId);

                            if (invoice == null || invoice.Status != InvoiceStatus.PendingConfirm)
                                continue;

                            var booking = invoice.Booking;

                            // Auto-confirm invoice
                            invoice.Status = InvoiceStatus.Confirmed;
                            invoice.UpdatedAt = DateTimeHelper.VietnamNow();

                            // Complete booking
                            booking.Status = BookingStatus.Completed;
                            booking.UpdatedAt = DateTimeHelper.VietnamNow();

                            await unitOfWork.CompleteAsync();

                            // Loyalty Points (dùng snapshot từ lúc tạo booking)
                            var earnRate = booking.LoyaltyEarnRateSnapshot == 0 ? 0.05m : booking.LoyaltyEarnRateSnapshot;
                            var pointsEarned = Math.Floor(booking.TotalAmount * earnRate);
                            if (pointsEarned > 0 && booking.Driver != null)
                            {
                                booking.Driver.LoyaltyPoints += pointsEarned;
                                booking.PointsEarned = pointsEarned;
                                loyaltyTxRepo.Add(new LoyaltyTransaction
                                {
                                    DriverUserId = booking.DriverUserId,
                                    BookingId = booking.Id,
                                    Type = "Earn",
                                    Points = pointsEarned,
                                    Description = $"Tích {pointsEarned:N0} điểm từ booking #{booking.Id} (auto-confirm invoice)",
                                    CreatedAt = DateTimeHelper.VietnamNow()
                                });
                                await unitOfWork.CompleteAsync();
                            }

                            // Settle payment: ESCROW → Owner + PLATFORM_REVENUE
                            await SettlePaymentAsync(walletRepo, ledgerRepo, unitOfWork, booking, invoice);

                            await transaction.CommitAsync(stoppingToken);

                            // Notifications (ngoài transaction)
                            await notificationService.SendAsync(
                                booking.DriverUserId,
                                "Hóa đơn tự động xác nhận",
                                $"Hóa đơn phiên sạc tại trạm {booking.ChargingSlot?.ChargingStation?.Name} ({invoice.TotalAmount:N0}đ) đã được tự động xác nhận sau 24h.",
                                NotificationType.Payment);

                            var ownerUserId = booking.ChargingSlot?.ChargingStation?.OwnerUserId;
                            if (ownerUserId.HasValue)
                            {
                                await notificationService.SendAsync(
                                    ownerUserId.Value,
                                    "Thanh toán đã chuyển",
                                    $"Phiên sạc tại trạm {booking.ChargingSlot?.ChargingStation?.Name} đã tự động xác nhận. {invoice.ChargingAmount:N0}đ đã chuyển vào ví của bạn.",
                                    NotificationType.Payment);
                            }

                            _logger.LogInformation(
                                "Invoice {InvoiceId} (Booking {BookingId}) auto-confirmed after 24h.",
                                invoice.Id, booking.Id);
                        }
                        catch (Exception ex)
                        {
                            await transaction.RollbackAsync(stoppingToken);
                            _logger.LogError(ex, "Error auto-confirming invoice {InvoiceId}", invoiceId);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in InvoiceAutoConfirmJob");
                }

                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }

        private async Task SettlePaymentAsync(IWalletRepository walletRepo, ILedgerTransactionRepository ledgerRepo, IUnitOfWork unitOfWork, Booking booking, Invoice invoice)
        {
            var ownerUserId = booking.ChargingSlot!.ChargingStation!.OwnerUserId;
            var now = DateTimeHelper.VietnamNow();

            var escrowWallet = await walletRepo.GetBySystemCodeAsync("ESCROW")
                ?? throw new InvalidOperationException("ESCROW wallet not found");
            var platformWallet = await walletRepo.GetBySystemCodeAsync("PLATFORM_REVENUE")
                ?? throw new InvalidOperationException("PLATFORM_REVENUE wallet not found");
            var ownerWallet = await walletRepo.GetByUserIdAsync(ownerUserId);

            if (ownerWallet == null)
            {
                ownerWallet = new Wallet
                {
                    UserId = ownerUserId,
                    WalletType = WalletType.Owner,
                    AvailableBalance = 0,
                    FrozenBalance = 0,
                    CreatedAt = now
                };
                walletRepo.Add(ownerWallet);
                await unitOfWork.CompleteAsync();
            }

            var ownerNet = invoice.ChargingAmount;
            var platformFee = invoice.PlatformFee;

            // ESCROW → Owner
            escrowWallet.AvailableBalance -= ownerNet;
            ownerWallet.AvailableBalance += ownerNet;

            ledgerRepo.Add(new LedgerTransaction
            {
                ReferenceType = "AutoSettlement",
                ReferenceId = booking.Id,
                Memo = $"Auto-confirm booking #{booking.Id} - Owner nhận {ownerNet:N0}đ",
                CreatedAt = now,
                Entries = new List<LedgerEntry>
                {
                    new LedgerEntry { WalletId = escrowWallet.Id, Direction = LedgerDirection.Debit, Amount = ownerNet, CreatedAt = now },
                    new LedgerEntry { WalletId = ownerWallet.Id, Direction = LedgerDirection.Credit, Amount = ownerNet, CreatedAt = now }
                }
            });

            // ESCROW → PLATFORM_REVENUE
            escrowWallet.AvailableBalance -= platformFee;
            platformWallet.AvailableBalance += platformFee;

            ledgerRepo.Add(new LedgerTransaction
            {
                ReferenceType = "PlatformFee",
                ReferenceId = booking.Id,
                Memo = $"Phí nền tảng auto-confirm booking #{booking.Id} - {platformFee:N0}đ",
                CreatedAt = now,
                Entries = new List<LedgerEntry>
                {
                    new LedgerEntry { WalletId = escrowWallet.Id, Direction = LedgerDirection.Debit, Amount = platformFee, CreatedAt = now },
                    new LedgerEntry { WalletId = platformWallet.Id, Direction = LedgerDirection.Credit, Amount = platformFee, CreatedAt = now }
                }
            });

            await unitOfWork.CompleteAsync();
        }
    }
}
