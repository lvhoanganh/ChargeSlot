using ChargeSlot.Api.Models;

namespace ChargeSlot.Api.Repositories.Interfaces
{
    public interface IChargingSessionRepository
    {
        Task<ChargingSession?> GetByIdAsync(int id);
        Task<ChargingSession?> GetByIdWithDetailsAsync(int id);
        Task<ChargingSession?> GetByBookingIdAsync(int bookingId);
        Task<List<ChargingSession>> GetActiveByOwnerAsync(int ownerUserId);
        Task<bool> HasSessionByBookingAsync(int bookingId);
        Task<bool> HasOngoingSessionBySlotAsync(int slotId);
        Task<(List<ChargingSession> Items, int TotalCount)> GetAdminAllSessionsAsync(ChargeSlot.Api.DTOs.Admin.Overview.SessionFilterDto filter);
        void Add(ChargingSession session);
        void Update(ChargingSession session);
    }
}

