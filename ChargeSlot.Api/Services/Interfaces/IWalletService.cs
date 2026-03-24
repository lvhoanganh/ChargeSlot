using ChargeSlot.Api.DTOs.Wallet;

namespace ChargeSlot.Api.Services.Interfaces
{
    public interface IWalletService
    {
        Task<WalletDto> GetOrCreateWalletAsync(int userId);
        Task<string> TopUpViaVnPayAsync(int userId, decimal amount, HttpContext context);
        Task ProcessTopUpCallbackAsync(IQueryCollection query);
        Task<WalletDto> PayBookingByWalletAsync(int userId, int bookingId);
        Task<WalletDto> WithdrawAsync(int userId, decimal amount);
        Task<List<TransactionHistoryDto>> GetTransactionHistoryAsync(int userId);
    }
}
