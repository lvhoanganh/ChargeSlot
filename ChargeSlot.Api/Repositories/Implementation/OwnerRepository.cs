using ChargeSlot.Api.Data;
using ChargeSlot.Api.Models;
using ChargeSlot.Api.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ChargeSlot.Api.Repositories.Implementation
{
    public class OwnerRepository : IOwnerRepository
    {
        private readonly ChargeSlotDbContext _context;

        public OwnerRepository(ChargeSlotDbContext context)
        {
            _context = context;
        }

        public async Task<Owner?> GetByUserIdAsync(int userId, bool tracking = false)
        {
            var query = _context.Owner.AsQueryable();
            if (!tracking)
                query = query.AsNoTracking();

            return await query.FirstOrDefaultAsync(x => x.UserId == userId);
        }

        public async Task AddAsync(Owner owner)
        {
            await _context.Owner.AddAsync(owner);
        }

        public void Remove(Owner owner)
        {
            _context.Owner.Remove(owner);
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}

