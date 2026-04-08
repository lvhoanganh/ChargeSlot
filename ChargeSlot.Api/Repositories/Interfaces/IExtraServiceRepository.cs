using ChargeSlot.Api.Models;

namespace ChargeSlot.Api.Repositories.Interfaces
{
    public interface IExtraServiceRepository
    {
        Task<List<ExtraService>> GetByStationIdAsync(int stationId);
        Task<List<ExtraService>> GetByIdsAsync(List<int> ids);
        Task<ExtraService?> GetByIdAsync(int id);
        Task<ExtraService?> GetByIdAndStationIdAsync(int id, int stationId);
        Task<bool> HasBookingsAsync(int serviceId);
        void Add(ExtraService service);
        void Update(ExtraService service);
        void Remove(ExtraService service);
    }
}
