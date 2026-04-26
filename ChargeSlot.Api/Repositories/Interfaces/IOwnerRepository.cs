using ChargeSlot.Api.Models;

namespace ChargeSlot.Api.Repositories.Interfaces
{
    public interface IOwnerRepository
    {
        Task<Owner?> GetByUserIdAsync(int userId, bool tracking = false);
        Task<List<Owner>> GetPendingKycAsync();
        Task<List<Owner>> GetAllKycsAsync(string? status = null);
        Task AddAsync(Owner owner);
        void Update(Owner owner);
        void Remove(Owner owner);
    }
}


