using ChargeSlot.Api.Data;
using ChargeSlot.Api.Enums;
using ChargeSlot.Api.Models;
using ChargeSlot.Api.Models.Identity;
using ChargeSlot.Api.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

using ChargeSlot.Api.Helpers;
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
                    await AutoResolveOwnerNoEvidenceAsync(_serviceProvider, stoppingToken);
                    await AutoResolveAdminNoActionAsync(_serviceProvider, stoppingToken);
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
        private async Task AutoResolveOwnerNoEvidenceAsync(IServiceProvider serviceProvider, CancellationToken ct)
        {
            var deadline = DateTimeHelper.VietnamNow() - OwnerEvidenceDeadline;
            List<int> expiredDisputeIds;

            using (var outerScope = serviceProvider.CreateScope())
            {
                var outerDb = outerScope.ServiceProvider.GetRequiredService<ChargeSlotDbContext>();
                expiredDisputeIds = await outerDb.Disputes
                    .Where(d => d.Status == DisputeStatus.WaitingOwnerEvidence && (d.StatusChangedAt ?? d.CreatedAt) <= deadline)
                    .Select(d => d.Id)
                    .ToListAsync(ct);
            }

            foreach (var disputeId in expiredDisputeIds)
            {
                using var innerScope = serviceProvider.CreateScope();
                var db = innerScope.ServiceProvider.GetRequiredService<ChargeSlotDbContext>();
                var notificationService = innerScope.ServiceProvider.GetRequiredService<INotificationService>();
                var userManager = innerScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

                using var transaction = await db.Database.BeginTransactionAsync(ct);
                try
                {
                    var dispute = await db.Disputes
                        .Include(d => d.Booking)
                            .ThenInclude(b => b.ChargingSlot).ThenInclude(s => s.ChargingStation)
                        .Include(d => d.Invoice)
                        .FirstOrDefaultAsync(d => d.Id == disputeId, ct);

                    if (dispute == null || dispute.Status != DisputeStatus.WaitingOwnerEvidence)
                        continue;

                    var now = DateTimeHelper.VietnamNow();

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

                    await transaction.CommitAsync(ct);

                    // Notifications (ngoài transaction)
                    await notificationService.SendAsync(
                        dispute.Booking.DriverUserId,
                        "Khiếu nại được giải quyết",
                        $"Khiếu nại của bạn tại trạm {dispute.Booking.ChargingSlot?.ChargingStation?.Name} được chấp nhận do Owner không phản hồi trong 24h. {dispute.Booking.TotalAmount:N0}đ đã hoàn vào ví.",
                        NotificationType.Dispute);

                    var ownerUserId = dispute.Booking.ChargingSlot?.ChargingStation?.OwnerUserId;
                    if (ownerUserId.HasValue)
                    {
                        await notificationService.SendAsync(
                            ownerUserId.Value,
                            "Khiếu nại tự động xử lý",
                            $"Khiếu nại tại trạm {dispute.Booking.ChargingSlot?.ChargingStation?.Name}: Bạn không phản hồi trong 24h nên tiền đã được hoàn cho Driver.",
                            NotificationType.Dispute);
                    }

                    _logger.LogInformation(
                        "Dispute {DisputeId} auto-resolved: Owner no evidence after 24h. Driver refunded {Amount}.",
                        dispute.Id, dispute.Booking.TotalAmount);

                    var adminUsers = await userManager.GetUsersInRoleAsync(Constants.RoleConstants.Admin);
                    foreach (var admin in adminUsers)
                    {
                        await notificationService.SendAsync(
                            admin.Id,
                            "Khiếu nại tự động xử lý",
                            $"Khiếu nại tại trạm {dispute.Booking.ChargingSlot?.ChargingStation?.Name}: Owner không phản hồi 24h → Driver được hoàn tiền {dispute.Booking.TotalAmount:N0}đ.",
                            NotificationType.Dispute);
                    }
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync(ct);
                    _logger.LogError(ex, "Error auto-resolving dispute {DisputeId} (owner no evidence)", disputeId);
                }
            }
        }

        /// <summary>
        /// Admin không xử lý sau 48h (từ khi Owner nộp evidence) → Owner thắng → settle payment.
        /// </summary>
        private async Task AutoResolveAdminNoActionAsync(IServiceProvider serviceProvider, CancellationToken ct)
        {
            var deadline = DateTimeHelper.VietnamNow() - AdminReviewDeadline;
            List<int> expiredDisputeIds;

            using (var outerScope = serviceProvider.CreateScope())
            {
                var outerDb = outerScope.ServiceProvider.GetRequiredService<ChargeSlotDbContext>();
                expiredDisputeIds = await outerDb.Disputes
                    .Where(d => d.Status == DisputeStatus.PendingReview && (d.StatusChangedAt ?? d.CreatedAt) <= deadline)
                    .Select(d => d.Id)
                    .ToListAsync(ct);
            }

            foreach (var disputeId in expiredDisputeIds)
            {
                using var innerScope = serviceProvider.CreateScope();
                var db = innerScope.ServiceProvider.GetRequiredService<ChargeSlotDbContext>();
                var notificationService = innerScope.ServiceProvider.GetRequiredService<INotificationService>();
                var userManager = innerScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

                using var transaction = await db.Database.BeginTransactionAsync(ct);
                try
                {
                    var dispute = await db.Disputes
                        .Include(d => d.Booking)
                            .ThenInclude(b => b.ChargingSlot).ThenInclude(s => s.ChargingStation)
                        .Include(d => d.Invoice)
                        .FirstOrDefaultAsync(d => d.Id == disputeId, ct);

                    if (dispute == null || dispute.Status != DisputeStatus.PendingReview)
                        continue;

                    var now = DateTimeHelper.VietnamNow();

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

                    await transaction.CommitAsync(ct);

                    // Notifications (ngoài transaction)
                    await notificationService.SendAsync(
                        dispute.Booking.DriverUserId,
                        "Khiếu nại được giải quyết",
                        $"Khiếu nại của bạn tại trạm {dispute.Booking.ChargingSlot?.ChargingStation?.Name} đã tự động giải quyết. Tiền được chuyển cho chủ trạm.",
                        NotificationType.Dispute);

                    var ownerUserId = dispute.Booking.ChargingSlot?.ChargingStation?.OwnerUserId;
                    if (ownerUserId.HasValue)
                    {
                        await notificationService.SendAsync(
                            ownerUserId.Value,
                            "Khiếu nại tự động xử lý",
                            $"Khiếu nại tại trạm {dispute.Booking.ChargingSlot?.ChargingStation?.Name} đã tự động giải quyết. {dispute.Invoice?.ChargingAmount:N0}đ đã chuyển vào ví của bạn.",
                            NotificationType.Dispute);
                    }

                    _logger.LogInformation(
                        "Dispute {DisputeId} auto-resolved: Admin no action after 48h. Owner wins.",
                        dispute.Id);

                    var adminUsers = await userManager.GetUsersInRoleAsync(Constants.RoleConstants.Admin);
                    foreach (var admin in adminUsers)
                    {
                        await notificationService.SendAsync(
                            admin.Id,
                            "Khiếu nại tự động xử lý",
                            $"Khiếu nại tại trạm {dispute.Booking.ChargingSlot?.ChargingStation?.Name}: Quá hạn 48h không phân xử → Owner nhận tiền {dispute.Invoice?.ChargingAmount:N0}đ.",
                            NotificationType.Dispute);
                    }
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync(ct);
                    _logger.LogError(ex, "Error auto-resolving dispute {DisputeId} (admin no action)", disputeId);
                }
            }
        }

        private async Task RefundToDriverAsync(ChargeSlotDbContext db, Booking booking, Dispute dispute)
        {
            var now = DateTimeHelper.VietnamNow();
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
