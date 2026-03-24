using ChargeSlot.Api.Models;

namespace ChargeSlot.Api.Repositories.Interfaces
{
    public interface IWalletRepository
    {
        Task<Wallet?> GetByIdAsync(int id);
        Task<Wallet?> GetByUserIdAsync(int userId);
        Task<Wallet> CreateAsync(Wallet wallet);
        Task UpdateAsync(Wallet wallet);
        Task<List<LedgerEntry>> GetTransactionHistoryAsync(int walletId, int take = 50);
        Task AddLedgerTransactionAsync(LedgerTransaction transaction);
    }
}
