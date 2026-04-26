using ChargeSlot.Api.Data;
using ChargeSlot.Api.Models;
using ChargeSlot.Api.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ChargeSlot.Api.Repositories.Implementation
{
    public class StationPricingRepository : IStationPricingRepository
    {
        private readonly ChargeSlotDbContext _context;

        public StationPricingRepository(ChargeSlotDbContext context)
        {
            _context = context;
        }

        public async Task<List<StationPricing>> GetByStationIdAsync(int stationId)
        {
            return await _context.StationPricings
                .Where(p => p.StationId == stationId)
                .OrderBy(p => p.StartTime)
                .ToListAsync();
        }

        public async Task<List<StationPricing>> GetActiveByStationIdAsync(int stationId)
        {
            return await _context.StationPricings
                .Where(p => p.StationId == stationId && p.IsActive)
                .OrderByDescending(p => p.Priority)
                .ThenBy(p => p.StartTime)
                .ToListAsync();
        }

        public async Task<StationPricing?> GetByIdAsync(int id, int stationId)
        {
            return await _context.StationPricings
                .FirstOrDefaultAsync(p => p.Id == id && p.StationId == stationId);
        }

        public void Add(StationPricing pricing)
        {
            _context.StationPricings.Add(pricing);
        }

        public void Update(StationPricing pricing)
        {
            _context.StationPricings.Update(pricing);
        }

        public void Remove(StationPricing pricing)
        {
            _context.StationPricings.Remove(pricing);
        }
    }
}
