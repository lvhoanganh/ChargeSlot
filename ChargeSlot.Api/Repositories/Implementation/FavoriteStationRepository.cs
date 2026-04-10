using ChargeSlot.Api.Data;
using ChargeSlot.Api.Models;
using ChargeSlot.Api.Enums;
using ChargeSlot.Api.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ChargeSlot.Api.Repositories.Implementation
{
    public class FavoriteStationRepository : IFavoriteStationRepository
    {
        private readonly ChargeSlotDbContext _context;

        public FavoriteStationRepository(ChargeSlotDbContext context)
        {
            _context = context;
        }

        public async Task<FavoriteStation?> GetAsync(int driverUserId, int stationId)
        {
            return await _context.FavoriteStations
                .FirstOrDefaultAsync(f => f.DriverUserId == driverUserId && f.StationId == stationId);
        }

        public async Task<bool> ExistsAsync(int driverUserId, int stationId)
        {
            return await _context.FavoriteStations
                .AnyAsync(f => f.DriverUserId == driverUserId && f.StationId == stationId);
        }

        public async Task<List<FavoriteStation>> GetByDriverAsync(int driverUserId)
        {
            return await _context.FavoriteStations
                .Include(f => f.Station)
                    .ThenInclude(s => s.Images)
                .Where(f => f.DriverUserId == driverUserId)
                .OrderByDescending(f => f.CreatedAt)
                .ToListAsync();
        }

        public async Task<List<(int StationId, int FavoriteCount)>> GetTopFavoriteStationIdsAsync(int limit)
        {
            var topStations = await _context.FavoriteStations
                .Where(f => f.Station.ApprovalStatus == ApprovalStatus.Approved
                    && f.Station.OperationalStatus == OperationalStatus.Active)
                .GroupBy(f => f.StationId)
                .Select(g => new
                {
                    StationId = g.Key,
                    FavoriteCount = g.Count()
                })
                .OrderByDescending(x => x.FavoriteCount)
                .Take(limit)
                .ToListAsync();

            return topStations.Select(x => (x.StationId, x.FavoriteCount)).ToList();
        }

        public void Add(FavoriteStation favoriteStation)
        {
            _context.FavoriteStations.Add(favoriteStation);
        }

        public void Remove(FavoriteStation favoriteStation)
        {
            _context.FavoriteStations.Remove(favoriteStation);
        }
    }
}
