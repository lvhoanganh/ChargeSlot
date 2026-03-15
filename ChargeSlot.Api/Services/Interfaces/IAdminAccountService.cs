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

        Task<string> ToggleBanStatusAsync(int targetUserId, int actingAdminUserId);

        Task<AccountStatisticsDto> GetAccountStatisticsAsync();
    }
}