using ChargeSlot.Api.DTOs.BankAccount;

namespace ChargeSlot.Api.Services.Interfaces
{
    public interface IBankAccountService
    {
        Task<List<BankAccountDto>> GetMyBankAccountsAsync(int userId);
        Task<ChargeSlot.Api.DTOs.PagedResultDto<BankAccountDto>> GetMyBankAccountsPagedAsync(int userId, int page, int pageSize);
        Task<BankAccountDto> CreateAsync(int userId, CreateBankAccountDto dto);
        Task SetDefaultAsync(int id, int userId);
        Task DeleteAsync(int id, int userId);
    }
}
