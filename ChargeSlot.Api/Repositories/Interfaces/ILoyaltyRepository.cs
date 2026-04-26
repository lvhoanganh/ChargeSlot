using ChargeSlot.Api.Models;

namespace ChargeSlot.Api.Repositories.Interfaces
{
    public interface ILoyaltyRepository
    {
        Task<List<LoyaltyTransaction>> GetRecentHistoryAsync(int driverUserId, int take = 20);
        void AddTransaction(LoyaltyTransaction transaction);
    }
}
