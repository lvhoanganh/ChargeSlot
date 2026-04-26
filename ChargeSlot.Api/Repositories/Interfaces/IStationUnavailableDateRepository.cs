using ChargeSlot.Api.Models;

namespace ChargeSlot.Api.Repositories.Interfaces
{
    public interface IStationUnavailableDateRepository
    {
        Task<List<DateOnly>> GetDatesByStationAndDateRangeAsync(int stationId, DateOnly startDate, DateOnly endDate);
        Task<List<DateOnly>> GetDatesByStationAsync(int stationId);
        Task<List<StationUnavailableDate>> GetByStationIdAsync(int stationId);
        void Add(StationUnavailableDate record);
        void RemoveRange(IEnumerable<StationUnavailableDate> records);
        Task<List<StationUnavailableDate>> GetByIdsAsync(int stationId, List<int> ids);
    }
}
