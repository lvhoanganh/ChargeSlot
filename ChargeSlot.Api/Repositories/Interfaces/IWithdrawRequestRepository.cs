using ChargeSlot.Api.Models;

namespace ChargeSlot.Api.Repositories.Interfaces
{
    public interface IWithdrawRequestRepository
    {
        Task<bool> HasPendingRequestsAsync(int userId, string bankAccountNumber, string bankName);
        void Add(WithdrawRequest request);
        void Update(WithdrawRequest request);
        Task<List<WithdrawRequest>> GetByUserIdAsync(int userId);
        Task<(List<WithdrawRequest> Items, int TotalCount)> GetByUserIdPagedAsync(int userId, int page, int pageSize);
        Task<List<WithdrawRequest>> GetPendingAsync();
        Task<(List<WithdrawRequest> Items, int TotalCount)> GetPendingPagedAsync(int page, int pageSize);
        Task<WithdrawRequest?> GetByIdWithUserAndWalletAsync(int id);
        Task<WithdrawRequest?> GetByIdWithUserAsync(int id);
        Task<List<WithdrawRequest>> GetAllWithUserAsync();
        Task<(List<WithdrawRequest> Items, int TotalCount)> GetAllWithUserPagedAsync(int page, int pageSize);
        Task<List<int>> GetExpiredTransferCompletedIdsAsync(DateTime deadline);
        Task<List<WithdrawRequest>> GetTransferCompletedForReminderAsync(DateTime reminderStart, DateTime reminderEnd);
    }
}
