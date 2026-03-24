using ChargeSlot.Api.DTOs.Dispute;
using ChargeSlot.Api.Enums;
using ChargeSlot.Api.Models;
using ChargeSlot.Api.Repositories.Interfaces;
using ChargeSlot.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

using ChargeSlot.Api.Helpers;
namespace ChargeSlot.Api.Services.Implementation
{
    public class DisputeService : IDisputeService
    {
        private readonly INotificationService _notificationService;
        private readonly Data.ChargeSlotDbContext _db;

        public DisputeService(
            INotificationService notificationService,
            Data.ChargeSlotDbContext db)
        {
            _notificationService = notificationService;
            _db = db;
        }

        /// <summary>
        /// Driver submits dispute: validate booking = CompletedPendingInvoice →
        /// create Dispute + evidence → freeze payment (invoice = UnderDispute) →
        /// booking = Disputed → notify Owner + Admin.
        /// </summary>
        public async Task<DisputeDto> SubmitDisputeAsync(int driverUserId, CreateDisputeDto dto)
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
            var dispute = new Dispute
            {
                BookingId = dto.BookingId,
                InvoiceId = invoice?.Id,
                CreatedByUserId = driverUserId,
                Reason = dto.Reason,
                Description = dto.Description,
                Status = DisputeStatus.WaitingOwnerEvidence,
                CreatedAt = DateTimeHelper.VietnamNow()
            };

            // Evidence sẽ được upload sau khi có disputeId

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

            // Single SaveChanges for all mutations
            await _db.SaveChangesAsync();

            // Upload evidence files
            if (dto.Files?.Length > 0)
            {
                await SaveEvidenceFilesAsync(dispute, dto.Files, driverUserId);
            }

            // Notify Owner
            var ownerUserId = booking.ChargingSlot.ChargingStation.OwnerUserId;
            await _notificationService.SendAsync(
                ownerUserId,
                "Khiếu nại mới từ Driver",
                $"{booking.Driver?.User?.FullName ?? "Driver"} khiếu nại về phiên sạc tại trạm {booking.ChargingSlot?.ChargingStation?.Name}. Lý do: {dto.Reason}. Bạn có 24h để nộp bằng chứng phản hồi.",
                NotificationType.Dispute);

            // Notify Admin
            var adminUsers = await _db.UserRoles
                .Where(ur => ur.RoleId == 1)
                .Select(ur => ur.UserId)
                .ToListAsync();

            foreach (var adminId in adminUsers)
            {
                await _notificationService.SendAsync(
                    adminId,
                    "Khiếu nại mới cần xử lý",
                    $"Khiếu nại mới tại trạm {booking.ChargingSlot?.ChargingStation?.Name} từ {booking.Driver?.User?.FullName ?? "Driver"}. Chờ Owner phản hồi.",
                    NotificationType.Dispute);
            }

            // Reload with details for response
            var result = await LoadDisputeWithDetailsAsync(dispute.Id);
            return MapToDto(result!);
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
            dispute.OwnerResponse = dto.Response;
            dispute.Status = DisputeStatus.PendingReview;

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

            // Notify Admin
            var adminUsers = await _db.UserRoles
                .Where(ur => ur.RoleId == 1)
                .Select(ur => ur.UserId)
                .ToListAsync();

            foreach (var adminId in adminUsers)
            {
                await _notificationService.SendAsync(
                    adminId,
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
                // ESCROW → Driver: full refund (toàn bộ TotalAmount driver đã trả)
                await RefundToDriverAsync(dispute.Booking, dispute);
            }
            else
            {
                // ESCROW → Owner (net) + PLATFORM_REVENUE (fee): giống flow confirm
                if (dispute.Invoice != null)
                {
                    await SettleToOwnerAsync(dispute.Booking, dispute.Invoice, dispute);
                }
            }

            // Notify both parties
            var verdict = dto.IsDriverWin ? "hoàn tiền cho Driver" : "thanh toán cho Owner";
            var driverAmount = dto.IsDriverWin ? $" Số tiền {dispute.Booking.TotalAmount:N0}đ đã hoàn vào ví." : "";
            var ownerAmount = !dto.IsDriverWin ? $" Số tiền {dispute.Invoice?.ChargingAmount:N0}đ đã chuyển vào ví." : "";

            var stationName = dispute.Booking.ChargingSlot?.ChargingStation?.Name ?? "";
            var driverName = dispute.Booking.Driver?.User?.FullName ?? "Driver";

            await _notificationService.SendAsync(
                dispute.Booking.DriverUserId,
                "Kết quả khiếu nại",
                dto.IsDriverWin
                    ? $"Khiếu nại của bạn tại trạm {stationName} đã được chấp nhận. {dispute.Booking.TotalAmount:N0}đ đã hoàn vào ví. {dto.AdminNote}"
                    : $"Khiếu nại của bạn tại trạm {stationName} không được chấp nhận. Tiền đã thanh toán cho chủ trạm. {dto.AdminNote}",
                NotificationType.Dispute);

            var ownerUserId = dispute.Booking.ChargingSlot.ChargingStation.OwnerUserId;
            await _notificationService.SendAsync(
                ownerUserId,
                "Kết quả khiếu nại",
                dto.IsDriverWin
                    ? $"Khiếu nại từ {driverName} tại trạm {stationName}: Driver được hoàn tiền. {dto.AdminNote}"
                    : $"Khiếu nại từ {driverName} tại trạm {stationName}: Bạn được thanh toán. {dispute.Invoice?.ChargingAmount:N0}đ đã chuyển vào ví. {dto.AdminNote}",
                NotificationType.Dispute);

            return MapToDto(dispute);
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
        /// Lưu files bằng chứng vào wwwroot/uploads/disputes/{disputeId}/ và tạo DisputeEvidence records.
        /// </summary>
        private async Task SaveEvidenceFilesAsync(Dispute dispute, IFormFile[] files, int uploadedByUserId)
        {
            var uploadDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "disputes", dispute.Id.ToString());
            Directory.CreateDirectory(uploadDir);

            foreach (var file in files)
            {
                if (file.Length <= 0) continue;

                var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
                var fileName = $"{Guid.NewGuid():N}{ext}";
                var filePath = Path.Combine(uploadDir, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                // Detect file type from extension
                var fileType = ext switch
                {
                    ".jpg" or ".jpeg" or ".png" or ".gif" or ".webp" or ".bmp" => "image",
                    ".mp4" or ".avi" or ".mov" or ".webm" => "video",
                    _ => "document"
                };

                var publicUrl = $"/uploads/disputes/{dispute.Id}/{fileName}";
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
