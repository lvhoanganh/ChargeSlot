using ChargeSlot.Api.DTOs.Station;

namespace ChargeSlot.Api.Services.Interfaces
{
    public interface IFavoriteService
    {
        Task AddFavoriteAsync(int driverUserId, int stationId);
        Task RemoveFavoriteAsync(int driverUserId, int stationId);
        Task<List<FavoriteStationDto>> GetMyFavoritesAsync(int driverUserId);
        Task<List<TopFavoriteStationDto>> GetTopFavoritesAsync(int limit);
        Task<bool> CheckFavoriteAsync(int driverUserId, int stationId);
    }
}
