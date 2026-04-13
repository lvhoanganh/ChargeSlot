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
                .Include(b => b.ChargingSession)
                .Include(b => b.Dispute)
                .Include(b => b.Invoice)
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

        public async Task<bool> HasOverlappingBookingAsync(int slotId, DateTime startTime, DateTime endTime, int bufferMinutes, int? excludeBookingId = null)
        {

            // WaitingOwner NOT included: cho phép nhiều driver request cùng giờ
            // Chỉ block khi Owner đã accept (PendingPayment trở đi)
            var activeStatuses = new[]
            {
                BookingStatus.PendingPayment,
                BookingStatus.Paid,
                BookingStatus.CheckedIn,
                BookingStatus.InProgress,
                BookingStatus.CompletedPendingInvoice,
                BookingStatus.Completed
            };

            var query = _db.Bookings
                .Where(b => b.SlotId == slotId
                    && activeStatuses.Contains(b.Status)
                    && b.StartTime < endTime.AddMinutes(bufferMinutes)
                    && (b.ChargingSession != null && b.ChargingSession.ActualEndTime != null 
                            ? b.ChargingSession.ActualEndTime.Value 
                            : b.EndTime).AddMinutes(bufferMinutes) > startTime);

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

        public async Task<int> GetPendingCountByDriverAsync(int driverUserId)
        {
            return await _db.Bookings
                .CountAsync(b => b.DriverUserId == driverUserId && 
                            (b.Status == BookingStatus.WaitingOwner || b.Status == BookingStatus.PendingPayment));
        }

        public void Add(Booking booking)
        {
            _db.Bookings.Add(booking);
        }

        public void Update(Booking booking)
        {
            booking.UpdatedAt = DateTimeHelper.VietnamNow();
            _db.Bookings.Update(booking);
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
            int slotId, DateTime startTime, DateTime endTime, int bufferMinutes, int excludeBookingId)
        {

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
                BookingStatus.InProgress,
                BookingStatus.CompletedPendingInvoice,
                BookingStatus.Completed
            };

            return await _db.Bookings
                .Include(b => b.ChargingSession)
                .Where(b => b.SlotId == slotId
                    && activeStatuses.Contains(b.Status)
                    && b.StartTime < dayEnd
                    && (b.ChargingSession != null && b.ChargingSession.ActualEndTime != null 
                            ? b.ChargingSession.ActualEndTime.Value 
                            : b.EndTime) > dayStart)
                .OrderBy(b => b.StartTime)
                .ToListAsync();
        }

        public async Task<List<Booking>> GetOverlappingActiveBookingsForStationsAsync(List<int> stationIds, DateTime startTime, DateTime endTime, int bufferMinutes)
        {
            var activeStatuses = new[]
            {
                BookingStatus.PendingPayment, BookingStatus.Paid, BookingStatus.CheckedIn,
                BookingStatus.InProgress, BookingStatus.CompletedPendingInvoice, BookingStatus.Completed
            };

            return await _db.Bookings
                .Include(b => b.ChargingSession)
                .Where(b => stationIds.Contains(b.ChargingSlot.StationId)
                    && activeStatuses.Contains(b.Status)
                    && b.StartTime < endTime.AddMinutes(bufferMinutes)
                    && (b.ChargingSession != null && b.ChargingSession.ActualEndTime != null
                            ? b.ChargingSession.ActualEndTime.Value
                            : b.EndTime).AddMinutes(bufferMinutes) > startTime)
                .ToListAsync();
        }

        public async Task<bool> HasAnyBookingsAsync(int slotId)
        {
            return await _db.Bookings.AnyAsync(b => b.SlotId == slotId);
        }

        public async Task<bool> HasActiveBookingsAsync(int slotId)
        {
            var activeStatuses = new[]
            {
                BookingStatus.WaitingOwner,
                BookingStatus.PendingPayment,
                BookingStatus.Paid,
                BookingStatus.CheckedIn,
                BookingStatus.InProgress
            };
            return await _db.Bookings.AnyAsync(b => b.SlotId == slotId && activeStatuses.Contains(b.Status));
        }

        public async Task<List<Booking>> GetActiveBookingsByDriverAsync(int driverUserId, BookingStatus[] statuses)
        {
            return await _db.Bookings
                .Include(b => b.ChargingSlot).ThenInclude(s => s.ChargingStation)
                .Where(b => b.DriverUserId == driverUserId && statuses.Contains(b.Status))
                .ToListAsync();
        }

        public async Task<List<Booking>> GetActiveBookingsByStationIdsAsync(List<int> stationIds, BookingStatus[] statuses)
        {
            return await _db.Bookings
                .Include(b => b.ChargingSlot).ThenInclude(s => s.ChargingStation)
                .Where(b => stationIds.Contains(b.ChargingSlot!.StationId) && statuses.Contains(b.Status))
                .ToListAsync();
        }
        public async Task<(List<Booking> Items, int TotalCount)> GetAdminBookingsPagedAsync(ChargeSlot.Api.DTOs.Admin.Overview.BookingFilterDto filter)
        {
            var query = _db.Bookings
                .Include(b => b.ChargingSlot).ThenInclude(cs => cs.ChargingStation)
                .Include(b => b.Payment)
                .Include(b => b.Driver).ThenInclude(u => u.User)
                .Include(b => b.BookingExtraServices).ThenInclude(es => es.ExtraService)
                .AsNoTracking()
                .AsQueryable();

            if (!string.IsNullOrEmpty(filter.Status))
            {
                if (System.Enum.TryParse<ChargeSlot.Api.Enums.BookingStatus>(filter.Status, true, out var statusEnum))
                {
                    query = query.Where(b => b.Status == statusEnum);
                }
            }

            if (filter.DriverUserId.HasValue)
            {
                query = query.Where(b => b.DriverUserId == filter.DriverUserId.Value);
            }

            if (filter.OwnerUserId.HasValue)
            {
                query = query.Where(b => b.ChargingSlot.ChargingStation.OwnerUserId == filter.OwnerUserId.Value);
            }

            if (filter.StationId.HasValue)
            {
                query = query.Where(b => b.ChargingSlot.StationId == filter.StationId.Value);
            }

            if (filter.FromDate.HasValue)
            {
                query = query.Where(b => b.CreatedAt >= filter.FromDate.Value);
            }
            if (filter.ToDate.HasValue)
            {
                query = query.Where(b => b.CreatedAt <= filter.ToDate.Value);
            }

            int totalCount = await query.CountAsync();

            var items = await query
                .OrderByDescending(b => b.CreatedAt)
                .Skip((filter.Page - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .ToListAsync();

            return (items, totalCount);
        }

        public async Task<Booking?> GetPaidBookingForDriverAndSlotAsync(int driverUserId, int slotId)
        {
            return await _db.Bookings
                .Include(b => b.Driver).ThenInclude(d => d.User)
                .Include(b => b.ChargingSlot).ThenInclude(s => s.ChargingStation)
                .FirstOrDefaultAsync(b =>
                    b.DriverUserId == driverUserId
                    && b.SlotId == slotId
                    && b.Status == BookingStatus.Paid);
        }

        public async Task<List<Booking>> GetStaleWaitingOwnerAsync(DateTime cutoff)
        {
            return await _db.Bookings
                .Where(b => b.Status == BookingStatus.WaitingOwner && b.CreatedAt <= cutoff)
                .Include(b => b.ChargingSlot).ThenInclude(s => s.ChargingStation)
                .ToListAsync();
        }

        public async Task<List<Booking>> GetPaidNoShowAsync(DateTime cutoff)
        {
            return await _db.Bookings
                .Where(b => b.Status == BookingStatus.Paid && b.EndTime < cutoff && b.ManualCheckinRequestedAt == null)
                .Include(b => b.ChargingSlot).ThenInclude(s => s.ChargingStation)
                .Include(b => b.Driver).ThenInclude(d => d.User)
                .Include(b => b.BookingExtraServices)
                .ToListAsync();
        }

        public async Task<List<Booking>> GetCheckedInOvertimeAsync(DateTime cutoff)
        {
            return await _db.Bookings
                .Where(b => b.Status == BookingStatus.CheckedIn && b.EndTime < cutoff)
                .Include(b => b.ChargingSlot).ThenInclude(s => s.ChargingStation)
                .Include(b => b.ChargingSession)
                .Include(b => b.Driver)
                .Include(b => b.BookingExtraServices)
                .ToListAsync();
        }

        public async Task<List<Booking>> GetApproachingPaidBookingsAsync(DateTime now, DateTime cutoff)
        {
            return await _db.Bookings
                .Include(b => b.ChargingSlot).ThenInclude(s => s.ChargingStation)
                .Where(b => b.Status == BookingStatus.Paid
                         && b.StartTime > now
                         && b.StartTime <= cutoff
                         && !b.ReminderSentAt.HasValue)
                .ToListAsync();
        }

        public async Task MarkReminderSentAsync(int bookingId, DateTime sentAt)
        {
            var booking = await _db.Bookings.FindAsync(bookingId);
            if (booking != null)
                booking.ReminderSentAt = sentAt;
        }

        public async Task AcquireSlotLockAsync(int slotId)
        {
            var lockResource = $"SlotLock_{slotId}";
            await _db.Database.ExecuteSqlRawSafeAsync(
                "EXEC sp_getapplock @Resource = {0}, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 5000",
                lockResource);
        }

    }
}

