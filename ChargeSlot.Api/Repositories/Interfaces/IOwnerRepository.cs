using ChargeSlot.Api.Models;

namespace ChargeSlot.Api.Repositories.Interfaces
{
    public interface IOwnerRepository
    {
        Task<Owner?> GetByUserIdAsync(int userId, bool tracking = false);
        Task AddAsync(Owner owner);
        void Remove(Owner owner);
        Task SaveChangesAsync();
    }
}

