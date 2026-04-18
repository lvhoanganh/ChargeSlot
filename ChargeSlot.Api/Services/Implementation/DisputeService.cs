using ChargeSlot.Api.Helpers;
using ChargeSlot.Api.DTOs.Dispute;
using ChargeSlot.Api.Enums;
using ChargeSlot.Api.Models;
using ChargeSlot.Api.Models.Identity;
using ChargeSlot.Api.Repositories.Interfaces;
using ChargeSlot.Api.Services.Interfaces;
using Microsoft.AspNetCore.Identity;

namespace ChargeSlot.Api.Services.Implementation
{
    public class DisputeService : IDisputeService
    {
        private readonly INotificationService _notificationService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IDisputeRepository _disputeRepo;
        private readonly IBookingRepository _bookingRepo;
        private readonly IInvoiceRepository _invoiceRepo;
        private readonly IWalletRepository _walletRepo;
        private readonly ILedgerTransactionRepository _ledgerRepo;
        private readonly IChargingStationRepository _stationRepo;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IFileStorageService _fileStorageService;
        private readonly Lazy<IBookingService> _lazyBookingService;
        private readonly Lazy<ISystemConfigService> _lazyConfigService;
        private readonly Lazy<IDriverRepository> _lazyDriverRepo;
        private readonly Lazy<ILoyaltyTransactionRepository> _lazyLoyaltyRepo;

        private static readonly string[] AllowedFileExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp", ".mp4", ".avi", ".mov", ".webm", ".pdf" };
        private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10MB

        public DisputeService(
            INotificationService notificationService,
            IUnitOfWork unitOfWork,
            IDisputeRepository disputeRepo,
            IBookingRepository bookingRepo,
            IInvoiceRepository invoiceRepo,
            IWalletRepository walletRepo,
            ILedgerTransactionRepository ledgerRepo,
            IChargingStationRepository stationRepo,
            UserManager<ApplicationUser> userManager,
            IFileStorageService fileStorageService,
            IServiceProvider serviceProvider)
        {
            _notificationService = notificationService;
            _unitOfWork = unitOfWork;
            _disputeRepo = disputeRepo;
            _bookingRepo = bookingRepo;
            _invoiceRepo = invoiceRepo;
            _walletRepo = walletRepo;
            _ledgerRepo = ledgerRepo;
            _stationRepo = stationRepo;
            _userManager = userManager;
            _fileStorageService = fileStorageService;
            _lazyBookingService = new Lazy<IBookingService>(() => serviceProvider.GetRequiredService<IBookingService>());
            _lazyConfigService = new Lazy<ISystemConfigService>(() => serviceProvider.GetRequiredService<ISystemConfigService>());
            _lazyDriverRepo = new Lazy<IDriverRepository>(() => serviceProvider.GetRequiredService<IDriverRepository>());
            _lazyLoyaltyRepo = new Lazy<ILoyaltyTransactionRepository>(() => serviceProvider.GetRequiredService<ILoyaltyTransactionRepository>());
        }

