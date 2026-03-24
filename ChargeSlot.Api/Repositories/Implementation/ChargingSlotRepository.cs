using ChargeSlot.Api.Data;
using ChargeSlot.Api.Models;
using ChargeSlot.Api.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ChargeSlot.Api.Repositories.Implementation
{
    public class ChargingSlotRepository : IChargingSlotRepository
    {
        private readonly ChargeSlotDbContext _context;

        public ChargingSlotRepository(ChargeSlotDbContext context)
        {
            _context = context;
        }

        public async Task<ChargingSlot?> GetByIdAsync(int id, bool tracking = false)
        {
            var query = _context.ChargingSlots.Include(s => s.ChargingStation).AsQueryable();

            if (!tracking)
                query = query.AsNoTracking();

            return await query.FirstOrDefaultAsync(s => s.Id == id);
        }

        public async Task<List<ChargingSlot>> GetAllByStationAsync(int stationId)
        {
            return await _context.ChargingSlots
                .AsNoTracking()
                .Where(s => s.StationId == stationId)
                .OrderBy(s => s.SlotName)
                .ToListAsync();
        }

        public async Task AddAsync(ChargingSlot slot)
        {
            await _context.ChargingSlots.AddAsync(slot);
        }

        public void Update(ChargingSlot slot)
        {
            _context.ChargingSlots.Update(slot);
        }

        public void Remove(ChargingSlot slot)
        {
            _context.ChargingSlots.Remove(slot);
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}
