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

        public async Task<bool> HasSessionByBookingAsync(int bookingId)
        {
            return await _db.ChargingSessions.AnyAsync(s => s.BookingId == bookingId);
        }

        public async Task<bool> HasOngoingSessionBySlotAsync(int slotId)
        {
            return await _db.ChargingSessions.AnyAsync(s => s.Booking.SlotId == slotId && s.ActualEndTime == null);
        }

        public async Task<(List<ChargingSession> Items, int TotalCount)> GetAdminAllSessionsAsync(ChargeSlot.Api.DTOs.Admin.Overview.SessionFilterDto filter)
        {
            IQueryable<ChargingSession> query = _db.ChargingSessions
                .Include(s => s.Booking).ThenInclude(b => b.Driver).ThenInclude(u => u.User)
                .Include(s => s.Booking).ThenInclude(b => b.ChargingSlot).ThenInclude(cs => cs.ChargingStation)
                .AsNoTracking();

            if (!string.IsNullOrEmpty(filter.Status))
            {
                if (System.Enum.TryParse<ChargeSlot.Api.Enums.BookingStatus>(filter.Status, true, out var statusEnum))
                {
                    query = query.Where(s => s.Booking.Status == statusEnum);
                }
            }

            if (filter.BookingId.HasValue)
            {
                query = query.Where(s => s.BookingId == filter.BookingId.Value);
            }

            if (filter.FromDate.HasValue)
            {
                query = query.Where(s => s.ActualStartTime >= filter.FromDate.Value || s.CreatedAt >= filter.FromDate.Value);
            }
            if (filter.ToDate.HasValue)
            {
                query = query.Where(s => s.ActualStartTime <= filter.ToDate.Value || s.CreatedAt <= filter.ToDate.Value);
            }

            int totalCount = await query.CountAsync();

            var items = await query
                .OrderByDescending(s => s.CreatedAt)
                .Skip((filter.Page - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .ToListAsync();

            return (items, totalCount);
        }

        public void Add(ChargingSession session)
        {
            _db.ChargingSessions.Add(session);
        }

        public void Update(ChargingSession session)
        {
            _db.ChargingSessions.Update(session);
        }
    }
}

