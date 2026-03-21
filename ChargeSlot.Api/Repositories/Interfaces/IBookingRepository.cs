using ChargeSlot.Api.Models;

namespace ChargeSlot.Api.Repositories.Interfaces
{
    public interface IBookingRepository
    {
        Task<Booking?> GetByIdAsync(int id);
        Task<Booking?> GetByIdWithDetailsAsync(int id);
        Task<List<Booking>> GetByDriverAsync(int driverUserId);
        Task<List<Booking>> GetByOwnerAsync(int ownerUserId);
        Task<bool> HasOverlappingBookingAsync(int slotId, DateTime startTime, DateTime endTime, int? excludeBookingId = null);
        Task<bool> HasDriverOverlappingBookingAsync(int driverUserId, DateTime startTime, DateTime endTime, int? excludeBookingId = null);
        Task<Booking> CreateAsync(Booking booking);
        Task UpdateAsync(Booking booking);
        Task<List<Booking>> GetExpiredPendingPaymentsAsync();
    }
}
