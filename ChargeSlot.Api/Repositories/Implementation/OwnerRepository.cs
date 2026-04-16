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

        public async Task<List<Owner>> GetPendingKycAsync()
        {
            return await _context.Owner
                .Where(o => o.KycStatus == Enums.KycStatus.Pending || o.KycStatus == Enums.KycStatus.PendingUpdate)
                .OrderBy(o => o.KycSubmittedAt)
                .ToListAsync();
        }

        public async Task<(List<Owner> Items, int TotalCount)> GetPendingKycPagedAsync(int page, int pageSize)
        {
            var query = _context.Owner
                .Where(o => o.KycStatus == Enums.KycStatus.Pending || o.KycStatus == Enums.KycStatus.PendingUpdate);

            int totalCount = await query.CountAsync();
            var items = await query.OrderBy(o => o.KycSubmittedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            return (items, totalCount);
        }

        public async Task<List<Owner>> GetAllKycsAsync(string? status = null)
        {
            var query = _context.Owner.AsQueryable();

            if (!string.IsNullOrEmpty(status) && Enum.TryParse<Enums.KycStatus>(status, true, out var parsed))
            {
                query = query.Where(o => o.KycStatus == parsed);
            }

            return await query.OrderByDescending(o => o.CreatedAt).ToListAsync();
        }

        public async Task<(List<Owner> Items, int TotalCount)> GetAllKycsPagedAsync(string? status, int page, int pageSize)
        {
            var query = _context.Owner.AsQueryable();

            if (!string.IsNullOrEmpty(status) && Enum.TryParse<Enums.KycStatus>(status, true, out var parsed))
            {
                query = query.Where(o => o.KycStatus == parsed);
            }

            int totalCount = await query.CountAsync();
            var items = await query.OrderByDescending(o => o.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            return (items, totalCount);
        }

        public void Update(Owner owner)
        {
            _context.Owner.Update(owner);
        }

        public void Remove(Owner owner)
        {
            _context.Owner.Remove(owner);
        }

        public async Task<int> CountAsync()
        {
            return await _context.Owner.CountAsync();
        }
    }
}


