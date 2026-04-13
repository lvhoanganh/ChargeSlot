using ChargeSlot.Api.DTOs.Admin;

namespace ChargeSlot.Api.Services.Interfaces
{
    public interface ISystemConfigService
    {
        Task<int> GetIntAsync(string key, int defaultValue);
        Task<decimal> GetDecimalAsync(string key, decimal defaultValue);

        Task<UpdateSystemConfigsDto> GetCurrentConfigsAsync();
        Task<PublicSystemConfigDto> GetPublicConfigsAsync();
        Task UpdateConfigsAsync(UpdateSystemConfigsDto dto, int adminUserId);
        
        Task SeedDefaultConfigsAsync();
    }
}
