using ChargeSlot.Api.DTOs.Dispute;
using ChargeSlot.Api.Enums;
using ChargeSlot.Api.Models;
using ChargeSlot.Api.Models.Identity;
using ChargeSlot.Api.Repositories.Interfaces;
using ChargeSlot.Api.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

using ChargeSlot.Api.Helpers;
namespace ChargeSlot.Api.Services.Implementation
{
    public class DisputeService : IDisputeService
    {
        private readonly INotificationService _notificationService;
        private readonly Data.ChargeSlotDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IFileStorageService _fileStorageService;
        private readonly Lazy<IBookingService> _lazyBookingService;
        private readonly Lazy<ISystemConfigService> _lazyConfigService;

        private static readonly string[] AllowedFileExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp", ".mp4", ".avi", ".mov", ".webm", ".pdf" };
        private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10MB

        public DisputeService(
            INotificationService notificationService,
            Data.ChargeSlotDbContext db,
            UserManager<ApplicationUser> userManager,
            IFileStorageService fileStorageService,
            IServiceProvider serviceProvider)
        {
            _notificationService = notificationService;
            _db = db;
            _userManager = userManager;
            _fileStorageService = fileStorageService;
            _lazyBookingService = new Lazy<IBookingService>(() => serviceProvider.GetRequiredService<IBookingService>());
            _lazyConfigService = new Lazy<ISystemConfigService>(() => serviceProvider.GetRequiredService<ISystemConfigService>());
        }

