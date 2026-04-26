using ChargeSlot.Api.Models;

namespace ChargeSlot.Api.Repositories.Interfaces
{
    public interface IBankAccountRepository
    {
        Task<List<BankAccount>> GetByUserIdAsync(int userId);
        Task<(List<BankAccount> Items, int TotalCount)> GetByUserIdPagedAsync(int userId, int page, int pageSize);
        Task<BankAccount?> GetByIdAsync(int id, int userId);
        Task<List<BankAccount>> GetDefaultByUserIdAsync(int userId);
        Task<bool> HasPendingWithdrawRequestsAsync(int userId, string bankName, string accountNumber);
        void Add(BankAccount account);
        void Remove(BankAccount account);
        void Update(BankAccount account);
        Task<BankAccount?> GetByIdAsync(int id);
    }
}
