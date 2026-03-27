using ChargeSlot.Api.DTOs.Payout;

namespace ChargeSlot.Api.Services.Interfaces
{
    public interface IPayoutService
    {
        Task<PayoutRequestDto> CreatePayoutAsync(int ownerUserId, CreatePayoutDto dto);
        Task<List<PayoutRequestDto>> GetByOwnerAsync(int ownerUserId);
        Task<List<PayoutRequestDto>> GetAllPendingAsync();
        Task<PayoutRequestDto> ProcessPayoutAsync(int adminUserId, int requestId, ProcessPayoutDto dto);
    }
}