        /// <summary>
        /// Driver submits dispute: validate booking = CompletedPendingInvoice →
        /// create Dispute + evidence → freeze payment (invoice = UnderDispute) →
        /// booking = Disputed → notify Owner + Admin.
        /// </summary>
        public async Task<DisputeDto> SubmitDisputeAsync(int driverUserId, CreateDisputeDto dto)
        {
            using var transaction = await _db.Database.BeginTransactionAsync();
            try
            {
                var booking = await _db.Bookings
                    .Include(b => b.Driver).ThenInclude(d => d.User)
                    .Include(b => b.ChargingSlot).ThenInclude(s => s.ChargingStation)
                    .FirstOrDefaultAsync(b => b.Id == dto.BookingId)
                    ?? throw new InvalidOperationException("Booking không tồn tại.");

                if (booking.DriverUserId != driverUserId)
                    throw new InvalidOperationException("Booking này không thuộc về bạn.");

                if (booking.Status != BookingStatus.CompletedPendingInvoice)
                    throw new InvalidOperationException("Chỉ có thể khiếu nại khi booking đang chờ xác nhận hóa đơn.");

                // Check if dispute already exists
                var existing = await _db.Disputes.AnyAsync(d => d.BookingId == dto.BookingId);
                if (existing)
                    throw new InvalidOperationException("Đã có khiếu nại cho booking này.");

                // Rate limit: max 3 disputes/driver/tháng
                var monthStart = new DateTime(DateTimeHelper.VietnamNow().Year, DateTimeHelper.VietnamNow().Month, 1);
                var disputeCountThisMonth = await _db.Disputes
                    .CountAsync(d => d.CreatedByUserId == driverUserId && d.CreatedAt >= monthStart);
                if (disputeCountThisMonth >= 3)
                    throw new InvalidOperationException("Bạn đã đạt giới hạn 3 khiếu nại/tháng. Vui lòng liên hệ hotline nếu cần hỗ trợ thêm.");

                // Get invoice
                var invoice = await _db.Invoices.FirstOrDefaultAsync(i => i.BookingId == dto.BookingId);

                // Create dispute
                var configs = await _lazyConfigService.Value.GetCurrentConfigsAsync();
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

                _db.Disputes.Add(dispute);

                // Freeze payment: invoice → UnderDispute
                if (invoice != null)
                {
                    invoice.Status = InvoiceStatus.UnderDispute;
                    invoice.UpdatedAt = DateTimeHelper.VietnamNow();
                }

                // Booking → Disputed
                booking.Status = BookingStatus.Disputed;
                booking.UpdatedAt = DateTimeHelper.VietnamNow();

                // Freeze ESCROW balance: AvailableBalance → FrozenBalance
                var escrowWallet = await _db.Wallets.FirstAsync(w => w.SystemCode == "ESCROW");
                escrowWallet.AvailableBalance -= booking.TotalAmount;
                escrowWallet.FrozenBalance += booking.TotalAmount;

                await _db.SaveChangesAsync();
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
                    $"{booking.Driver?.User?.FullName ?? "Driver"} khiếu nại về phiên sạc tại trạm {booking.ChargingSlot?.ChargingStation?.Name}. Lý do: {dto.Reason}. Bạn có 24h để nộp bằng chứng phản hồi.",
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
            var dispute = await _db.Disputes
                .Include(d => d.Booking)
                    .ThenInclude(b => b.ChargingSlot).ThenInclude(s => s.ChargingStation)
                .Include(d => d.Evidences)
                .FirstOrDefaultAsync(d => d.Id == disputeId)
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

            await _db.SaveChangesAsync();

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
            using var transaction = await _db.Database.BeginTransactionAsync();
            try
            {
                var dispute = await _db.Disputes
                    .Include(d => d.Booking)
                        .ThenInclude(b => b.ChargingSlot).ThenInclude(s => s.ChargingStation)
                    .Include(d => d.Booking)
                        .ThenInclude(b => b.Driver).ThenInclude(dr => dr.User)
                    .Include(d => d.Invoice)
                    .Include(d => d.CreatedByUser)
                    .Include(d => d.Evidences)
                    .FirstOrDefaultAsync(d => d.Id == disputeId)
                    ?? throw new InvalidOperationException("Khiếu nại không tồn tại.");

                if (dispute.Status != DisputeStatus.PendingReview && dispute.Status != DisputeStatus.WaitingOwnerEvidence)
                    throw new InvalidOperationException("Khiếu nại không ở trạng thái có thể xử lý.");

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
                }

                // Booking → Completed
                dispute.Booking.Status = BookingStatus.Completed;
                dispute.Booking.UpdatedAt = now;

                await _db.SaveChangesAsync();

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

                // ── CHECK BANNING RULES (Lũy tiến: lần 1 -> 30 ngày, lần 2 -> vĩnh viễn) ──
                var startOfMonth = new DateTime(now.Year, now.Month, 1);
                
                if (!dto.IsDriverWin)
                {
                    // Driver thua
                    var driverUserIdLocal = dispute.CreatedByUserId;
                    var driverLoseCount = await _db.Disputes
                        .CountAsync(d => d.CreatedByUserId == driverUserIdLocal
                                      && d.ResolvedAt >= startOfMonth
                                      && d.Status == DisputeStatus.ResolvedPayout); // ResolvedPayout = Owner win
                                      
                    if (driverLoseCount >= 3)
                    {
                        var driverUser = dispute.Booking.Driver!.User;
                        
                        // Đảm bảo không phạt dồn (tránh trường hợp Admin xử lý 4 khiếu nại liên tiếp cùng lúc khiến BanCount tăng vọt lên 2)
                        // Chỉ phạt khi User đang ở trạng thái tự do (không bị cấm)
                        if (driverUser.Status != Constants.UserStatusConstants.Banned && driverUser.BannedUntil == null)
                        {
                            driverUser.BanCount += 1;
                            if (driverUser.BanCount == 1)
                            {
                                driverUser.Status = Constants.UserStatusConstants.Suspended;
                                driverUser.BannedUntil = now.AddDays(30);
                                await _notificationService.SendAsync(driverUserIdLocal, "Tài khoản bị đình chỉ", "Tài khoản bị đình chỉ 30 ngày do vi phạm chính sách khiếu nại (thua quá 3 lần/tháng).", NotificationType.System);
                            }
                            else
                            {
                                driverUser.Status = Constants.UserStatusConstants.Banned;
                                driverUser.BannedUntil = null;
                                await _notificationService.SendAsync(driverUserIdLocal, "Tài khoản bị khóa vĩnh viễn", "Tài khoản bị khóa vĩnh viễn do lạm dụng bộ phận CSKH nhiều lần.", NotificationType.System);
                            }
                            _db.Users.Update(driverUser);
                            await _db.SaveChangesAsync();

                            await CancelDriverBookingsAsync(driverUserIdLocal, "Tài xế bị hệ thống khóa tài khoản.");
                        }
                    }
                }
                else
                {
                    // Station thua
                    var stationId = dispute.Booking.ChargingSlot?.StationId ?? 0;
                    var stationLoseCount = await _db.Disputes
                        .CountAsync(d => d.Booking.ChargingSlot.StationId == stationId
                                      && d.ResolvedAt >= startOfMonth
                                      && d.Status == DisputeStatus.ResolvedRefund); // ResolvedRefund = Driver win
                                      
                    if (stationLoseCount >= 5)
                    {
                        var station = dispute.Booking.ChargingSlot?.ChargingStation;
                        
                        // Đảm bảo không phạt dồn lặp lại nếu trạm đang trong thời gian phạt hoặc đã bị cấm vĩnh viễn
                        if (station != null && station.BannedUntil == null && station.BanCount < 2)
                        {
                            station.BanCount += 1;
                            if (station.BanCount == 1)
                            {
                                station.OperationalStatus = OperationalStatus.Inactive;
                                station.BannedUntil = now.AddDays(30);
                                await _notificationService.SendAsync(station.OwnerUserId, "Trạm sạc bị đình chỉ", $"Trạm {station.Name} bị đình chỉ 30 ngày do lượng khiếu nại quá cao (>= 5 lần/tháng).", NotificationType.System);
                            }
                            else
                            {
                                station.OperationalStatus = OperationalStatus.Inactive;
                                station.BannedUntil = null;
                                await _notificationService.SendAsync(station.OwnerUserId, "Trạm sạc bị khóa vĩnh viễn", $"Trạm {station.Name} bị khóa vĩnh viễn do chất lượng dịch vụ không đạt yêu cầu tái phạm.", NotificationType.System);
                            }
                            _db.ChargingStations.Update(station);
                            await _db.SaveChangesAsync(); // Auto commit

                            await CancelStationBookingsAsync(stationId, "Trạm sạc bị hệ thống đình chỉ do vi phạm chất lượng.");
                        }
                    }
                }

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
            var escrowWallet = await _db.Wallets.FirstAsync(w => w.SystemCode == "ESCROW");

            // Get or create Driver wallet
            var driverWallet = await _db.Wallets.FirstOrDefaultAsync(w => w.UserId == booking.DriverUserId);
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
                _db.Wallets.Add(driverWallet);
                await _db.SaveChangesAsync();
            }

            var refundAmount = booking.TotalAmount;

            // Unfreeze from ESCROW.FrozenBalance (was frozen when dispute submitted)
            escrowWallet.FrozenBalance -= refundAmount;
            driverWallet.AvailableBalance += refundAmount;

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
            _db.Set<LedgerTransaction>().Add(ledger);
            await _db.SaveChangesAsync();
        }

