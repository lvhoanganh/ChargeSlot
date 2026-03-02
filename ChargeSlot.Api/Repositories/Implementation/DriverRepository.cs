using ChargeSlot.Api.Data;
using ChargeSlot.Api.Models;
using ChargeSlot.Api.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ChargeSlot.Api.Repositories.Implementation
{
    public class DriverRepository : IDriverRepository
    {
        private readonly ChargeSlotDbContext _context;

        public DriverRepository(ChargeSlotDbContext context)
        {
            _context = context;
        }

        public async Task<Driver?> GetByUserIdAsync(int userId, bool tracking = false)
        {
            var query = _context.Drivers.AsQueryable();
            if (!tracking)
                query = query.AsNoTracking();

            return await query.FirstOrDefaultAsync(x => x.UserId == userId);
        }

        public async Task AddAsync(Driver driver)
        {
            await _context.Drivers.AddAsync(driver);
        }

        public void Remove(Driver driver)
        {
            _context.Drivers.Remove(driver);
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}

