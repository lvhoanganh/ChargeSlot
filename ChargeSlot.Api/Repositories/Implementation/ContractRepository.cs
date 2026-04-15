using ChargeSlot.Api.Data;
using ChargeSlot.Api.Enums;
using ChargeSlot.Api.Helpers;
using ChargeSlot.Api.Models;
using ChargeSlot.Api.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ChargeSlot.Api.Repositories.Implementation
{
    public class ContractRepository : IContractRepository
    {
        private readonly ChargeSlotDbContext _context;

        public ContractRepository(ChargeSlotDbContext context)
        {
            _context = context;
        }

        public async Task<Contract?> GetByOwnerAsync(int ownerUserId)
        {
            return await _context.Contracts
                .Where(c => c.OwnerUserId == ownerUserId)
                .OrderByDescending(c => c.CreatedAt)
                .FirstOrDefaultAsync();
        }

        public async Task<Contract?> GetByIdAsync(int id)
        {
            return await _context.Contracts
                .FirstOrDefaultAsync(c => c.Id == id);
        }

        public async Task<List<Contract>> GetAllAsync(ContractStatus? status = null)
        {
            var query = _context.Contracts.AsQueryable();

            if (status.HasValue)
                query = query.Where(c => c.Status == status.Value);

            return await query.OrderByDescending(c => c.CreatedAt).ToListAsync();
        }

        public async Task<(List<Contract> Items, int TotalCount)> GetAllPagedAsync(ContractStatus? status, int page, int pageSize)
        {
            var query = _context.Contracts.AsQueryable();

            if (status.HasValue)
                query = query.Where(c => c.Status == status.Value);

            int totalCount = await query.CountAsync();
            var items = await query.OrderByDescending(c => c.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            return (items, totalCount);
        }

        public async Task<int> GetMaxIdAsync()
        {
            var any = await _context.Contracts.AnyAsync();
            if (!any) return 0;
            return await _context.Contracts.MaxAsync(c => c.Id);
        }

        public async Task AddAsync(Contract contract)
        {
            await _context.Contracts.AddAsync(contract);
        }

        public void Update(Contract contract)
        {
            _context.Contracts.Update(contract);
        }

        public async Task<List<Contract>> GetExpiringAsync(DateTime deadline)
        {
            var now = DateTimeHelper.VietnamNow();
            return await _context.Contracts
                .Where(c => c.Status == ContractStatus.Signed
                    && c.ExpiresAt.HasValue
                    && c.ExpiresAt.Value <= deadline
                    && c.ExpiresAt.Value > now
                    && c.RenewalNotifiedAt == null)
                .ToListAsync();
        }

        public async Task<List<Contract>> GetExpiredSignedAsync(DateTime now)
        {
            return await _context.Contracts
                .Where(c => c.Status == ContractStatus.Signed
                    && c.ExpiresAt.HasValue
                    && c.ExpiresAt.Value <= now)
                .ToListAsync();
        }
    }
}
