using ChargeSlot.Api.Enums;
using ChargeSlot.Api.Models;

namespace ChargeSlot.Api.Repositories.Interfaces
{
    public interface IBookingRepository
    {
        Task<Booking?> GetByIdAsync(int id);
        Task<Booking?> GetByIdWithDetailsAsync(int id);
        Task<List<Booking>> GetByDriverAsync(int driverUserId);
        Task<List<Booking>> GetByOwnerAsync(int ownerUserId);
        Task<bool> HasOverlappingBookingAsync(int slotId, DateTime startTime, DateTime endTime, int bufferMinutes, int? excludeBookingId = null);
        Task<bool> HasDriverOverlappingBookingAsync(int driverUserId, DateTime startTime, DateTime endTime, int? excludeBookingId = null);
        Task<int> GetPendingCountByDriverAsync(int driverUserId);
        void Add(Booking booking);
        void Update(Booking booking);
        Task<List<Booking>> GetExpiredPendingPaymentsAsync();
        Task<List<Booking>> GetOverlappingWaitingBookingsAsync(int slotId, DateTime startTime, DateTime endTime, int bufferMinutes, int excludeBookingId);
        Task<List<Booking>> GetOverlappingActiveBookingsForStationsAsync(List<int> stationIds, DateTime startTime, DateTime endTime, int bufferMinutes);
        Task<bool> HasAnyBookingsAsync(int slotId);
        Task<bool> HasActiveBookingsAsync(int slotId);
        Task<List<Booking>> GetActiveBookingsByDriverAsync(int driverUserId, BookingStatus[] statuses);
        Task<List<Booking>> GetActiveBookingsByStationIdsAsync(List<int> stationIds, BookingStatus[] statuses);
        Task<List<Booking>> GetActiveBookingsForSlotAsync(int slotId, DateTime date);
        Task<(List<Booking> Items, int TotalCount)> GetAdminBookingsPagedAsync(ChargeSlot.Api.DTOs.Admin.Overview.BookingFilterDto filter);
        Task<Booking?> GetPaidBookingForDriverAndSlotAsync(int driverUserId, int slotId);
        Task<List<Booking>> GetStaleWaitingOwnerAsync(DateTime cutoff);
        Task<List<Booking>> GetPaidNoShowAsync(DateTime cutoff);
        Task<List<Booking>> GetCheckedInOvertimeAsync(DateTime cutoff);
        Task<List<Booking>> GetApproachingPaidBookingsAsync(DateTime now, DateTime cutoff);
        Task MarkReminderSentAsync(int bookingId, DateTime sentAt);

        /// <summary>SQL Server distributed lock per slot (sp_getapplock). Must be called inside a transaction.</summary>
        Task AcquireSlotLockAsync(int slotId);
    }
}

