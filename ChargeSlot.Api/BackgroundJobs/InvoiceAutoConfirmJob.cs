using ChargeSlot.Api.Data;
using ChargeSlot.Api.Enums;
using ChargeSlot.Api.Models;
using ChargeSlot.Api.Repositories.Interfaces;
using ChargeSlot.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

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

        private static readonly TimeSpan AutoConfirmDeadline = TimeSpan.FromHours(24);

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
                    var deadline = DateTimeHelper.VietnamNow() - AutoConfirmDeadline;

                    // 1. Chỉ lấy danh sách ID (tránh nạp nguyên object khổng lồ vào tracking của outer_scope)
                    using (var outerScope = _serviceProvider.CreateScope())
                    {
                        var outerDb = outerScope.ServiceProvider.GetRequiredService<ChargeSlotDbContext>();
                        expiredInvoiceIds = await outerDb.Invoices
                            .Where(i => i.Status == InvoiceStatus.PendingConfirm && i.CreatedAt <= deadline)
                            .Select(i => i.Id)
                            .ToListAsync(stoppingToken);
                    }

                    // 2. Xử lý từng ID trong 1 scope biệt lập
                    foreach (var invoiceId in expiredInvoiceIds)
                    {
                        using var innerScope = _serviceProvider.CreateScope();
                        var db = innerScope.ServiceProvider.GetRequiredService<ChargeSlotDbContext>();
                        var walletRepo = innerScope.ServiceProvider.GetRequiredService<IWalletRepository>();
                        var notificationService = innerScope.ServiceProvider.GetRequiredService<INotificationService>();

                        using var transaction = await db.Database.BeginTransactionAsync(stoppingToken);
                        try
                        {
                            var invoice = await db.Invoices
                                .Include(i => i.Booking)
                                    .ThenInclude(b => b.ChargingSlot).ThenInclude(s => s.ChargingStation)
                                .FirstOrDefaultAsync(i => i.Id == invoiceId, stoppingToken);

                            if (invoice == null || invoice.Status != InvoiceStatus.PendingConfirm)
                                continue;

                            var booking = invoice.Booking;

                            // Auto-confirm invoice
                            invoice.Status = InvoiceStatus.Confirmed;
                            invoice.UpdatedAt = DateTimeHelper.VietnamNow();

                            // Complete booking
                            booking.Status = BookingStatus.Completed;
                            booking.UpdatedAt = DateTimeHelper.VietnamNow();

                            await db.SaveChangesAsync(stoppingToken);

                            // Settle payment: ESCROW → Owner + PLATFORM_REVENUE
                            await SettlePaymentAsync(db, walletRepo, booking, invoice);

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

        // TODO: L5 — This settlement logic is duplicated from ChargingSessionService.SettlePaymentToOwnerAsync.
        // Refactor into a shared ISettlementService to avoid divergence.
        private async Task SettlePaymentAsync(ChargeSlotDbContext db, IWalletRepository walletRepo, Booking booking, Invoice invoice)
        {
            var ownerUserId = booking.ChargingSlot!.ChargingStation!.OwnerUserId;
            var now = DateTimeHelper.VietnamNow();

            var escrowWallet = await db.Wallets.FirstAsync(w => w.SystemCode == "ESCROW");
            var platformWallet = await db.Wallets.FirstAsync(w => w.SystemCode == "PLATFORM_REVENUE");
            var ownerWallet = await db.Wallets.FirstOrDefaultAsync(w => w.UserId == ownerUserId);

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
                db.Wallets.Add(ownerWallet);
                await db.SaveChangesAsync();
            }

            var ownerNet = invoice.ChargingAmount;
            var platformFee = invoice.PlatformFee;

            // ESCROW → Owner
            escrowWallet.AvailableBalance -= ownerNet;
            ownerWallet.AvailableBalance += ownerNet;

            db.Set<LedgerTransaction>().Add(new LedgerTransaction
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

            db.Set<LedgerTransaction>().Add(new LedgerTransaction
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

            await db.SaveChangesAsync();
        }
    }
}
