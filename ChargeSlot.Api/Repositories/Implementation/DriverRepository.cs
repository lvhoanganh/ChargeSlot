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
            var query = _context.Driver.AsQueryable();
            if (!tracking) query = query.AsNoTracking();

            return await query.FirstOrDefaultAsync(x => x.UserId == userId);
        }

        public async Task AddAsync(Driver driver)
        {
            await _context.Driver.AddAsync(driver);
        }

        public void Update(Driver driver)
        {
            _context.Driver.Update(driver);
        }

        public void Remove(Driver driver)
        {
            _context.Driver.Remove(driver);
        }
    }
}


