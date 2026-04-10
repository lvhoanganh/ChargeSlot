using ChargeSlot.Api.Data;
using ChargeSlot.Api.Models;
using ChargeSlot.Api.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ChargeSlot.Api.Repositories.Implementation
{
    public class RatingRepository : IRatingRepository
    {
        private readonly ChargeSlotDbContext _context;

        public RatingRepository(ChargeSlotDbContext context)
        {
            _context = context;
        }

        public async Task<bool> HasRatingForBookingAsync(int bookingId)
        {
            return await _context.Ratings.AnyAsync(r => r.BookingId == bookingId);
        }

        public void Add(Rating rating)
        {
            _context.Ratings.Add(rating);
        }

        public async Task<Rating?> GetByIdWithStationAsync(int id)
        {
            return await _context.Ratings
                .Include(r => r.ChargingStation)
                .FirstOrDefaultAsync(r => r.Id == id);
        }

        public async Task<Rating?> GetByIdWithDetailsAsync(int id)
        {
            return await _context.Ratings
                .Include(r => r.DriverUser)
                .FirstOrDefaultAsync(r => r.Id == id);
        }

        public async Task<(List<Rating> Items, int TotalCount)> GetRatingsByStationPagedAsync(int stationId, int page, int pageSize)
        {
            var query = _context.Ratings
                .Include(r => r.DriverUser)
                .Where(r => r.StationId == stationId);

            var totalCount = await query.CountAsync();

            var items = await query
                .OrderByDescending(r => r.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, totalCount);
        }

        public async Task<Dictionary<int, int>> GetRatingCountsByStationAsync(int stationId)
        {
            var groups = await _context.Ratings
                .Where(r => r.StationId == stationId)
                .GroupBy(r => r.Score)
                .Select(g => new { Score = g.Key, Count = g.Count() })
                .ToListAsync();

            return groups.ToDictionary(r => r.Score, r => r.Count);
        }

        public async Task<(decimal Average, int Count)?> GetRatingStatsAsync(int stationId)
        {
            var stats = await _context.Ratings
                .Where(r => r.StationId == stationId)
                .GroupBy(r => r.StationId)
                .Select(g => new { Avg = g.Average(r => (decimal)r.Score), Count = g.Count() })
                .FirstOrDefaultAsync();

            if (stats == null) return null;
            return ((decimal)stats.Avg, stats.Count);
        }
    }
}
