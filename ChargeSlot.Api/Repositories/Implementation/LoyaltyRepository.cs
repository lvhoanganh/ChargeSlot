using ChargeSlot.Api.Data;
using ChargeSlot.Api.Models;
using ChargeSlot.Api.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ChargeSlot.Api.Repositories.Implementation
{
    public class LoyaltyRepository : ILoyaltyRepository
    {
        private readonly ChargeSlotDbContext _context;

        public LoyaltyRepository(ChargeSlotDbContext context)
        {
            _context = context;
        }

        public async Task<List<LoyaltyTransaction>> GetRecentHistoryAsync(int driverUserId, int take = 20)
        {
            return await _context.LoyaltyTransactions
                .Where(t => t.DriverUserId == driverUserId)
                .OrderByDescending(t => t.CreatedAt)
                .Take(take)
                .ToListAsync();
        }

        public void AddTransaction(LoyaltyTransaction transaction)
        {
            _context.LoyaltyTransactions.Add(transaction);
        }
    }
}
