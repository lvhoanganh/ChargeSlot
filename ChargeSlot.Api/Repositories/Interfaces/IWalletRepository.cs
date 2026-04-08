using ChargeSlot.Api.Models;

namespace ChargeSlot.Api.Repositories.Interfaces
{
    public interface IWalletRepository
    {
        Task<Wallet?> GetByIdAsync(int id);
        Task<Wallet?> GetByUserIdAsync(int userId);
        Task<Wallet?> GetBySystemCodeAsync(string systemCode);
        void Add(Wallet wallet);
        void Update(Wallet wallet);
        Task<List<LedgerEntry>> GetTransactionHistoryAsync(int walletId, int take = 50);
        void AddLedgerTransaction(LedgerTransaction transaction);
        Task<(List<Wallet> Items, int TotalCount)> GetAdminAllWalletsAsync(ChargeSlot.Api.DTOs.Admin.Overview.WalletFilterDto filter);
    }
}

