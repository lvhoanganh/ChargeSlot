using ChargeSlot.Api.Models;

namespace ChargeSlot.Api.Repositories.Interfaces
{
    public interface IFavoriteStationRepository
    {
        Task<FavoriteStation?> GetAsync(int driverUserId, int stationId);
        Task<bool> ExistsAsync(int driverUserId, int stationId);
        Task<List<FavoriteStation>> GetByDriverAsync(int driverUserId);
        Task<List<(int StationId, int FavoriteCount)>> GetTopFavoriteStationIdsAsync(int limit);
        void Add(FavoriteStation favoriteStation);
        void Remove(FavoriteStation favoriteStation);
    }
}
