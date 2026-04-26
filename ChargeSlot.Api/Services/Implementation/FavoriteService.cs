using ChargeSlot.Api.DTOs.Station;
using ChargeSlot.Api.Models;
using ChargeSlot.Api.Repositories.Interfaces;
using ChargeSlot.Api.Services.Interfaces;
using ChargeSlot.Api.Helpers;

namespace ChargeSlot.Api.Services.Implementation
{
    public class FavoriteService : IFavoriteService
    {
        private readonly IFavoriteStationRepository _favoriteRepo;
        private readonly IChargingStationRepository _stationRepo;
        private readonly IUnitOfWork _unitOfWork;

        public FavoriteService(
            IFavoriteStationRepository favoriteRepo, 
            IChargingStationRepository stationRepo,
            IUnitOfWork unitOfWork)
        {
            _favoriteRepo = favoriteRepo;
            _stationRepo = stationRepo;
            _unitOfWork = unitOfWork;
        }

        public async Task AddFavoriteAsync(int driverUserId, int stationId)
        {
            var station = await _stationRepo.GetByIdAsync(stationId);
            if (station == null)
                throw new InvalidOperationException("Trạm sạc không tồn tại.");

            var exists = await _favoriteRepo.ExistsAsync(driverUserId, stationId);
            if (exists)
                throw new InvalidOperationException("Đã có trong danh sách yêu thích.");

            var fav = new FavoriteStation
            {
                DriverUserId = driverUserId,
                StationId = stationId,
                CreatedAt = DateTimeHelper.VietnamNow()
            };

            _favoriteRepo.Add(fav);
            await _unitOfWork.CompleteAsync();
        }

        public async Task RemoveFavoriteAsync(int driverUserId, int stationId)
        {
            var fav = await _favoriteRepo.GetAsync(driverUserId, stationId);
            if (fav == null)
                throw new InvalidOperationException("Trạm chưa có trong yêu thích.");

            _favoriteRepo.Remove(fav);
            await _unitOfWork.CompleteAsync();
        }

        public async Task<List<FavoriteStationDto>> GetMyFavoritesAsync(int driverUserId)
        {
            var favorites = await _favoriteRepo.GetByDriverAsync(driverUserId);

            return favorites.Select(f => new FavoriteStationDto
            {
                StationId = f.Station.Id,
                Name = f.Station.Name,
                Address = f.Station.Address,
                ImageUrl = f.Station.Images.FirstOrDefault()?.ImageUrl,
                AverageRating = f.Station.AverageRating,
                TotalReviews = f.Station.TotalReviews,
                IsFavorite = true,
                FavoritedAt = f.CreatedAt
            }).ToList();
        }

        public async Task<List<TopFavoriteStationDto>> GetTopFavoritesAsync(int limit)
        {
            var topIds = await _favoriteRepo.GetTopFavoriteStationIdsAsync(limit);
            if (!topIds.Any()) return new List<TopFavoriteStationDto>();

            var stationIds = topIds.Select(t => t.StationId).ToList();
            
            // To maintain order properly and not re-fetch unneeded things, we should ideally have a GetStationsByIds in repo.
            // But since we are migrating, we can just fetch one by one or we need to add `GetByIdsAsync` to `IChargingStationRepository`.
            // Let's just use what we have or implement a quick loop if count is small (limit is 10 max).
            var result = new List<TopFavoriteStationDto>();
            int rank = 1;

            foreach (var item in topIds)
            {
                var station = await _stationRepo.GetByIdAsync(item.StationId, tracking: false, includeDetails: true);
                if (station != null)
                {
                    result.Add(new TopFavoriteStationDto
                    {
                        Rank = rank++,
                        StationId = station.Id,
                        Name = station.Name,
                        Address = station.Address,
                        ImageUrl = station.Images.FirstOrDefault()?.ImageUrl,
                        AverageRating = station.AverageRating,
                        TotalReviews = station.TotalReviews,
                        FavoriteCount = item.FavoriteCount
                    });
                }
            }

            return result;
        }

        public async Task<bool> CheckFavoriteAsync(int driverUserId, int stationId)
        {
            return await _favoriteRepo.ExistsAsync(driverUserId, stationId);
        }
    }
}
