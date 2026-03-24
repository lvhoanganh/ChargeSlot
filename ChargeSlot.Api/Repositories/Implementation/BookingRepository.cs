using ChargeSlot.Api.Data;
using ChargeSlot.Api.Enums;
using ChargeSlot.Api.Models;
using ChargeSlot.Api.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

using ChargeSlot.Api.Helpers;
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
                .Include(b => b.BookingExtraServices).ThenInclude(be => be.ExtraService)
                .FirstOrDefaultAsync(b => b.Id == id);
        }

        public async Task<List<Booking>> GetByDriverAsync(int driverUserId)
        {
            return await _db.Bookings
                .Include(b => b.Driver).ThenInclude(d => d.User)
                .Include(b => b.ChargingSlot).ThenInclude(s => s.ChargingStation)
                .Include(b => b.BookingExtraServices).ThenInclude(be => be.ExtraService)
                .Where(b => b.DriverUserId == driverUserId)
                .OrderByDescending(b => b.CreatedAt)
                .ToListAsync();
        }

        public async Task<List<Booking>> GetByOwnerAsync(int ownerUserId)
        {
            return await _db.Bookings
                .Include(b => b.Driver).ThenInclude(d => d.User)
                .Include(b => b.ChargingSlot).ThenInclude(s => s.ChargingStation)
                .Include(b => b.BookingExtraServices).ThenInclude(be => be.ExtraService)
                .Where(b => b.ChargingSlot.ChargingStation.OwnerUserId == ownerUserId)
                .OrderByDescending(b => b.CreatedAt)
                .ToListAsync();
        }

        public async Task<bool> HasOverlappingBookingAsync(int slotId, DateTime startTime, DateTime endTime, int? excludeBookingId = null)
        {
            // Buffer 15 phút giữa các booking để driver trước lấy xe ra, driver sau đưa xe vào
            const int bufferMinutes = 15;

            // WaitingOwner NOT included: cho phép nhiều driver request cùng giờ
            // Chỉ block khi Owner đã accept (PendingPayment trở đi)
            var activeStatuses = new[]
            {
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
            booking.UpdatedAt = DateTimeHelper.VietnamNow();
            _db.Bookings.Update(booking);
            await _db.SaveChangesAsync();
        }

        public async Task<List<Booking>> GetExpiredPendingPaymentsAsync()
        {
            return await _db.Bookings
                .Include(b => b.ChargingSlot)
                .Include(b => b.Payment)
                .Where(b => b.Status == BookingStatus.PendingPayment
                    && b.PaymentExpiresAt.HasValue
                    && b.PaymentExpiresAt.Value <= DateTimeHelper.VietnamNow())
                .ToListAsync();
        }

        /// <summary>
        /// Tìm các booking WaitingOwner trùng giờ trên cùng slot (để auto-reject khi Owner accept).
        /// </summary>
        public async Task<List<Booking>> GetOverlappingWaitingBookingsAsync(
            int slotId, DateTime startTime, DateTime endTime, int excludeBookingId)
        {
            const int bufferMinutes = 15;

            return await _db.Bookings
                .Include(b => b.Driver).ThenInclude(d => d.User)
                .Where(b => b.SlotId == slotId
                    && b.Status == BookingStatus.WaitingOwner
                    && b.Id != excludeBookingId
                    && b.StartTime < endTime.AddMinutes(bufferMinutes)
                    && b.EndTime.AddMinutes(bufferMinutes) > startTime)
                .ToListAsync();
        }

        /// <summary>
        /// Lấy bookings đang active trên slot cho ngày cụ thể (dùng cho availability API).
        /// </summary>
        public async Task<List<Booking>> GetActiveBookingsForSlotAsync(int slotId, DateTime date)
        {
            var dayStart = date.Date;
            var dayEnd = dayStart.AddDays(1);

            var activeStatuses = new[]
            {
                BookingStatus.WaitingOwner,
                BookingStatus.PendingPayment,
                BookingStatus.Paid,
                BookingStatus.CheckedIn,
                BookingStatus.InProgress
            };

            return await _db.Bookings
                .Where(b => b.SlotId == slotId
                    && activeStatuses.Contains(b.Status)
                    && b.StartTime < dayEnd
                    && b.EndTime > dayStart)
                .OrderBy(b => b.StartTime)
                .ToListAsync();
        }
    }
}
