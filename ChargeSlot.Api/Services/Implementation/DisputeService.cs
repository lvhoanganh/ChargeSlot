using ChargeSlot.Api.DTOs.Dispute;
using ChargeSlot.Api.Enums;
using ChargeSlot.Api.Models;
using ChargeSlot.Api.Repositories.Interfaces;
using ChargeSlot.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

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
                CreatedAt = DateTime.UtcNow
            };

            // Add evidence
            if (dto.Evidences?.Count > 0)
            {
                foreach (var ev in dto.Evidences)
                {
                    dispute.Evidences.Add(new DisputeEvidence
                    {
                        FileUrl = ev.FileUrl,
                        FileType = ev.FileType,
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }

            _db.Disputes.Add(dispute);

            // Freeze payment: invoice → UnderDispute
            if (invoice != null)
            {
                invoice.Status = InvoiceStatus.UnderDispute;
                invoice.UpdatedAt = DateTime.UtcNow;
            }

            // Booking → Disputed
            booking.Status = BookingStatus.Disputed;
            booking.UpdatedAt = DateTime.UtcNow;

            // Single SaveChanges for all mutations
            await _db.SaveChangesAsync();

            // Notify Owner
            var ownerUserId = booking.ChargingSlot.ChargingStation.OwnerUserId;
            await _notificationService.SendAsync(
                ownerUserId,
                "Khiếu nại mới từ Driver",
                $"Booking #{booking.Id} bị khiếu nại. Lý do: {dto.Reason}. Bạn có 1 ngày để nộp bằng chứng phản hồi.",
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
                    $"Booking #{booking.Id} có khiếu nại mới. Chờ Owner phản hồi.",
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

            // Add evidence
            if (dto.Evidences?.Count > 0)
            {
                foreach (var ev in dto.Evidences)
                {
                    dispute.Evidences.Add(new DisputeEvidence
                    {
                        DisputeId = dispute.Id,
                        FileUrl = ev.FileUrl,
                        FileType = ev.FileType,
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }

            await _db.SaveChangesAsync();

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
                    $"Khiếu nại #{dispute.Id} (Booking #{dispute.BookingId}) đã có phản hồi từ Owner. Sẵn sàng xem xét.",
                    NotificationType.Dispute);
            }

            var result = await LoadDisputeWithDetailsAsync(dispute.Id);
            return MapToDto(result!);
        }

        /// <summary>
        /// Admin resolves dispute:
        /// - Driver wins → ResolvedRefund → refund driver
        /// - Owner wins → ResolvedPayout → pay owner
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

            var now = DateTime.UtcNow;

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

            // Notify both parties
            var verdict = dto.IsDriverWin ? "hoàn tiền cho Driver" : "thanh toán cho Owner";

            await _notificationService.SendAsync(
                dispute.Booking.DriverUserId,
                "Kết quả khiếu nại",
                $"Khiếu nại #{dispute.Id} đã được xử lý. Kết quả: {verdict}. {dto.AdminNote}",
                NotificationType.Dispute);

            var ownerUserId = dispute.Booking.ChargingSlot.ChargingStation.OwnerUserId;
            await _notificationService.SendAsync(
                ownerUserId,
                "Kết quả khiếu nại",
                $"Khiếu nại #{dispute.Id} đã được xử lý. Kết quả: {verdict}. {dto.AdminNote}",
                NotificationType.Dispute);

            return MapToDto(dispute);
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

        // ─────────────── HELPERS ───────────────

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
                    FileUrl = e.FileUrl,
                    FileType = e.FileType,
                    CreatedAt = e.CreatedAt
                }).ToList()
            };
        }
    }
}
