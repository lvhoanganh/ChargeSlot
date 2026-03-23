using ChargeSlot.Api.Data;
using ChargeSlot.Api.Enums;
using ChargeSlot.Api.Models;
using ChargeSlot.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ChargeSlot.Api.BackgroundJobs
{
    /// <summary>
    /// Auto-resolve disputes khi hết deadline:
    /// - WaitingOwnerEvidence > 24h → Driver thắng (refund)
    /// - PendingReview > 48h → Owner thắng (settle)
    /// Chạy mỗi 5 phút.
    /// </summary>
    public class DisputeAutoResolveJob : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<DisputeAutoResolveJob> _logger;

        private static readonly TimeSpan OwnerEvidenceDeadline = TimeSpan.FromHours(24);
        private static readonly TimeSpan AdminReviewDeadline = TimeSpan.FromHours(48);

        public DisputeAutoResolveJob(IServiceProvider serviceProvider, ILogger<DisputeAutoResolveJob> logger)
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
                    var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

                    await AutoResolveOwnerNoEvidenceAsync(db, notificationService, stoppingToken);
                    await AutoResolveAdminNoActionAsync(db, notificationService, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in DisputeAutoResolveJob");
                }

                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }

        /// <summary>
        /// Owner không nộp evidence sau 24h → Driver thắng → hoàn tiền.
        /// </summary>
        private async Task AutoResolveOwnerNoEvidenceAsync(
            ChargeSlotDbContext db, INotificationService notificationService, CancellationToken ct)
        {
            var deadline = DateTime.UtcNow - OwnerEvidenceDeadline;

            var expiredDisputes = await db.Disputes
                .Include(d => d.Booking)
                    .ThenInclude(b => b.ChargingSlot).ThenInclude(s => s.ChargingStation)
                .Include(d => d.Invoice)
                .Where(d => d.Status == DisputeStatus.WaitingOwnerEvidence
                    && d.CreatedAt <= deadline)
                .ToListAsync(ct);

            foreach (var dispute in expiredDisputes)
            {
                var now = DateTime.UtcNow;

                // Auto-resolve: Driver thắng
                dispute.Status = DisputeStatus.ResolvedRefund;
                dispute.AdminNote = "Tự động xử lý: Owner không phản hồi trong 24h. Driver được hoàn tiền.";
                dispute.ResolvedAt = now;

                // Invoice → Resolved
                if (dispute.Invoice != null)
                {
                    dispute.Invoice.Status = InvoiceStatus.Resolved;
                    dispute.Invoice.UpdatedAt = now;
                }

                // Booking → Completed
                dispute.Booking.Status = BookingStatus.Completed;
                dispute.Booking.UpdatedAt = now;

                await db.SaveChangesAsync(ct);

                // Refund: ESCROW.FrozenBalance → Driver
                await RefundToDriverAsync(db, dispute.Booking, dispute);

                // Notify
                await notificationService.SendAsync(
                    dispute.Booking.DriverUserId,
                    "Khiếu nại được giải quyết",
                    $"Khiếu nại #{dispute.Id} — Owner không phản hồi trong 24h. Số tiền {dispute.Booking.TotalAmount:N0}đ đã hoàn vào ví.",
                    NotificationType.Dispute);

                var ownerUserId = dispute.Booking.ChargingSlot?.ChargingStation?.OwnerUserId;
                if (ownerUserId.HasValue)
                {
                    await notificationService.SendAsync(
                        ownerUserId.Value,
                        "Khiếu nại tự động xử lý",
                        $"Khiếu nại #{dispute.Id} — Bạn không phản hồi trong 24h. Tiền đã hoàn cho Driver.",
                        NotificationType.Dispute);
                }

                _logger.LogInformation(
                    "Dispute {DisputeId} auto-resolved: Owner no evidence after 24h. Driver refunded {Amount}.",
                    dispute.Id, dispute.Booking.TotalAmount);
            }
        }

        /// <summary>
        /// Admin không xử lý sau 48h (từ khi Owner nộp evidence) → Owner thắng → settle payment.
        /// </summary>
        private async Task AutoResolveAdminNoActionAsync(
            ChargeSlotDbContext db, INotificationService notificationService, CancellationToken ct)
        {
            var deadline = DateTime.UtcNow - AdminReviewDeadline;

            // PendingReview disputes: tìm theo thời gian Owner nộp evidence
            // Vì không có trường riêng, dùng heuristic: dispute nào PendingReview quá 48h
            // (Status chuyển sang PendingReview khi Owner nộp evidence)
            var expiredDisputes = await db.Disputes
                .Include(d => d.Booking)
                    .ThenInclude(b => b.ChargingSlot).ThenInclude(s => s.ChargingStation)
                .Include(d => d.Invoice)
                .Where(d => d.Status == DisputeStatus.PendingReview
                    && d.CreatedAt <= deadline)  // fallback: dùng CreatedAt + tổng thời gian (24h owner + 48h admin = 72h)
                .ToListAsync(ct);

            foreach (var dispute in expiredDisputes)
            {
                var now = DateTime.UtcNow;

                // Auto-resolve: Owner thắng
                dispute.Status = DisputeStatus.ResolvedPayout;
                dispute.AdminNote = "Tự động xử lý: Admin không phân xử trong 48h. Owner nhận tiền.";
                dispute.ResolvedAt = now;

                // Invoice → Resolved
                if (dispute.Invoice != null)
                {
                    dispute.Invoice.Status = InvoiceStatus.Resolved;
                    dispute.Invoice.UpdatedAt = now;
                }

                // Booking → Completed
                dispute.Booking.Status = BookingStatus.Completed;
                dispute.Booking.UpdatedAt = now;

                await db.SaveChangesAsync(ct);

                // Settle: ESCROW.FrozenBalance → Owner + PLATFORM_REVENUE
                if (dispute.Invoice != null)
                {
                    await SettleToOwnerAsync(db, dispute.Booking, dispute.Invoice, dispute);
                }

                // Notify
                await notificationService.SendAsync(
                    dispute.Booking.DriverUserId,
                    "Khiếu nại được giải quyết",
                    $"Khiếu nại #{dispute.Id} — Admin không xử lý trong 48h. Tiền được chuyển cho Owner.",
                    NotificationType.Dispute);

                var ownerUserId = dispute.Booking.ChargingSlot?.ChargingStation?.OwnerUserId;
                if (ownerUserId.HasValue)
                {
                    await notificationService.SendAsync(
                        ownerUserId.Value,
                        "Khiếu nại tự động xử lý",
                        $"Khiếu nại #{dispute.Id} — Tự động giải quyết. Số tiền {dispute.Invoice?.ChargingAmount:N0}đ đã chuyển vào ví.",
                        NotificationType.Dispute);
                }

                _logger.LogInformation(
                    "Dispute {DisputeId} auto-resolved: Admin no action after 48h. Owner wins.",
                    dispute.Id);
            }
        }

        private async Task RefundToDriverAsync(ChargeSlotDbContext db, Booking booking, Dispute dispute)
        {
            var now = DateTime.UtcNow;
            var escrowWallet = await db.Wallets.FirstAsync(w => w.SystemCode == "ESCROW");
            var driverWallet = await db.Wallets.FirstOrDefaultAsync(w => w.UserId == booking.DriverUserId);

            if (driverWallet == null)
            {
                driverWallet = new Wallet
                {
                    UserId = booking.DriverUserId,
                    WalletType = WalletType.Driver,
                    AvailableBalance = 0, FrozenBalance = 0, CreatedAt = now
                };
                db.Wallets.Add(driverWallet);
                await db.SaveChangesAsync();
            }

            var refundAmount = booking.TotalAmount;
            escrowWallet.FrozenBalance -= refundAmount;
            driverWallet.AvailableBalance += refundAmount;

            db.Set<LedgerTransaction>().Add(new LedgerTransaction
            {
                ReferenceType = "AutoRefund",
                ReferenceId = booking.Id,
                Memo = $"Auto-refund Dispute #{dispute.Id} - Owner không phản hồi - {refundAmount:N0}đ → Driver",
                CreatedAt = now,
                Entries = new List<LedgerEntry>
                {
                    new LedgerEntry { WalletId = escrowWallet.Id, Direction = LedgerDirection.Debit, Amount = refundAmount, CreatedAt = now },
                    new LedgerEntry { WalletId = driverWallet.Id, Direction = LedgerDirection.Credit, Amount = refundAmount, CreatedAt = now }
                }
            });
            await db.SaveChangesAsync();
        }

        private async Task SettleToOwnerAsync(ChargeSlotDbContext db, Booking booking, Invoice invoice, Dispute dispute)
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
                    AvailableBalance = 0, FrozenBalance = 0, CreatedAt = now
                };
                db.Wallets.Add(ownerWallet);
                await db.SaveChangesAsync();
            }

            var ownerNet = invoice.ChargingAmount;
            var platformFee = invoice.PlatformFee;
            var vatAmount = invoice.VatAmount;

            escrowWallet.FrozenBalance -= (ownerNet + platformFee + vatAmount);
            ownerWallet.AvailableBalance += ownerNet;
            platformWallet.AvailableBalance += platformFee;

            db.Set<LedgerTransaction>().Add(new LedgerTransaction
            {
                ReferenceType = "AutoSettlement",
                ReferenceId = booking.Id,
                Memo = $"Auto-settle Dispute #{dispute.Id} - Admin timeout - Owner nhận {ownerNet:N0}đ",
                CreatedAt = now,
                Entries = new List<LedgerEntry>
                {
                    new LedgerEntry { WalletId = escrowWallet.Id, Direction = LedgerDirection.Debit, Amount = ownerNet, CreatedAt = now },
                    new LedgerEntry { WalletId = ownerWallet.Id, Direction = LedgerDirection.Credit, Amount = ownerNet, CreatedAt = now }
                }
            });

            db.Set<LedgerTransaction>().Add(new LedgerTransaction
            {
                ReferenceType = "PlatformFee",
                ReferenceId = booking.Id,
                Memo = $"Phí nền tảng auto-settle Dispute #{dispute.Id} - {platformFee:N0}đ",
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
