using ChargeSlot.Api.Data;
using ChargeSlot.Api.Models;
using ChargeSlot.Api.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ChargeSlot.Api.Repositories.Implementation
{
    public class StationUnavailableDateRepository : IStationUnavailableDateRepository
    {
        private readonly ChargeSlotDbContext _context;

        public StationUnavailableDateRepository(ChargeSlotDbContext context)
        {
            _context = context;
        }

        public async Task<List<DateOnly>> GetDatesByStationAndDateRangeAsync(int stationId, DateOnly startDate, DateOnly endDate)
        {
            return await _context.StationUnavailableDates
                .Where(u => u.StationId == stationId && u.Date >= startDate && u.Date <= endDate)
                .Select(u => u.Date)
                .ToListAsync();
        }

        public async Task<List<DateOnly>> GetDatesByStationAsync(int stationId)
        {
            return await _context.StationUnavailableDates
                .Where(x => x.StationId == stationId)
                .Select(x => x.Date)
                .ToListAsync();
        }

        public async Task<List<StationUnavailableDate>> GetByStationIdAsync(int stationId)
        {
            return await _context.StationUnavailableDates
                .Where(x => x.StationId == stationId)
                .OrderBy(x => x.Date)
                .ToListAsync();
        }

        public void Add(StationUnavailableDate record)
        {
            _context.StationUnavailableDates.Add(record);
        }

        public void RemoveRange(IEnumerable<StationUnavailableDate> records)
        {
            _context.StationUnavailableDates.RemoveRange(records);
        }

        public async Task<List<StationUnavailableDate>> GetByIdsAsync(int stationId, List<int> ids)
        {
            return await _context.StationUnavailableDates
                .Where(x => x.StationId == stationId && ids.Contains(x.Id))
                .ToListAsync();
        }
    }
}
