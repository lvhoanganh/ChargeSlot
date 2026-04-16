using ChargeSlot.Api.Enums;
using ChargeSlot.Api.Models;
using ChargeSlot.Api.Models.Identity;
using ChargeSlot.Api.Repositories.Interfaces;
using ChargeSlot.Api.Services.Interfaces;

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
            var now = DateTimeHelper.VietnamNow();
            List<int> expiredDisputeIds;

            using (var outerScope = serviceProvider.CreateScope())
            {
                var disputeRepo = outerScope.ServiceProvider.GetRequiredService<IDisputeRepository>();
                expiredDisputeIds = await disputeRepo.GetExpiredOwnerEvidenceIdsAsync(now);
            }

            foreach (var disputeId in expiredDisputeIds)
            {
                using var innerScope = serviceProvider.CreateScope();
                var disputeRepo = innerScope.ServiceProvider.GetRequiredService<IDisputeRepository>();
                var unitOfWork = innerScope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                var walletRepo = innerScope.ServiceProvider.GetRequiredService<IWalletRepository>();
                var ledgerRepo = innerScope.ServiceProvider.GetRequiredService<ILedgerTransactionRepository>();
                var notificationService = innerScope.ServiceProvider.GetRequiredService<INotificationService>();
                var adminAccountRepo = innerScope.ServiceProvider.GetRequiredService<IAdminAccountRepository>();
                var driverRepo = innerScope.ServiceProvider.GetRequiredService<IDriverRepository>();
                var loyaltyTxRepo = innerScope.ServiceProvider.GetRequiredService<ILoyaltyTransactionRepository>();

                using var transaction = await unitOfWork.BeginTransactionAsync();
                try
                {
                    var dispute = await disputeRepo.GetByIdWithBookingAndInvoiceDetailsAsync(disputeId);

                    if (dispute == null || dispute.Status != DisputeStatus.WaitingOwnerEvidence)
                        continue;

                    var currentTime = DateTimeHelper.VietnamNow();

                    // Auto-resolve: Driver thắng
                    dispute.Status = DisputeStatus.ResolvedRefund;
                    dispute.AdminNote = "Tự động xử lý: Owner không phản hồi trong 24h. Driver được hoàn tiền.";
                    dispute.ResolvedAt = currentTime;

                    // Invoice → Resolved
                    if (dispute.Invoice != null)
                    {
                        dispute.Invoice.Status = InvoiceStatus.Resolved;
                        dispute.Invoice.UpdatedAt = currentTime;
                    }

                    // Booking → Completed
                    dispute.Booking.Status = BookingStatus.Completed;
                    dispute.Booking.UpdatedAt = currentTime;

                    await unitOfWork.CompleteAsync();

                    // Refund: ESCROW.FrozenBalance → Driver + Hoàn Loyalty Points
                    await RefundToDriverAsync(walletRepo, ledgerRepo, unitOfWork, driverRepo, loyaltyTxRepo, dispute.Booking, dispute);

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

                    var adminUserIds = await adminAccountRepo.GetAdminUserIdsAsync();
                    foreach (var adminId in adminUserIds)
                    {
                        await notificationService.SendAsync(
                            adminId,
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
            var now = DateTimeHelper.VietnamNow();
            List<int> expiredDisputeIds;

            using (var outerScope = serviceProvider.CreateScope())
            {
                var disputeRepo = outerScope.ServiceProvider.GetRequiredService<IDisputeRepository>();
                expiredDisputeIds = await disputeRepo.GetExpiredAdminReviewIdsAsync(now);
            }

            foreach (var disputeId in expiredDisputeIds)
            {
                using var innerScope = serviceProvider.CreateScope();
                var disputeRepo = innerScope.ServiceProvider.GetRequiredService<IDisputeRepository>();
                var unitOfWork = innerScope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                var walletRepo = innerScope.ServiceProvider.GetRequiredService<IWalletRepository>();
                var ledgerRepo = innerScope.ServiceProvider.GetRequiredService<ILedgerTransactionRepository>();
                var notificationService = innerScope.ServiceProvider.GetRequiredService<INotificationService>();
                var adminAccountRepo = innerScope.ServiceProvider.GetRequiredService<IAdminAccountRepository>();

                using var transaction = await unitOfWork.BeginTransactionAsync();
                try
                {
                    var dispute = await disputeRepo.GetByIdWithBookingAndInvoiceDetailsAsync(disputeId);

                    if (dispute == null || dispute.Status != DisputeStatus.PendingReview)
                        continue;

                    var currentTime = DateTimeHelper.VietnamNow();

                    // Auto-resolve: Owner thắng
                    dispute.Status = DisputeStatus.ResolvedPayout;
                    dispute.AdminNote = "Tự động xử lý: Admin không phân xử trong 48h. Owner nhận tiền.";
                    dispute.ResolvedAt = currentTime;

                    // Invoice → Resolved
                    if (dispute.Invoice != null)
                    {
                        dispute.Invoice.Status = InvoiceStatus.Resolved;
                        dispute.Invoice.UpdatedAt = currentTime;
                    }

                    // Booking → Completed
                    dispute.Booking.Status = BookingStatus.Completed;
                    dispute.Booking.UpdatedAt = currentTime;

                    await unitOfWork.CompleteAsync();

                    // Settle: ESCROW.FrozenBalance → Owner + PLATFORM_REVENUE
                    if (dispute.Invoice != null)
                    {
                        await SettleToOwnerAsync(walletRepo, ledgerRepo, unitOfWork, dispute.Booking, dispute.Invoice, dispute);
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

                    var adminUserIds = await adminAccountRepo.GetAdminUserIdsAsync();
                    foreach (var adminId in adminUserIds)
                    {
                        await notificationService.SendAsync(
                            adminId,
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

        private async Task RefundToDriverAsync(IWalletRepository walletRepo, ILedgerTransactionRepository ledgerRepo, IUnitOfWork unitOfWork,
            IDriverRepository driverRepo, ILoyaltyTransactionRepository loyaltyTxRepo,
            Booking booking, Dispute dispute)
        {
            var now = DateTimeHelper.VietnamNow();
            var escrowWallet = await walletRepo.GetBySystemCodeAsync("ESCROW")
                ?? throw new InvalidOperationException("ESCROW wallet not found");
            var driverWallet = await walletRepo.GetByUserIdAsync(booking.DriverUserId);

            if (driverWallet == null)
            {
                driverWallet = new Wallet
                {
                    UserId = booking.DriverUserId,
                    WalletType = WalletType.Driver,
                    AvailableBalance = 0, FrozenBalance = 0, CreatedAt = now
                };
                walletRepo.Add(driverWallet);
                await unitOfWork.CompleteAsync();
            }

            var refundAmount = booking.TotalAmount;

            // Unfreeze from ESCROW.FrozenBalance → refund to Driver (Atomic via Repository)
            await walletRepo.AdjustBalanceAtomicAsync(escrowWallet.Id, 0, -refundAmount);
            await walletRepo.AdjustBalanceAtomicAsync(driverWallet.Id, refundAmount, 0);

            ledgerRepo.Add(new LedgerTransaction
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
            await unitOfWork.CompleteAsync();

            // Hoàn Loyalty Points nếu booking đã dùng điểm
            if (booking.PointsUsed > 0 && booking.Driver != null)
            {
                booking.Driver.LoyaltyPoints += booking.PointsUsed;
                driverRepo.Update(booking.Driver);

                loyaltyTxRepo.Add(new LoyaltyTransaction
                {
                    DriverUserId = booking.DriverUserId,
                    BookingId = booking.Id,
                    Type = "Refund",
                    Points = booking.PointsUsed,
                    Description = $"Hoàn {booking.PointsUsed:N0} điểm (thắng khiếu nại tự động #{dispute.Id})",
                    CreatedAt = now
                });
                await unitOfWork.CompleteAsync();
            }
        }

        private async Task SettleToOwnerAsync(IWalletRepository walletRepo, ILedgerTransactionRepository ledgerRepo, IUnitOfWork unitOfWork, Booking booking, Invoice invoice, Dispute dispute)
        {
            var ownerUserId = booking.ChargingSlot!.ChargingStation!.OwnerUserId;
            var now = DateTimeHelper.VietnamNow();

            var escrowWallet = await walletRepo.GetBySystemCodeAsync("ESCROW")
                ?? throw new InvalidOperationException("ESCROW wallet not found");
            var platformWallet = await walletRepo.GetBySystemCodeAsync("PLATFORM_REVENUE")
                ?? throw new InvalidOperationException("PLATFORM_REVENUE wallet not found");
            var taxWallet = await walletRepo.GetBySystemCodeAsync("TAX_HOLD")
                ?? throw new InvalidOperationException("TAX_HOLD wallet not found");
            var ownerWallet = await walletRepo.GetByUserIdAsync(ownerUserId);

            if (ownerWallet == null)
            {
                ownerWallet = new Wallet
                {
                    UserId = ownerUserId,
                    WalletType = WalletType.Owner,
                    AvailableBalance = 0, FrozenBalance = 0, CreatedAt = now
                };
                walletRepo.Add(ownerWallet);
                await unitOfWork.CompleteAsync();
            }

            var ownerNet = invoice.ChargingAmount;
            var platformFee = invoice.PlatformFee;
            var vatAmount = invoice.VatAmount;

            // 0. Bù tiền bảo trợ Điểm thưởng vào ESCROW trước khi settle (Atomic via Repository)
            if (booking.PointsDiscountAmount > 0)
            {
                await walletRepo.TransferAtomicAsync(platformWallet.Id, escrowWallet.Id, booking.PointsDiscountAmount);

                ledgerRepo.Add(new LedgerTransaction
                {
                    ReferenceType = "PointsSubsidy",
                    ReferenceId = booking.Id,
                    Memo = $"Nền tảng bù {booking.PointsDiscountAmount:N0}đ chiết khấu điểm thưởng cho Dispute #{dispute.Id}",
                    CreatedAt = now,
                    Entries = new List<LedgerEntry>
                    {
                        new LedgerEntry { WalletId = platformWallet.Id, Direction = LedgerDirection.Debit, Amount = booking.PointsDiscountAmount, CreatedAt = now },
                        new LedgerEntry { WalletId = escrowWallet.Id, Direction = LedgerDirection.Credit, Amount = booking.PointsDiscountAmount, CreatedAt = now }
                    }
                });
                await unitOfWork.CompleteAsync();
            }

            // 1. Unfreeze ALL from ESCROW.FrozenBalance (Atomic via Repository)
            await walletRepo.UnfreezeAtomicAsync(escrowWallet.Id, ownerNet + platformFee + vatAmount);

            // 2. ESCROW → Owner (Atomic via Repository)
            await walletRepo.TransferAtomicAsync(escrowWallet.Id, ownerWallet.Id, ownerNet);

            ledgerRepo.Add(new LedgerTransaction
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
            await unitOfWork.CompleteAsync();

            // 3. ESCROW → PLATFORM_REVENUE (Atomic via Repository)
            await walletRepo.TransferAtomicAsync(escrowWallet.Id, platformWallet.Id, platformFee);

            ledgerRepo.Add(new LedgerTransaction
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
            await unitOfWork.CompleteAsync();

            // 4. ESCROW → TAX_HOLD (Atomic via Repository)
            if (vatAmount > 0)
            {
                await walletRepo.TransferAtomicAsync(escrowWallet.Id, taxWallet.Id, vatAmount);

                ledgerRepo.Add(new LedgerTransaction
                {
                    ReferenceType = "TaxHold",
                    ReferenceId = booking.Id,
                    Memo = $"Thuế GTGT auto-settle Dispute #{dispute.Id} - {vatAmount:N0}đ",
                    CreatedAt = now,
                    Entries = new List<LedgerEntry>
                    {
                        new LedgerEntry { WalletId = escrowWallet.Id, Direction = LedgerDirection.Debit, Amount = vatAmount, CreatedAt = now },
                        new LedgerEntry { WalletId = taxWallet.Id, Direction = LedgerDirection.Credit, Amount = vatAmount, CreatedAt = now }
                    }
                });
                await unitOfWork.CompleteAsync();
            }
        }
    }
}
