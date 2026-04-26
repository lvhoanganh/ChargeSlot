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

        /// <summary>Atomic SQL: cộng/trừ AvailableBalance và FrozenBalance trong 1 lệnh UPDATE.</summary>
        Task<int> AdjustBalanceAtomicAsync(int walletId, decimal availableDelta, decimal frozenDelta);

        /// <summary>Atomic SQL: chuyển tiền AvailableBalance từ source → dest.</summary>
        Task TransferAtomicAsync(int sourceWalletId, int destWalletId, decimal amount);

        /// <summary>Atomic SQL: chuyển tiền từ FrozenBalance → AvailableBalance trong cùng 1 ví.</summary>
        Task UnfreezeAtomicAsync(int walletId, decimal amount);

        /// <summary>Atomic SQL: trừ AvailableBalance chỉ khi đủ tiền. Return rows affected (0 = không đủ).</summary>
        Task<int> DeductIfSufficientAsync(int walletId, decimal amount);

        /// <summary>Atomic SQL: freeze (Available → Frozen) chỉ khi đủ tiền. Return rows affected (0 = không đủ).</summary>
        Task<int> FreezeIfSufficientAsync(int walletId, decimal amount);
    }
}