        /// <summary>
        /// ESCROW → Owner (net) + PLATFORM_REVENUE (fee). Owner wins dispute.
        /// </summary>
        private async Task SettleToOwnerAsync(Booking booking, Invoice invoice, Dispute dispute)
        {
            var ownerUserId = booking.ChargingSlot!.ChargingStation!.OwnerUserId;
            var now = DateTimeHelper.VietnamNow();

            var escrowWallet = await _db.Wallets.FirstAsync(w => w.SystemCode == "ESCROW");
            var platformWallet = await _db.Wallets.FirstAsync(w => w.SystemCode == "PLATFORM_REVENUE");

            var ownerWallet = await _db.Wallets.FirstOrDefaultAsync(w => w.UserId == ownerUserId);
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
                _db.Wallets.Add(ownerWallet);
                await _db.SaveChangesAsync();
            }

            var ownerNet = invoice.ChargingAmount;
            var platformFee = invoice.PlatformFee;
            var vatAmount = invoice.VatAmount;

            // Unfreeze from ESCROW.FrozenBalance → distribute
            escrowWallet.FrozenBalance -= (ownerNet + platformFee + vatAmount);
            ownerWallet.AvailableBalance += ownerNet;

            _db.Set<LedgerTransaction>().Add(new LedgerTransaction
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

            // ESCROW → PLATFORM_REVENUE (already unfrozen above)
            platformWallet.AvailableBalance += platformFee;
            // VAT stays as revenue in ESCROW for tax authority payment later

            _db.Set<LedgerTransaction>().Add(new LedgerTransaction
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

            await _db.SaveChangesAsync();
        }

        public async Task<DisputeDto?> GetByIdAsync(int disputeId)
        {
            var dispute = await LoadDisputeWithDetailsAsync(disputeId);
            return dispute == null ? null : MapToDto(dispute);
        }

        public async Task<DisputeDto?> GetByBookingIdAsync(int bookingId)
        {
            var dispute = await _db.Disputes
                .Include(d => d.Evidences)
                .Include(d => d.CreatedByUser)
                .FirstOrDefaultAsync(d => d.BookingId == bookingId);
            return dispute == null ? null : MapToDto(dispute);
        }

        public async Task<List<DisputeDto>> GetPendingAsync()
        {
            var disputes = await _db.Disputes
                .Include(d => d.Booking)
                    .ThenInclude(b => b.ChargingSlot).ThenInclude(s => s.ChargingStation)
                .Include(d => d.Booking)
                    .ThenInclude(b => b.Driver).ThenInclude(dr => dr.User)
                .Include(d => d.Invoice)
                .Include(d => d.CreatedByUser)
                .Include(d => d.Evidences)
                .Where(d => d.Status == DisputeStatus.WaitingOwnerEvidence
                    || d.Status == DisputeStatus.PendingReview)
                .OrderBy(d => d.CreatedAt)
                .ToListAsync();
            return disputes.Select(MapToDto).ToList();
        }

        public async Task<List<DisputeDto>> GetAllAsync(string? status = null)
        {
            var query = _db.Disputes
                .Include(d => d.Booking)
                    .ThenInclude(b => b.ChargingSlot).ThenInclude(s => s.ChargingStation)
                .Include(d => d.Booking)
                    .ThenInclude(b => b.Driver).ThenInclude(dr => dr.User)
                .Include(d => d.Invoice)
                .Include(d => d.CreatedByUser)
                .Include(d => d.Evidences)
                    .ThenInclude(e => e.UploadedByUser)
                .AsQueryable();

            if (!string.IsNullOrEmpty(status) && Enum.TryParse<DisputeStatus>(status, true, out var parsed))
            {
                query = query.Where(d => d.Status == parsed);
            }

            var disputes = await query
                .OrderByDescending(d => d.CreatedAt)
                .ToListAsync();

            return disputes.Select(MapToDto).ToList();
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

            await _db.SaveChangesAsync();
        }

        private async Task CancelDriverBookingsAsync(int driverUserId, string reason)
        {
            var targetStatuses = new[] { 
                BookingStatus.WaitingOwner, 
                BookingStatus.PendingPayment, 
                BookingStatus.Paid 
            };
            
            var bookings = await _db.Bookings
                .Include(b => b.ChargingSlot).ThenInclude(s => s.ChargingStation)
                .Where(b => b.DriverUserId == driverUserId && targetStatuses.Contains(b.Status))
                .ToListAsync();
                
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

            var bookings = await _db.Bookings
                .Include(b => b.ChargingSlot).ThenInclude(s => s.ChargingStation)
                .Where(b => b.ChargingSlot!.StationId == stationId && targetStatuses.Contains(b.Status))
                .ToListAsync();
                
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
            return await _db.Disputes
                .Include(d => d.Booking)
                    .ThenInclude(b => b.ChargingSlot).ThenInclude(s => s.ChargingStation)
                .Include(d => d.Booking)
                    .ThenInclude(b => b.Driver).ThenInclude(dr => dr.User)
                .Include(d => d.Invoice)
                .Include(d => d.CreatedByUser)
                .Include(d => d.Evidences)
                    .ThenInclude(e => e.UploadedByUser)
                .FirstOrDefaultAsync(d => d.Id == id);
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
    }
}
