using ChargeSlot.Api.Models;

namespace ChargeSlot.Api.Repositories.Interfaces
{
    public interface IChargingSlotRepository
    {
        Task<ChargingSlot?> GetByIdAsync(int id, bool tracking = false);
        Task<List<ChargingSlot>> GetAllByStationAsync(int stationId);
        Task<(List<ChargingSlot> Items, int TotalCount)> GetAllByStationPagedAsync(int stationId, int page, int pageSize);
        Task AddAsync(ChargingSlot slot);
        void Update(ChargingSlot slot);
        void Remove(ChargingSlot slot);
        Task<ChargingSlot?> GetByQrCodeTokenAsync(string qrCodeToken);
    }
}

