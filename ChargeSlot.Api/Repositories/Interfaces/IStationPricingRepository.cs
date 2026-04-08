using ChargeSlot.Api.Models;

namespace ChargeSlot.Api.Repositories.Interfaces
{
    public interface IStationPricingRepository
    {
        Task<List<StationPricing>> GetByStationIdAsync(int stationId);
        Task<List<StationPricing>> GetActiveByStationIdAsync(int stationId);
        Task<StationPricing?> GetByIdAsync(int id, int stationId);
        void Add(StationPricing pricing);
        void Update(StationPricing pricing);
        void Remove(StationPricing pricing);
    }
}
