using ChargeSlot.Api.Data;
using ChargeSlot.Api.Enums;
using ChargeSlot.Api.Models;
using ChargeSlot.Api.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ChargeSlot.Api.Repositories.Implementation
{
    public class BookingRepository : IBookingRepository
    {
        private readonly ChargeSlotDbContext _db;

        public BookingRepository(ChargeSlotDbContext db)
        {
            _db = db;
        }

        public async Task<Booking?> GetByIdAsync(int id)
        {
            return await _db.Bookings.FindAsync(id);
        }

        public async Task<Booking?> GetByIdWithDetailsAsync(int id)
        {
            return await _db.Bookings
                .Include(b => b.Driver).ThenInclude(d => d.User)
                .Include(b => b.ChargingSlot).ThenInclude(s => s.ChargingStation)
                .Include(b => b.Payment)
                .FirstOrDefaultAsync(b => b.Id == id);
        }

        public async Task<List<Booking>> GetByDriverAsync(int driverUserId)
        {
            return await _db.Bookings
                .Include(b => b.Driver).ThenInclude(d => d.User)
                .Include(b => b.ChargingSlot).ThenInclude(s => s.ChargingStation)
                .Where(b => b.DriverUserId == driverUserId)
                .OrderByDescending(b => b.CreatedAt)
                .ToListAsync();
        }

        public async Task<List<Booking>> GetByOwnerAsync(int ownerUserId)
        {
            return await _db.Bookings
                .Include(b => b.Driver).ThenInclude(d => d.User)
                .Include(b => b.ChargingSlot).ThenInclude(s => s.ChargingStation)
                .Where(b => b.ChargingSlot.ChargingStation.OwnerUserId == ownerUserId)
                .OrderByDescending(b => b.CreatedAt)
                .ToListAsync();
        }

        public async Task<bool> HasOverlappingBookingAsync(int slotId, DateTime startTime, DateTime endTime, int? excludeBookingId = null)
        {
            // Buffer 15 phút giữa các booking để driver trước lấy xe ra, driver sau đưa xe vào
            const int bufferMinutes = 15;

            var activeStatuses = new[]
            {
                BookingStatus.WaitingOwner,
                BookingStatus.PendingPayment,
                BookingStatus.Paid,
                BookingStatus.CheckedIn,
                BookingStatus.InProgress
            };

            var query = _db.Bookings
                .Where(b => b.SlotId == slotId
                    && activeStatuses.Contains(b.Status)
                    && b.StartTime < endTime.AddMinutes(bufferMinutes)
                    && b.EndTime.AddMinutes(bufferMinutes) > startTime);

            if (excludeBookingId.HasValue)
                query = query.Where(b => b.Id != excludeBookingId.Value);

            return await query.AnyAsync();
        }

        public async Task<bool> HasDriverOverlappingBookingAsync(int driverUserId, DateTime startTime, DateTime endTime, int? excludeBookingId = null)
        {
            var activeStatuses = new[]
            {
                BookingStatus.WaitingOwner,
                BookingStatus.PendingPayment,
                BookingStatus.Paid,
                BookingStatus.CheckedIn,
                BookingStatus.InProgress
            };

            var query = _db.Bookings
                .Where(b => b.DriverUserId == driverUserId
                    && activeStatuses.Contains(b.Status)
                    && b.StartTime < endTime
                    && b.EndTime > startTime);

            if (excludeBookingId.HasValue)
                query = query.Where(b => b.Id != excludeBookingId.Value);

            return await query.AnyAsync();
        }

        public async Task<Booking> CreateAsync(Booking booking)
        {
            _db.Bookings.Add(booking);
            await _db.SaveChangesAsync();
            return booking;
        }

        public async Task UpdateAsync(Booking booking)
        {
            booking.UpdatedAt = DateTime.UtcNow;
            _db.Bookings.Update(booking);
            await _db.SaveChangesAsync();
        }

        public async Task<List<Booking>> GetExpiredPendingPaymentsAsync()
        {
            return await _db.Bookings
                .Include(b => b.ChargingSlot)
                .Where(b => b.Status == BookingStatus.PendingPayment
                    && b.PaymentExpiresAt.HasValue
                    && b.PaymentExpiresAt.Value <= DateTime.UtcNow)
                .ToListAsync();
        }
    }
}
