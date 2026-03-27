using ChargeSlot.Api.DTOs.Wallet;

namespace ChargeSlot.Api.Services.Interfaces
{
    public interface IWalletService
    {
        Task<WalletDto> GetOrCreateWalletAsync(int userId);
        Task<string> TopUpViaVnPayAsync(int userId, decimal amount, HttpContext context);
        Task ProcessTopUpCallbackAsync(IQueryCollection query);
        Task<WalletDto> PayBookingByWalletAsync(int userId, int bookingId);
        Task<WithdrawRequestDto> WithdrawAsync(int userId, WithdrawDto dto);
        Task<List<TransactionHistoryDto>> GetTransactionHistoryAsync(int userId);

        // Withdraw requests
        Task<List<WithdrawRequestDto>> GetUserWithdrawRequestsAsync(int userId);
        Task<List<WithdrawRequestDto>> GetAllPendingWithdrawsAsync();
        Task<WithdrawRequestDto> ProcessWithdrawAsync(int adminUserId, int requestId, ProcessWithdrawDto dto);
    }
}
