using ChargeSlot.Api.Models;

namespace ChargeSlot.Api.Repositories.Interfaces
{
    public interface IChargingSlotRepository
    {
        Task<ChargingSlot?> GetByIdAsync(int id, bool tracking = false);
        Task<List<ChargingSlot>> GetAllByStationAsync(int stationId);
        Task AddAsync(ChargingSlot slot);
        void Update(ChargingSlot slot);
        void Remove(ChargingSlot slot);
        Task SaveChangesAsync();
    }
}
