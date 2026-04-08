using ChargeSlot.Api.Data;
using ChargeSlot.Api.Models;
using ChargeSlot.Api.Repositories.Interfaces;

namespace ChargeSlot.Api.Repositories.Implementation
{
    public class LoyaltyTransactionRepository : ILoyaltyTransactionRepository
    {
        private readonly ChargeSlotDbContext _context;

        public LoyaltyTransactionRepository(ChargeSlotDbContext context)
        {
            _context = context;
        }

        public void Add(LoyaltyTransaction transaction)
        {
            _context.LoyaltyTransactions.Add(transaction);
        }
    }
}
