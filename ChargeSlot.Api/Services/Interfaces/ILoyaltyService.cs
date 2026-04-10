using ChargeSlot.Api.DTOs.Loyalty;

namespace ChargeSlot.Api.Services.Interfaces
{
    public interface ILoyaltyService
    {
        Task<LoyaltyInfoDto> GetLoyaltyInfoAsync(int driverUserId);
    }
}
