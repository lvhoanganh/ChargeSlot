using ChargeSlot.Api.DTOs.Wallet;

namespace ChargeSlot.Api.Services.Interfaces
{
    public interface IWalletService
    {
        Task<WalletDto> GetOrCreateWalletAsync(int userId);
        Task<string> GetSePayTopUpQrUrlAsync(int userId, decimal amount);
        Task<WalletDto> PayBookingByWalletAsync(int userId, int bookingId);
        Task<WithdrawRequestDto> WithdrawAsync(int userId, WithdrawDto dto);
        Task<List<TransactionHistoryDto>> GetTransactionHistoryAsync(int userId);

        // Withdraw requests — queries
        Task<List<WithdrawRequestDto>> GetUserWithdrawRequestsAsync(int userId);
        Task<List<WithdrawRequestDto>> GetAllPendingWithdrawsAsync();
        Task<List<WithdrawRequestDto>> GetAllWithdrawsAsync();
        Task<WithdrawRequestDto> ProcessWithdrawAsync(int adminUserId, int requestId, ProcessWithdrawDto dto);

        // Withdraw multi-step flow
        Task<WithdrawRequestDto> ConfirmTransferAsync(int adminUserId, int requestId, IFormFile receiptImage);
        Task<WithdrawRequestDto> UserConfirmReceivedAsync(int userId, int requestId);
        Task<WithdrawRequestDto> UserReportIssueAsync(int userId, int requestId, string issueNote);
        Task<WithdrawRequestDto> AdminResolveIssueAsync(int adminUserId, int requestId, bool refund, string? note);

        // Finalize withdraw: trừ frozen + ghi ledger (dùng bởi WithdrawAutoConfirmJob)
        Task FinalizeWithdrawCompletedAsync(Models.WithdrawRequest request, int? confirmedByUserId = null);

        // Admin Overview
        Task<ChargeSlot.Api.DTOs.Admin.Overview.PagedResultDto<WalletDto>> GetAdminAllWalletsAsync(ChargeSlot.Api.DTOs.Admin.Overview.WalletFilterDto filter);
        Task<ChargeSlot.Api.DTOs.Admin.Overview.PagedResultDto<TransactionHistoryDto>> GetAdminWalletTransactionsAsync(int walletId, ChargeSlot.Api.DTOs.Admin.Overview.TransactionFilterDto filter);
    }
}
