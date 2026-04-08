using ChargeSlot.Api.Data;
using ChargeSlot.Api.Models;
using ChargeSlot.Api.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ChargeSlot.Api.Repositories.Implementation
{
    public class ExtraServiceRepository : IExtraServiceRepository
    {
        private readonly ChargeSlotDbContext _context;

        public ExtraServiceRepository(ChargeSlotDbContext context)
        {
            _context = context;
        }

        public async Task<List<ExtraService>> GetByStationIdAsync(int stationId)
        {
            return await _context.Set<ExtraService>()
                .Where(s => s.StationId == stationId)
                .OrderBy(s => s.ServiceName)
                .ToListAsync();
        }

        public async Task<List<ExtraService>> GetByIdsAsync(List<int> ids)
        {
            return await _context.Set<ExtraService>()
                .Where(s => ids.Contains(s.Id))
                .ToListAsync();
        }

        public async Task<ExtraService?> GetByIdAsync(int id)
        {
            return await _context.Set<ExtraService>().FindAsync(id);
        }

        public async Task<ExtraService?> GetByIdAndStationIdAsync(int id, int stationId)
        {
            return await _context.Set<ExtraService>()
                .FirstOrDefaultAsync(s => s.Id == id && s.StationId == stationId);
        }

        public async Task<bool> HasBookingsAsync(int serviceId)
        {
            return await _context.Set<BookingExtraService>()
                .AnyAsync(bes => bes.ServiceId == serviceId);
        }

        public void Add(ExtraService service)
        {
            _context.Set<ExtraService>().Add(service);
        }

        public void Update(ExtraService service)
        {
            _context.Set<ExtraService>().Update(service);
        }

        public void Remove(ExtraService service)
        {
            _context.Set<ExtraService>().Remove(service);
        }
    }
}
