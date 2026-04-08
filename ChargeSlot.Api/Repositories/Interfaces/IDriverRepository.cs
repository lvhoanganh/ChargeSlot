using ChargeSlot.Api.Models;

namespace ChargeSlot.Api.Repositories.Interfaces
{
    public interface IDriverRepository
    {
        Task<Driver?> GetByUserIdAsync(int userId, bool tracking = false);
        Task AddAsync(Driver driver);
        void Remove(Driver driver);
    }
}


