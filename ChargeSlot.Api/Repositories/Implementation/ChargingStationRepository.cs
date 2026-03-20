using ChargeSlot.Api.Data;
using ChargeSlot.Api.Enums;
using ChargeSlot.Api.Models;
using ChargeSlot.Api.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ChargeSlot.Api.Repositories.Implementation
{
    public class ChargingStationRepository : IChargingStationRepository
    {
        private readonly ChargeSlotDbContext _context;

        public ChargingStationRepository(ChargeSlotDbContext context)
        {
            _context = context;
        }

        public async Task<ChargingStation?> GetByIdAsync(int id, bool tracking = false, bool includeDetails = true)
        {
            var query = _context.ChargingStations.AsQueryable();

            if (!tracking)
                query = query.AsNoTracking();

            if (includeDetails)
            {
                query = query
                    .Include(s => s.Images)
                    .Include(s => s.OperatingHours)
                    .Include(s => s.ChargingSlots)
                        .ThenInclude(slot => slot.SlotPricings);
            }

            return await query.FirstOrDefaultAsync(s => s.Id == id);
        }

        public async Task<List<ChargingStation>> GetAllByOwnerAsync(int ownerUserId)
        {
            return await _context.ChargingStations
                .AsNoTracking()
                .Include(s => s.Images)
                .Include(s => s.OperatingHours)
                .Include(s => s.ChargingSlots)
                    .ThenInclude(slot => slot.SlotPricings)
                .Where(s => s.OwnerUserId == ownerUserId)
                .OrderByDescending(s => s.CreatedAt)
                .ToListAsync();
        }

        public async Task<List<ChargingStation>> GetByApprovalStatusAsync(ApprovalStatus status)
        {
            return await _context.ChargingStations
                .AsNoTracking()
                .Include(s => s.Images)
                .Include(s => s.OperatingHours)
                .Include(s => s.ChargingSlots)
                    .ThenInclude(slot => slot.SlotPricings)
                .Where(s => s.ApprovalStatus == status)
                .OrderByDescending(s => s.CreatedAt)
                .ToListAsync();
        }

        public async Task AddAsync(ChargingStation station)
        {
            await _context.ChargingStations.AddAsync(station);
        }

        public void Update(ChargingStation station)
        {
            _context.ChargingStations.Update(station);
        }

        public void Remove(ChargingStation station)
        {
            _context.ChargingStations.Remove(station);
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}
