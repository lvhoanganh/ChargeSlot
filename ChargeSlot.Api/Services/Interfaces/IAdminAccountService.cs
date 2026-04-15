using ChargeSlot.Api.DTOs.Admin;

namespace ChargeSlot.Api.Services.Interfaces
{
    public interface IAdminAccountService
    {
        Task<PagedResultDto<AccountListItemDto>> GetAccountsAsync(
            string? search,
            string? role,
            string? status,
            int page,
            int pageSize);

        Task<AdminOwnerDetailDto> GetOwnerDetailAsync(int ownerUserId);
        Task<AdminDriverDetailDto> GetDriverDetailAsync(int driverUserId);

        Task<string> ToggleBanStatusAsync(int targetUserId, int actingAdminUserId, string? reason);

        Task<AccountStatisticsDto> GetAccountStatisticsAsync();

        Task SetupSecondaryPasswordAsync(int adminUserId, SetupSecondaryPasswordDto dto);
        Task RequestResetSecondaryPasswordAsync(int adminUserId);
        Task ConfirmResetSecondaryPasswordAsync(int adminUserId, ConfirmResetSecondaryPasswordDto dto);
    }
}