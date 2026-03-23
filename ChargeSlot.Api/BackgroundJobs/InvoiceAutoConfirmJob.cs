using ChargeSlot.Api.Data;
using ChargeSlot.Api.Enums;
using ChargeSlot.Api.Models;
using ChargeSlot.Api.Repositories.Interfaces;
using ChargeSlot.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

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
                    using var scope = _serviceProvider.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<ChargeSlotDbContext>();
                    var walletRepo = scope.ServiceProvider.GetRequiredService<IWalletRepository>();
                    var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

                    var deadline = DateTime.UtcNow - AutoConfirmDeadline;

                    // Tìm invoices PendingConfirm quá 24h
                    var expiredInvoices = await db.Invoices
                        .Include(i => i.Booking)
                            .ThenInclude(b => b.ChargingSlot).ThenInclude(s => s.ChargingStation)
                        .Where(i => i.Status == InvoiceStatus.PendingConfirm
                            && i.CreatedAt <= deadline)
                        .ToListAsync(stoppingToken);

                    foreach (var invoice in expiredInvoices)
                    {
                        var booking = invoice.Booking;

                        // Auto-confirm invoice
                        invoice.Status = InvoiceStatus.Confirmed;
                        invoice.UpdatedAt = DateTime.UtcNow;

                        // Complete booking
                        booking.Status = BookingStatus.Completed;
                        booking.UpdatedAt = DateTime.UtcNow;

                        await db.SaveChangesAsync(stoppingToken);

                        // Settle payment: ESCROW → Owner + PLATFORM_REVENUE
                        await SettlePaymentAsync(db, walletRepo, booking, invoice);

                        // Notify Driver
                        await notificationService.SendAsync(
                            booking.DriverUserId,
                            "Hóa đơn tự động xác nhận",
                            $"Hóa đơn booking #{booking.Id} đã được tự động xác nhận sau 24h. Số tiền {invoice.TotalAmount:N0}đ.",
                            NotificationType.Payment);

                        // Notify Owner
                        var ownerUserId = booking.ChargingSlot?.ChargingStation?.OwnerUserId;
                        if (ownerUserId.HasValue)
                        {
                            await notificationService.SendAsync(
                                ownerUserId.Value,
                                "Thanh toán đã chuyển",
                                $"Booking #{booking.Id} auto-confirm. Số tiền {invoice.ChargingAmount:N0}đ đã chuyển vào ví.",
                                NotificationType.Payment);
                        }

                        _logger.LogInformation(
                            "Invoice {InvoiceId} (Booking {BookingId}) auto-confirmed after 24h.",
                            invoice.Id, booking.Id);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in InvoiceAutoConfirmJob");
                }

                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }

        /// <summary>
        /// Giống SettlePaymentToOwnerAsync trong ChargingSessionService
        /// </summary>
        private async Task SettlePaymentAsync(ChargeSlotDbContext db, IWalletRepository walletRepo, Booking booking, Invoice invoice)
        {
            var ownerUserId = booking.ChargingSlot!.ChargingStation!.OwnerUserId;
            var now = DateTime.UtcNow;

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
