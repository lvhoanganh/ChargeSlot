using ChargeSlot.Api.Models;

namespace ChargeSlot.Api.Repositories.Interfaces
{
    public interface ILoyaltyTransactionRepository
    {
        void Add(LoyaltyTransaction transaction);
    }
}
