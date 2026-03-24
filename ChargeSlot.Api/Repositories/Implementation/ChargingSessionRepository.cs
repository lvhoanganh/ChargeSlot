using ChargeSlot.Api.Data;
using ChargeSlot.Api.Enums;
using ChargeSlot.Api.Models;
using ChargeSlot.Api.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ChargeSlot.Api.Repositories.Implementation
{
    public class ChargingSessionRepository : IChargingSessionRepository
    {
        private readonly ChargeSlotDbContext _db;

        public ChargingSessionRepository(ChargeSlotDbContext db)
        {
            _db = db;
        }

        public async Task<ChargingSession?> GetByIdAsync(int id)
        {
            return await _db.ChargingSessions.FindAsync(id);
        }

        public async Task<ChargingSession?> GetByIdWithDetailsAsync(int id)
        {
            return await _db.ChargingSessions
                .Include(s => s.Booking)
                    .ThenInclude(b => b.Driver).ThenInclude(d => d.User)
                .Include(s => s.Booking)
                    .ThenInclude(b => b.ChargingSlot).ThenInclude(sl => sl.ChargingStation)
                .FirstOrDefaultAsync(s => s.Id == id);
        }

        public async Task<ChargingSession?> GetByBookingIdAsync(int bookingId)
        {
            return await _db.ChargingSessions
                .Include(s => s.Booking)
                    .ThenInclude(b => b.Driver).ThenInclude(d => d.User)
                .Include(s => s.Booking)
                    .ThenInclude(b => b.ChargingSlot).ThenInclude(sl => sl.ChargingStation)
                .FirstOrDefaultAsync(s => s.BookingId == bookingId);
        }

        public async Task<List<ChargingSession>> GetActiveByOwnerAsync(int ownerUserId)
        {
            return await _db.ChargingSessions
                .Include(s => s.Booking)
                    .ThenInclude(b => b.Driver).ThenInclude(d => d.User)
                .Include(s => s.Booking)
                    .ThenInclude(b => b.ChargingSlot).ThenInclude(sl => sl.ChargingStation)
                .Where(s => s.Booking.ChargingSlot.ChargingStation.OwnerUserId == ownerUserId
                    && s.ActualEndTime == null)
                .OrderByDescending(s => s.CreatedAt)
                .ToListAsync();
        }

        public async Task<ChargingSession> CreateAsync(ChargingSession session)
        {
            _db.ChargingSessions.Add(session);
            await _db.SaveChangesAsync();
            return session;
        }

        public async Task UpdateAsync(ChargingSession session)
        {
            _db.ChargingSessions.Update(session);
            await _db.SaveChangesAsync();
        }
    }
}
