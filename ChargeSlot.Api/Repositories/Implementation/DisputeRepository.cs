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

        public async Task<Dispute> CreateAsync(Dispute dispute)
        {
            _db.Disputes.Add(dispute);
            await _db.SaveChangesAsync();
            return dispute;
        }

        public async Task UpdateAsync(Dispute dispute)
        {
            _db.Disputes.Update(dispute);
            await _db.SaveChangesAsync();
        }
    }
}