        /// <summary>
        /// Driver submits dispute: validate booking = CompletedPendingInvoice →
        /// create Dispute + evidence → freeze payment (invoice = UnderDispute) →
        /// booking = Disputed → notify Owner + Admin.
        /// </summary>
        public async Task<DisputeDto> SubmitDisputeAsync(int driverUserId, CreateDisputeDto dto)
        {
            using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                var booking = await _bookingRepo.GetByIdWithDetailsAsync(dto.BookingId)
                    ?? throw new InvalidOperationException("Booking không tồn tại.");

                if (booking.DriverUserId != driverUserId)
                    throw new InvalidOperationException("Booking này không thuộc về bạn.");

                if (booking.Status != BookingStatus.CompletedPendingInvoice)
                    throw new InvalidOperationException("Chỉ có thể khiếu nại khi booking đang chờ xác nhận hóa đơn.");

                // Check if dispute already exists
                var existing = await _disputeRepo.HasDisputeForBookingAsync(dto.BookingId);
                if (existing)
                    throw new InvalidOperationException("Đã có khiếu nại cho booking này.");

                // Load configs for dispute settings
                var configs = await _lazyConfigService.Value.GetCurrentConfigsAsync();

                // Rate limit: max disputes/driver/tháng (dùng config thay hardcode)
                var monthStart = new DateTime(DateTimeHelper.VietnamNow().Year, DateTimeHelper.VietnamNow().Month, 1);
                var disputeCountThisMonth = await _disputeRepo.GetDisputeCountByDriverInMonthAsync(driverUserId, monthStart);
                if (disputeCountThisMonth >= configs.Dispute_Limit_Per_Month)
                    throw new InvalidOperationException($"Bạn đã đạt giới hạn {configs.Dispute_Limit_Per_Month} khiếu nại/tháng. Vui lòng liên hệ hotline nếu cần hỗ trợ thêm.");

                // Get invoice
                var invoice = await _invoiceRepo.GetByBookingIdAsync(dto.BookingId);

                // Create dispute
                var dispute = new Dispute
                {
                    BookingId = dto.BookingId,
                    InvoiceId = invoice?.Id,
                    CreatedByUserId = driverUserId,
                    Reason = dto.Reason,
                    Description = dto.Description,
                    Status = DisputeStatus.WaitingOwnerEvidence,
                    StatusChangedAt = DateTimeHelper.VietnamNow(),
                    OwnerEvidenceDeadlineAt = DateTimeHelper.VietnamNow().AddHours(configs.Dispute_OwnerEvidence_Hours),
                    CreatedAt = DateTimeHelper.VietnamNow()
                };

                _disputeRepo.Add(dispute);

                // Freeze payment: invoice → UnderDispute
                if (invoice != null)
                {
                    invoice.Status = InvoiceStatus.UnderDispute;
                    invoice.UpdatedAt = DateTimeHelper.VietnamNow();
                    _invoiceRepo.Update(invoice);
                }

                // Booking → Disputed
                booking.Status = BookingStatus.Disputed;
                booking.UpdatedAt = DateTimeHelper.VietnamNow();
                _bookingRepo.Update(booking);

                // Freeze ESCROW balance (Atomic via Repository)
                var escrowWallet = await _walletRepo.GetBySystemCodeAsync("ESCROW");
                await _walletRepo.AdjustBalanceAtomicAsync(escrowWallet!.Id, -booking.TotalAmount, booking.TotalAmount);

                await _unitOfWork.CompleteAsync();
                await transaction.CommitAsync();

                // Evidence upload + notifications ngoài transaction
                if (dto.Files?.Length > 0)
                {
                    await SaveEvidenceFilesAsync(dispute, dto.Files, driverUserId);
                }

                var ownerUserId = booking.ChargingSlot.ChargingStation.OwnerUserId;
                await _notificationService.SendAsync(
                    ownerUserId,
                    "Khiếu nại mới từ Driver",
                    $"{booking.Driver?.User?.FullName ?? "Driver"} khiếu nại về phiên sạc tại trạm {booking.ChargingSlot?.ChargingStation?.Name}. Lý do: {dto.Reason}. Bạn có {configs.Dispute_OwnerEvidence_Hours}h để nộp bằng chứng phản hồi.",
                    NotificationType.Dispute);

                var adminUsers = await _userManager.GetUsersInRoleAsync(Constants.RoleConstants.Admin);
                foreach (var admin in adminUsers)
                {
                    await _notificationService.SendAsync(
                        admin.Id,
                        "Khiếu nại mới cần xử lý",
                        $"Khiếu nại mới tại trạm {booking.ChargingSlot?.ChargingStation?.Name} từ {booking.Driver?.User?.FullName ?? "Driver"}. Chờ Owner phản hồi.",
                        NotificationType.Dispute);
                }

                var result = await LoadDisputeWithDetailsAsync(dispute.Id);
                return MapToDto(result!);
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        /// <summary>
        /// Owner submits response + evidence → dispute = PendingReview → notify Admin.
        /// </summary>
        public async Task<DisputeDto> SubmitOwnerEvidenceAsync(int ownerUserId, int disputeId, OwnerEvidenceDto dto)
        {
            var dispute = await _disputeRepo.GetByIdWithDetailsAsync(disputeId)
                ?? throw new InvalidOperationException("Khiếu nại không tồn tại.");

            // Validate owner
            var ownerOfStation = dispute.Booking.ChargingSlot.ChargingStation.OwnerUserId;
            if (ownerOfStation != ownerUserId)
                throw new UnauthorizedAccessException("Bạn không có quyền phản hồi khiếu nại này.");

            if (dispute.Status != DisputeStatus.WaitingOwnerEvidence)
                throw new InvalidOperationException("Khiếu nại không ở trạng thái chờ bằng chứng.");

            // Update response
            var configs = await _lazyConfigService.Value.GetCurrentConfigsAsync();
            dispute.OwnerResponse = dto.Response;
            dispute.Status = DisputeStatus.PendingReview;
            dispute.StatusChangedAt = DateTimeHelper.VietnamNow();
            dispute.AdminReviewDeadlineAt = DateTimeHelper.VietnamNow().AddHours(configs.Dispute_AdminReview_Hours);

            // Upload evidence files
            if (dto.Files?.Length > 0)
            {
                await SaveEvidenceFilesAsync(dispute, dto.Files, ownerUserId);
            }

            await _unitOfWork.CompleteAsync();

            // Notify Driver: Owner đã phản hồi
            await _notificationService.SendAsync(
                dispute.Booking.DriverUserId,
                "Owner đã phản hồi khiếu nại",
                $"Chủ trạm {dispute.Booking.ChargingSlot?.ChargingStation?.Name} đã nộp bằng chứng phản hồi khiếu nại của bạn. Chờ Admin xem xét.",
                NotificationType.Dispute);

            // Notify Admin (dùng UserManager thay vì hardcode RoleId)
            var adminUsers2 = await _userManager.GetUsersInRoleAsync(Constants.RoleConstants.Admin);

            foreach (var admin in adminUsers2)
            {
                await _notificationService.SendAsync(
                    admin.Id,
                    "Owner đã phản hồi khiếu nại",
                    $"Khiếu nại tại trạm {dispute.Booking.ChargingSlot?.ChargingStation?.Name} đã có phản hồi từ Owner. Sẵn sàng xem xét.",
                    NotificationType.Dispute);
            }

            var result = await LoadDisputeWithDetailsAsync(dispute.Id);
            return MapToDto(result!);
        }

        /// <summary>
        /// Admin resolves dispute:
        /// - Driver wins → ResolvedRefund → ESCROW → Driver (full refund)
        /// - Owner wins → ResolvedPayout → ESCROW → Owner (net) + PLATFORM_REVENUE (fee)
        /// Both → invoice = Resolved, booking = Completed
        /// </summary>
        public async Task<DisputeDto> ResolveDisputeAsync(int adminUserId, int disputeId, ResolveDisputeDto dto)
        {
            using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                var dispute = await _disputeRepo.GetByIdWithDetailsAsync(disputeId)
                    ?? throw new InvalidOperationException("Khiếu nại không tồn tại.");

                if (dispute.Status != DisputeStatus.PendingReview)
                    throw new InvalidOperationException("Khiếu nại chưa sẵn sàng để xử lý. Cần chờ Owner nộp bằng chứng phản hồi trước.");

                var now = DateTimeHelper.VietnamNow();

                // Resolve dispute
                dispute.Status = dto.IsDriverWin ? DisputeStatus.ResolvedRefund : DisputeStatus.ResolvedPayout;
                dispute.AdminNote = dto.AdminNote;
                dispute.ResolvedByUserId = adminUserId;
                dispute.ResolvedAt = now;

                // Invoice → Resolved
                if (dispute.Invoice != null)
                {
                    dispute.Invoice.Status = InvoiceStatus.Resolved;
                    dispute.Invoice.UpdatedAt = now;
                    _invoiceRepo.Update(dispute.Invoice);
                }

                // Booking → Completed
                dispute.Booking.Status = BookingStatus.Completed;
                dispute.Booking.UpdatedAt = now;
                _bookingRepo.Update(dispute.Booking);

                _disputeRepo.Update(dispute);
                await _unitOfWork.CompleteAsync();

                // ── WALLET SETTLEMENT ──
                if (dto.IsDriverWin)
                {
                    await RefundToDriverAsync(dispute.Booking, dispute);
                }
                else
                {
                    if (dispute.Invoice != null)
                    {
                        await SettleToOwnerAsync(dispute.Booking, dispute.Invoice, dispute);
                    }
                }

                // ── BANNING RULES (Hardcode: lần 1 → 30 ngày, lần 2 → vĩnh viễn) ──
                var startOfMonth = new DateTime(now.Year, now.Month, 1);
                
                if (!dto.IsDriverWin)
                {
                    // Driver thua
                    var driverUserIdLocal = dispute.CreatedByUserId;
                    var driverLoseCount = await _disputeRepo.GetDriverLoseCountInMonthAsync(driverUserIdLocal, startOfMonth);
                    
                    const int driverBanThreshold = 3;
                    var driverRemaining = driverBanThreshold - driverLoseCount;
                    
                    if (driverLoseCount >= driverBanThreshold)
                    {
                        var driverUser = dispute.Booking.Driver!.User;
                        
                        // Đảm bảo không phạt dồn (tránh trường hợp Admin xử lý 4 khiếu nại liên tiếp cùng lúc khiến BanCount tăng vọt lên 2)
                        // Chỉ phạt khi User đang ở trạng thái tự do (không bị cấm)
                        if (driverUser.Status != Constants.UserStatusConstants.Banned && driverUser.BannedUntil == null)
                        {
                            driverUser.BanCount += 1;
                            driverUser.Status = Constants.UserStatusConstants.Suspended;
                            driverUser.BannedUntil = now.AddDays(30);
                            await _notificationService.SendAsync(driverUserIdLocal, "Tài khoản bị đình chỉ", "Tài khoản bị đình chỉ 30 ngày do vi phạm chính sách khiếu nại (thua quá 3 lần/tháng).", NotificationType.System);
                            
                            await _userManager.UpdateAsync(driverUser);

                            await CancelDriverBookingsAsync(driverUserIdLocal, "Tài xế bị hệ thống khóa tài khoản.");
                        }
                    }
                    else if (driverRemaining > 0)
                    {
                        // Cảnh cáo: chưa đạt ngưỡng ban nhưng đang tiến gần
                        await _notificationService.SendAsync(
                            driverUserIdLocal,
                            "Cảnh cáo vi phạm khiếu nại",
                            $"Bạn đã thua {driverLoseCount}/{driverBanThreshold} lượt khiếu nại trong tháng này. Còn {driverRemaining} lượt nữa tài khoản sẽ bị đình chỉ.",
                            NotificationType.System);
                    }
                }
                else
                {
                    // Station thua
                    var stationId = dispute.Booking.ChargingSlot?.StationId ?? 0;
                    var stationLoseCount = await _disputeRepo.GetStationLoseCountInMonthAsync(stationId, startOfMonth);
                    
                    const int stationBanThreshold = 5;
                    var stationRemaining = stationBanThreshold - stationLoseCount;
                                      
                    if (stationLoseCount >= stationBanThreshold)
                    {
                        var station = dispute.Booking.ChargingSlot?.ChargingStation;
                        
                        // Đảm bảo không phạt dồn lặp lại nếu trạm đang trong thời gian phạt
                        if (station != null && station.BannedUntil == null)
                        {
                            station.BanCount += 1;
                            station.OperationalStatus = OperationalStatus.Inactive;
                            station.BannedUntil = now.AddDays(30);
                            await _notificationService.SendAsync(station.OwnerUserId, "Trạm sạc bị đình chỉ", $"Trạm {station.Name} bị đình chỉ 30 ngày do lượng khiếu nại quá cao (>= 5 lần/tháng).", NotificationType.System);
                            
                            _stationRepo.Update(station);
                            await _unitOfWork.CompleteAsync();
                            
                            await CancelStationBookingsAsync(stationId, "Trạm sạc bị hệ thống đình chỉ do vi phạm chất lượng.");
                        }
                    }
                    else if (stationRemaining > 0)
                    {
                        var ownerIdForWarning = dispute.Booking.ChargingSlot?.ChargingStation?.OwnerUserId ?? 0;
                        var stationNameForWarning = dispute.Booking.ChargingSlot?.ChargingStation?.Name ?? "Trạm";
                        await _notificationService.SendAsync(
                            ownerIdForWarning,
                            "Cảnh cáo chất lượng trạm sạc",
                            $"Trạm {stationNameForWarning} đã thua {stationLoseCount}/{stationBanThreshold} lượt khiếu nại trong tháng này. Còn {stationRemaining} lượt nữa trạm sẽ bị đình chỉ.",
                            NotificationType.System);
                    }
                }

                await transaction.CommitAsync();

                // Notifications (ngoài transaction)
                var stationName = dispute.Booking.ChargingSlot?.ChargingStation?.Name ?? "";
                var driverName = dispute.Booking.Driver?.User?.FullName ?? "Driver";

                await _notificationService.SendAsync(
                    dispute.Booking.DriverUserId,
                    "Kết quả khiếu nại",
                    dto.IsDriverWin
                        ? $"Khiếu nại của bạn tại trạm {stationName} đã được chấp nhận. {dispute.Booking.TotalAmount:N0}đ đã hoàn vào ví. {dto.AdminNote}"
                        : $"Khiếu nại của bạn tại trạm {stationName} không được chấp nhận. Tiền đã thanh toán cho chủ trạm. {dto.AdminNote}",
                    NotificationType.Dispute);

                var ownerUserId = dispute.Booking.ChargingSlot?.ChargingStation?.OwnerUserId ?? 0;
                await _notificationService.SendAsync(
                    ownerUserId,
                    "Kết quả khiếu nại",
                    dto.IsDriverWin
                        ? $"Khiếu nại từ {driverName} tại trạm {stationName}: Driver được hoàn tiền. {dto.AdminNote}"
                        : $"Khiếu nại từ {driverName} tại trạm {stationName}: Bạn được thanh toán. {dispute.Invoice?.ChargingAmount:N0}đ đã chuyển vào ví. {dto.AdminNote}",
                    NotificationType.Dispute);

                return MapToDto(dispute);
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        /// <summary>
        /// ESCROW → Driver: hoàn toàn bộ TotalAmount.
        /// </summary>
        private async Task RefundToDriverAsync(Booking booking, Dispute dispute)
        {
            var now = DateTimeHelper.VietnamNow();
            var escrowWallet = await _walletRepo.GetBySystemCodeAsync("ESCROW");

            // Get or create Driver wallet
            var driverWallet = await _walletRepo.GetByUserIdAsync(booking.DriverUserId);
            if (driverWallet == null)
            {
                driverWallet = new Wallet
                {
                    UserId = booking.DriverUserId,
                    WalletType = WalletType.Driver,
                    AvailableBalance = 0,
                    FrozenBalance = 0,
                    CreatedAt = now
                };
                _walletRepo.Add(driverWallet);
                await _unitOfWork.CompleteAsync();
            }

            var refundAmount = booking.TotalAmount;

            // Unfreeze from ESCROW.FrozenBalance → refund to Driver (Atomic via Repository)
            await _walletRepo.AdjustBalanceAtomicAsync(escrowWallet!.Id, 0, -refundAmount);
            await _walletRepo.AdjustBalanceAtomicAsync(driverWallet.Id, refundAmount, 0);

            var ledger = new LedgerTransaction
            {
                ReferenceType = "Refund",
                ReferenceId = booking.Id,
                Memo = $"Hoàn tiền booking #{booking.Id} (Dispute #{dispute.Id}) - {refundAmount:N0}đ → Driver",
                CreatedByUserId = null,
                CreatedAt = now,
                Entries = new List<LedgerEntry>
                {
                    new LedgerEntry { WalletId = escrowWallet.Id, Direction = LedgerDirection.Debit, Amount = refundAmount, CreatedAt = now },
                    new LedgerEntry { WalletId = driverWallet.Id, Direction = LedgerDirection.Credit, Amount = refundAmount, CreatedAt = now }
                }
            };
            _ledgerRepo.Add(ledger);

            // Refund Loyalty Points if applicable
            if (booking.PointsUsed > 0)
            {
                var driver = await _lazyDriverRepo.Value.GetByUserIdAsync(booking.DriverUserId, tracking: true);
                if (driver != null)
                {
                    driver.LoyaltyPoints += booking.PointsUsed;
                    _lazyLoyaltyRepo.Value.Add(new LoyaltyTransaction
                    {
                        DriverUserId = booking.DriverUserId,
                        BookingId = booking.Id,
                        Type = "Refund",
                        Points = booking.PointsUsed,
                        Description = $"Hoàn {booking.PointsUsed:N0} điểm (thắng khiếu nại #{dispute.Id})",
                        CreatedAt = now
                    });
                }
            }

            await _unitOfWork.CompleteAsync();
        }

        /// <summary>
        /// ESCROW → Owner (net) + PLATFORM_REVENUE (fee). Owner wins dispute.
        /// </summary>
        private async Task SettleToOwnerAsync(Booking booking, Invoice invoice, Dispute dispute)
        {
            var ownerUserId = booking.ChargingSlot!.ChargingStation!.OwnerUserId;
            var now = DateTimeHelper.VietnamNow();

            var escrowWallet = await _walletRepo.GetBySystemCodeAsync("ESCROW");
            var platformWallet = await _walletRepo.GetBySystemCodeAsync("PLATFORM_REVENUE");
            var taxWallet = await _walletRepo.GetBySystemCodeAsync("TAX_HOLD");

            var ownerWallet = await _walletRepo.GetByUserIdAsync(ownerUserId);
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
                _walletRepo.Add(ownerWallet);
                await _unitOfWork.CompleteAsync();
            }

            var ownerNet = invoice.ChargingAmount;
            var platformFee = invoice.PlatformFee;
            var vatAmount = invoice.VatAmount;
            var totalDeduct = ownerNet + platformFee + vatAmount;

            // 0. Bù tiền bảo trợ Điểm thưởng vào ESCROW trước khi settle (Atomic via Repository)
            if (booking.PointsDiscountAmount > 0)
            {
                await _walletRepo.TransferAtomicAsync(platformWallet!.Id, escrowWallet!.Id, booking.PointsDiscountAmount);

                _ledgerRepo.Add(new LedgerTransaction
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
                await _unitOfWork.CompleteAsync();
            }

            // 1. Unfreeze ALL back to ESCROW.AvailableBalance (Atomic via Repository)
            await _walletRepo.UnfreezeAtomicAsync(escrowWallet!.Id, totalDeduct);

            // 2. ESCROW → Owner (Atomic via Repository)
            await _walletRepo.TransferAtomicAsync(escrowWallet.Id, ownerWallet.Id, ownerNet);

            _ledgerRepo.Add(new LedgerTransaction
            {
                ReferenceType = "DisputeSettlement",
                ReferenceId = booking.Id,
                Memo = $"Dispute #{dispute.Id} - Owner thắng - Nhận {ownerNet:N0}đ (Station: {booking.ChargingSlot.ChargingStation.Name})",
                CreatedAt = now,
                Entries = new List<LedgerEntry>
                {
                    new LedgerEntry { WalletId = escrowWallet.Id, Direction = LedgerDirection.Debit, Amount = ownerNet, CreatedAt = now },
                    new LedgerEntry { WalletId = ownerWallet.Id, Direction = LedgerDirection.Credit, Amount = ownerNet, CreatedAt = now }
                }
            });

            // 3. ESCROW → PLATFORM_REVENUE (Atomic via Repository)
            await _walletRepo.TransferAtomicAsync(escrowWallet.Id, platformWallet!.Id, platformFee);

            _ledgerRepo.Add(new LedgerTransaction
            {
                ReferenceType = "PlatformFee",
                ReferenceId = booking.Id,
                Memo = $"Phí nền tảng Dispute #{dispute.Id} - {platformFee:N0}đ (Station: {booking.ChargingSlot.ChargingStation.Name})",
                CreatedAt = now,
                Entries = new List<LedgerEntry>
                {
                    new LedgerEntry { WalletId = escrowWallet.Id, Direction = LedgerDirection.Debit, Amount = platformFee, CreatedAt = now },
                    new LedgerEntry { WalletId = platformWallet.Id, Direction = LedgerDirection.Credit, Amount = platformFee, CreatedAt = now }
                }
            });

            // 4. ESCROW → TAX_HOLD (Atomic via Repository)
            if (vatAmount > 0)
            {
                await _walletRepo.TransferAtomicAsync(escrowWallet.Id, taxWallet!.Id, vatAmount);

                _ledgerRepo.Add(new LedgerTransaction
                {
                    ReferenceType = "TaxHold",
                    ReferenceId = booking.Id,
                    Memo = $"Thuế GTGT Dispute #{dispute.Id} - {vatAmount:N0}đ",
                    CreatedAt = now,
                    Entries = new List<LedgerEntry>
                    {
                        new LedgerEntry { WalletId = escrowWallet.Id, Direction = LedgerDirection.Debit, Amount = vatAmount, CreatedAt = now },
                        new LedgerEntry { WalletId = taxWallet.Id, Direction = LedgerDirection.Credit, Amount = vatAmount, CreatedAt = now }
                    }
                });
            }

            await _unitOfWork.CompleteAsync();
        }

        public async Task<DisputeDto?> GetByIdAsync(int disputeId, int currentUserId, string currentUserRole)
        {
            var dispute = await LoadDisputeWithDetailsAsync(disputeId);
            if (dispute == null) return null;

            if (currentUserRole == Constants.RoleConstants.Driver && dispute.CreatedByUserId != currentUserId)
                throw new UnauthorizedAccessException("Bạn không có quyền xem khiếu nại này.");
            
            if (currentUserRole == Constants.RoleConstants.Owner && dispute.Booking.ChargingSlot?.ChargingStation?.OwnerUserId != currentUserId)
                throw new UnauthorizedAccessException("Bạn không có quyền xem khiếu nại này.");

            return MapToDto(dispute);
        }

        public async Task<DisputeDto?> GetByBookingIdAsync(int bookingId, int currentUserId, string currentUserRole)
        {
            var dispute = await _disputeRepo.GetByBookingIdAsync(bookingId);
            
            if (dispute == null) return null;

            if (currentUserRole == Constants.RoleConstants.Driver && dispute.CreatedByUserId != currentUserId)
                throw new UnauthorizedAccessException("Bạn không có quyền xem khiếu nại này.");
            
            if (currentUserRole == Constants.RoleConstants.Owner && dispute.Booking.ChargingSlot?.ChargingStation?.OwnerUserId != currentUserId)
                throw new UnauthorizedAccessException("Bạn không có quyền xem khiếu nại này.");

            return MapToDto(dispute);
        }

        public async Task<List<DisputeDto>> GetPendingAsync()
        {
            var disputes = await _disputeRepo.GetPendingAsync();
            return disputes.Select(MapToDto).ToList();
        }

        public async Task<ChargeSlot.Api.DTOs.PagedResultDto<DisputeDto>> GetPendingPagedAsync(int page, int pageSize)
        {
            var result = await _disputeRepo.GetPendingPagedAsync(page, pageSize);
            return new ChargeSlot.Api.DTOs.PagedResultDto<DisputeDto>
            {
                Page = page,
                PageSize = pageSize,
                TotalItems = result.TotalCount,
                Items = result.Items.Select(MapToDto).ToList()
            };
        }

        public async Task<List<DisputeDto>> GetAllAsync(string? status = null)
        {
            var disputes = await _disputeRepo.GetAllAsync(status);

            return disputes.Select(MapToDto).ToList();
        }

        public async Task<ChargeSlot.Api.DTOs.PagedResultDto<DisputeDto>> GetAllPagedAsync(string? status, int page, int pageSize)
        {
            var result = await _disputeRepo.GetAllPagedAsync(status, page, pageSize);
            return new ChargeSlot.Api.DTOs.PagedResultDto<DisputeDto>
            {
                Page = page,
                PageSize = pageSize,
                TotalItems = result.TotalCount,
                Items = result.Items.Select(MapToDto).ToList()
            };
        }

        public async Task<List<DisputeDto>> GetMyDisputesAsync(int driverUserId)
        {
            var disputes = await _disputeRepo.GetByDriverAsync(driverUserId);
            return disputes.Select(MapToDto).ToList();
        }

        public async Task<ChargeSlot.Api.DTOs.PagedResultDto<DisputeDto>> GetMyDisputesPagedAsync(int driverUserId, int page, int pageSize)
        {
            var result = await _disputeRepo.GetByDriverPagedAsync(driverUserId, page, pageSize);
            return new ChargeSlot.Api.DTOs.PagedResultDto<DisputeDto>
            {
                Page = page,
                PageSize = pageSize,
                TotalItems = result.TotalCount,
                Items = result.Items.Select(MapToDto).ToList()
            };
        }

        public async Task<List<DisputeDto>> GetOwnerDisputesAsync(int ownerUserId)
        {
            var disputes = await _disputeRepo.GetByOwnerAsync(ownerUserId);
            return disputes.Select(MapToDto).ToList();
        }

        public async Task<ChargeSlot.Api.DTOs.PagedResultDto<DisputeDto>> GetOwnerDisputesPagedAsync(int ownerUserId, int page, int pageSize)
        {
            var result = await _disputeRepo.GetByOwnerPagedAsync(ownerUserId, page, pageSize);
            return new ChargeSlot.Api.DTOs.PagedResultDto<DisputeDto>
            {
                Page = page,
                PageSize = pageSize,
                TotalItems = result.TotalCount,
                Items = result.Items.Select(MapToDto).ToList()
            };
        }

        // ─────────────── HELPERS ───────────────

        /// <summary>
        /// Upload files bằng chứng lên Firebase Storage và tạo DisputeEvidence records.
        /// </summary>
        private async Task SaveEvidenceFilesAsync(Dispute dispute, IFormFile[] files, int uploadedByUserId)
        {
            foreach (var file in files)
            {
                if (file.Length <= 0) continue;

                // Validate file size
                if (file.Length > MaxFileSizeBytes)
                    throw new InvalidOperationException($"File '{file.FileName}' vượt quá giới hạn {MaxFileSizeBytes / 1024 / 1024}MB.");

                var ext = Path.GetExtension(file.FileName).ToLowerInvariant();

                // Validate file extension
                if (!AllowedFileExtensions.Contains(ext))
                    throw new InvalidOperationException($"Loại file '{ext}' không được cho phép. Chỉ chấp nhận: {string.Join(", ", AllowedFileExtensions)}");

                // Upload lên Firebase Storage
                var publicUrl = await _fileStorageService.UploadAsync(file, $"disputes/{dispute.Id}");

                // Detect file type from extension
                var fileType = ext switch
                {
                    ".jpg" or ".jpeg" or ".png" or ".gif" or ".webp" or ".bmp" => "image",
                    ".mp4" or ".avi" or ".mov" or ".webm" => "video",
                    _ => "document"
                };

                dispute.Evidences.Add(new DisputeEvidence
                {
                    DisputeId = dispute.Id,
                    UploadedByUserId = uploadedByUserId,
                    FileUrl = publicUrl,
                    FileType = fileType,
                    CreatedAt = DateTimeHelper.VietnamNow()
                });
            }

            await _unitOfWork.CompleteAsync();
        }

        private async Task CancelDriverBookingsAsync(int driverUserId, string reason)
        {
            var targetStatuses = new[] { 
                BookingStatus.WaitingOwner, 
                BookingStatus.PendingPayment, 
                BookingStatus.Paid 
            };
            
            var bookings = await _bookingRepo.GetActiveBookingsByDriverAsync(driverUserId, targetStatuses);
                
            var bookingService = _lazyBookingService.Value;
            foreach (var b in bookings)
            {
                await bookingService.CancelSystemBookingAsync(b.Id, reason);
                
                var ownerId = b.ChargingSlot?.ChargingStation?.OwnerUserId;
                if (ownerId.HasValue)
                {
                    await _notificationService.SendAsync(
                        ownerId.Value, 
                        "Lịch đặt đã bị hủy", 
                        $"Tài xế đặt slot {b.ChargingSlot?.SlotName} tại trạm {b.ChargingSlot?.ChargingStation?.Name} vừa bị khóa tài khoản theo chính sách vi phạm. Lịch đã tự động hủy.", 
                        NotificationType.System);
                }
            }
        }

        private async Task CancelStationBookingsAsync(int stationId, string reason)
        {
            var targetStatuses = new[] { 
                BookingStatus.WaitingOwner, 
                BookingStatus.PendingPayment, 
                BookingStatus.Paid 
            };

            var bookings = await _bookingRepo.GetActiveBookingsByStationIdsAsync(new List<int> { stationId }, targetStatuses);
                
            var bookingService = _lazyBookingService.Value;
            foreach (var b in bookings)
            {
                await bookingService.CancelSystemBookingAsync(b.Id, reason);
                
                await _notificationService.SendAsync(
                    b.DriverUserId, 
                    "Lịch đặt đã bị hủy", 
                    $"Trạm sạc {b.ChargingSlot?.ChargingStation?.Name} do vi phạm nên đã bị hệ thống khóa. Lịch đặt của bạn tại slot {b.ChargingSlot?.SlotName} bị hủy, tiền cọc (nếu có) đã hoàn 100% vào ví.", 
                    NotificationType.System);
            }
        }

        private async Task<Dispute?> LoadDisputeWithDetailsAsync(int id)
        {
            return await _disputeRepo.GetByIdWithDetailsAsync(id);
        }

        private static DisputeDto MapToDto(Dispute d)
        {
            return new DisputeDto
            {
                Id = d.Id,
                BookingId = d.BookingId,
                InvoiceId = d.InvoiceId,
                CreatedByUserId = d.CreatedByUserId,
                CreatedByName = d.CreatedByUser?.FullName ?? "",
                Reason = d.Reason,
                Description = d.Description,
                Status = d.Status.ToString(),
                OwnerResponse = d.OwnerResponse,
                AdminNote = d.AdminNote,
                ResolvedByUserId = d.ResolvedByUserId,
                ResolvedAt = d.ResolvedAt,
                CreatedAt = d.CreatedAt,
                Evidences = d.Evidences.Select(e => new DisputeEvidenceDto
                {
                    Id = e.Id,
                    UploadedByUserId = e.UploadedByUserId,
                    UploadedByName = e.UploadedByUser?.FullName ?? "",
                    FileUrl = e.FileUrl,
                    FileType = e.FileType,
                    CreatedAt = e.CreatedAt
                }).ToList()
            };
        }

        // ─────────────── STRIKE STATUS ───────────────

        public async Task<DisputeStrikeStatusDto> GetDriverStrikeStatusAsync(int driverUserId)
        {
            var now = DateTimeHelper.VietnamNow();
            var startOfMonth = new DateTime(now.Year, now.Month, 1);
            const int threshold = 3;

            var loseCount = await _disputeRepo.GetDriverLoseCountInMonthAsync(driverUserId, startOfMonth);

            // Lấy user info cho ban status
            var user = await _userManager.FindByIdAsync(driverUserId.ToString());

            return new DisputeStrikeStatusDto
            {
                LoseCountThisMonth = loseCount,
                BanThreshold = threshold,
                RemainingBeforeBan = Math.Max(0, threshold - loseCount),
                BanCount = user?.BanCount ?? 0,
                IsBanned = user?.Status == Constants.UserStatusConstants.Banned || user?.Status == Constants.UserStatusConstants.Suspended,
                BannedUntil = user?.BannedUntil
            };
        }

        public async Task<DisputeStrikeStatusDto> GetStationStrikeStatusAsync(int stationId, int ownerUserId)
        {
            var station = await _stationRepo.GetByIdAsync(stationId, includeDetails: false)
                ?? throw new KeyNotFoundException($"Station {stationId} not found.");

            if (station.OwnerUserId != ownerUserId)
                throw new UnauthorizedAccessException("Bạn không có quyền xem thông tin trạm này.");

            var now = DateTimeHelper.VietnamNow();
            var startOfMonth = new DateTime(now.Year, now.Month, 1);
            const int threshold = 5;

            var loseCount = await _disputeRepo.GetStationLoseCountInMonthAsync(stationId, startOfMonth);

            return new DisputeStrikeStatusDto
            {
                LoseCountThisMonth = loseCount,
                BanThreshold = threshold,
                RemainingBeforeBan = Math.Max(0, threshold - loseCount),
                BanCount = station.BanCount,
                IsBanned = station.BannedUntil.HasValue,
                BannedUntil = station.BannedUntil
            };
        }
    }
}

