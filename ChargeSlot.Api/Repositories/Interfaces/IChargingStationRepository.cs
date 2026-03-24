using ChargeSlot.Api.Enums;
using ChargeSlot.Api.Models;

namespace ChargeSlot.Api.Repositories.Interfaces
{
    public interface IChargingStationRepository
    {
        Task<ChargingStation?> GetByIdAsync(int id, bool tracking = false, bool includeDetails = true);
        Task<List<ChargingStation>> GetAllByOwnerAsync(int ownerUserId);
        Task<List<ChargingStation>> GetByApprovalStatusAsync(ApprovalStatus status);
        Task AddAsync(ChargingStation station);
        void Update(ChargingStation station);
        void Remove(ChargingStation station);
        Task SaveChangesAsync();
    }
}
