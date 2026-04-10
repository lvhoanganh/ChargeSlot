using ChargeSlot.Api.Models;

namespace ChargeSlot.Api.Repositories.Interfaces
{
    public interface ILedgerTransactionRepository
    {
        void Add(LedgerTransaction transaction);
        Task<bool> HasTransactionWithMemoAsync(string memoSubstring);
        Task<(List<LedgerEntry> Items, int TotalCount)> GetAdminWalletTransactionsAsync(int walletId, ChargeSlot.Api.DTOs.Admin.Overview.TransactionFilterDto filter);
        Task<LedgerTransaction?> GetTransactionDetailAsync(long transactionId);
    }
}
