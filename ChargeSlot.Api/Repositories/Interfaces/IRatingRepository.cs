using ChargeSlot.Api.Models;

namespace ChargeSlot.Api.Repositories.Interfaces
{
    public interface IRatingRepository
    {
        Task<bool> HasRatingForBookingAsync(int bookingId);
        void Add(Rating rating);
        Task<Rating?> GetByIdWithStationAsync(int id);
        Task<Rating?> GetByIdWithDetailsAsync(int id);
        Task<(List<Rating> Items, int TotalCount)> GetRatingsByStationPagedAsync(int stationId, int page, int pageSize);
        Task<Dictionary<int, int>> GetRatingCountsByStationAsync(int stationId);
        Task<(decimal Average, int Count)?> GetRatingStatsAsync(int stationId);
    }
}
