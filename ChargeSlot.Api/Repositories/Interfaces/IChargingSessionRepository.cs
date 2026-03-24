using ChargeSlot.Api.Models;

namespace ChargeSlot.Api.Repositories.Interfaces
{
    public interface IChargingSessionRepository
    {
        Task<ChargingSession?> GetByIdAsync(int id);
        Task<ChargingSession?> GetByIdWithDetailsAsync(int id);
        Task<ChargingSession?> GetByBookingIdAsync(int bookingId);
        Task<List<ChargingSession>> GetActiveByOwnerAsync(int ownerUserId);
        Task<ChargingSession> CreateAsync(ChargingSession session);
        Task UpdateAsync(ChargingSession session);
    }
}
