using ChargeSlot.Api.Data;
using ChargeSlot.Api.Enums;
using ChargeSlot.Api.Models;
using ChargeSlot.Api.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ChargeSlot.Api.Repositories.Implementation
{
    public class DisputeRepository : IDisputeRepository
    {
        private readonly ChargeSlotDbContext _db;

        public DisputeRepository(ChargeSlotDbContext db)
        {
            _db = db;
        }

        public async Task<Dispute?> GetByIdAsync(int id)
        {
            return await _db.Disputes.FindAsync(id);
        }

        public async Task<Dispute?> GetByIdWithDetailsAsync(int id)
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

        public async Task<Dispute?> GetByBookingIdAsync(int bookingId)
        {
            return await _db.Disputes
                .Include(d => d.Evidences)
                .Include(d => d.CreatedByUser)
                .FirstOrDefaultAsync(d => d.BookingId == bookingId);
        }

        public async Task<List<Dispute>> GetPendingAsync()
        {
            return await _db.Disputes
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
        }

        public async Task<int> GetDisputeCountByDriverInMonthAsync(int driverUserId, DateTime monthStart)
        {
            return await _db.Disputes
                .CountAsync(d => d.CreatedByUserId == driverUserId && d.CreatedAt >= monthStart);
        }

        public async Task<int> GetDriverLoseCountInMonthAsync(int driverUserId, DateTime monthStart)
        {
            return await _db.Disputes
                .CountAsync(d => d.CreatedByUserId == driverUserId
                              && d.ResolvedAt >= monthStart
                              && d.Status == DisputeStatus.ResolvedPayout); // ResolvedPayout = Owner win
        }

        public async Task<int> GetStationLoseCountInMonthAsync(int stationId, DateTime monthStart)
        {
            return await _db.Disputes
                .CountAsync(d => d.Booking.ChargingSlot.StationId == stationId
                              && d.ResolvedAt >= monthStart
                              && d.Status == DisputeStatus.ResolvedRefund); // ResolvedRefund = Driver win
        }

        public async Task<bool> HasDisputeForBookingAsync(int bookingId)
        {
            return await _db.Disputes.AnyAsync(d => d.BookingId == bookingId);
        }

        public async Task<List<Dispute>> GetAllAsync(string? status = null)
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

            return await query.OrderByDescending(d => d.CreatedAt).ToListAsync();
        }

        public async Task<List<Dispute>> GetByDriverAsync(int driverUserId)
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
                .Where(d => d.CreatedByUserId == driverUserId)
                .OrderByDescending(d => d.CreatedAt)
                .ToListAsync();
        }

        public async Task<List<Dispute>> GetByOwnerAsync(int ownerUserId)
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
                .Where(d => d.Booking.ChargingSlot.ChargingStation.OwnerUserId == ownerUserId)
                .OrderByDescending(d => d.CreatedAt)
                .ToListAsync();
        }

        public void Add(Dispute dispute)
        {
            _db.Disputes.Add(dispute);
        }

        public void Update(Dispute dispute)
        {
            _db.Disputes.Update(dispute);
        }

        public async Task<List<int>> GetExpiredOwnerEvidenceIdsAsync(DateTime now)
        {
            return await _db.Disputes
                .Where(d => d.Status == DisputeStatus.WaitingOwnerEvidence
                         && d.OwnerEvidenceDeadlineAt.HasValue
                         && d.OwnerEvidenceDeadlineAt.Value <= now)
                .Select(d => d.Id)
                .ToListAsync();
        }

        public async Task<List<int>> GetExpiredAdminReviewIdsAsync(DateTime now)
        {
            return await _db.Disputes
                .Where(d => d.Status == DisputeStatus.PendingReview
                         && d.AdminReviewDeadlineAt.HasValue
                         && d.AdminReviewDeadlineAt.Value <= now)
                .Select(d => d.Id)
                .ToListAsync();
        }

        public async Task<Dispute?> GetByIdWithBookingAndInvoiceDetailsAsync(int id)
        {
            return await _db.Disputes
                .Include(d => d.Booking)
                    .ThenInclude(b => b.ChargingSlot).ThenInclude(s => s.ChargingStation)
                .Include(d => d.Invoice)
                .FirstOrDefaultAsync(d => d.Id == id);
        }

        public async Task<List<Dispute>> GetOwnerEvidenceForReminderAsync(DateTime now, DateTime cutoff)
        {
            return await _db.Disputes
                .Include(d => d.Booking).ThenInclude(b => b.ChargingSlot).ThenInclude(s => s.ChargingStation)
                .Where(d => d.Status == DisputeStatus.WaitingOwnerEvidence
                         && d.OwnerEvidenceDeadlineAt.HasValue
                         && d.OwnerEvidenceDeadlineAt.Value > now
                         && d.OwnerEvidenceDeadlineAt.Value <= cutoff
                         && !d.OwnerReminderSentAt.HasValue)
                .ToListAsync();
        }

        public async Task<List<Dispute>> GetAdminReviewForReminderAsync(DateTime now, DateTime cutoff)
        {
            return await _db.Disputes
                .Where(d => d.Status == DisputeStatus.PendingReview
                         && d.AdminReviewDeadlineAt.HasValue
                         && d.AdminReviewDeadlineAt.Value > now
                         && d.AdminReviewDeadlineAt.Value <= cutoff
                         && !d.AdminReminderSentAt.HasValue)
                .ToListAsync();
        }

        public async Task MarkOwnerReminderSentAsync(int disputeId, DateTime sentAt)
        {
            var dispute = await _db.Disputes.FindAsync(disputeId);
            if (dispute != null)
                dispute.OwnerReminderSentAt = sentAt;
        }

        public async Task MarkAdminReminderSentAsync(int disputeId, DateTime sentAt)
        {
            var dispute = await _db.Disputes.FindAsync(disputeId);
            if (dispute != null)
                dispute.AdminReminderSentAt = sentAt;
        }
    }
}
