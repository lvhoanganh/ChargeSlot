   using ChargeSlot.Api.Enums;
using ChargeSlot.Api.Models;

namespace ChargeSlot.Api.Repositories.Interfaces
{
    public interface IContractRepository
    {
        Task<Contract?> GetByOwnerAsync(int ownerUserId);
        Task<Contract?> GetByIdAsync(int id);
        Task<List<Contract>> GetAllAsync(ContractStatus? status = null);
        Task<(List<Contract> Items, int TotalCount)> GetAllPagedAsync(ContractStatus? status, int page, int pageSize);
        Task<(List<Contract> Items, int TotalCount)> GetAllFilteredPagedAsync(DTOs.Contract.ContractFilterDto filter);
        Task<int> GetMaxIdAsync();
        Task AddAsync(Contract contract);
        void Update(Contract contract);

        /// <summary>Contracts expiring within next N days that haven't been notified yet.</summary>
        Task<List<Contract>> GetExpiringAsync(DateTime deadline);

        /// <summary>Signed contracts that have expired (ExpiresAt <= now).</summary>
        Task<List<Contract>> GetExpiredSignedAsync(DateTime now);
    }
}
