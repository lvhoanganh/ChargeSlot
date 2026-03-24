
using ChargeSlot.Api.Models.Identity;

namespace ChargeSlot.Api.Repositories.Interfaces
{
    public interface IAdminAccountRepository
    {
        Task<List<ApplicationUser>> GetAllUsersAsync();
        Task<List<ApplicationUser>> GetUsersByRoleAsync(string roleName);
        Task<bool> RoleExistsAsync(string roleName);
        Task<ApplicationUser?> FindByIdAsync(int userId);
        Task<IList<string>> GetRolesAsync(ApplicationUser user);
        Task<bool> UpdateAsync(ApplicationUser user);

        Task<List<ApplicationUser>> GetAllUsersForStatisticsAsync();
    }
}