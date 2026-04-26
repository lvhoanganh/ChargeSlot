using ChargeSlot.Api.Models.Identity;

namespace ChargeSlot.Api.Repositories.Interfaces
{
    public interface IUserRepository
    {
        Task<ApplicationUser?> GetByIdAsync(int userId);
        Task<string?> GetFullNameAsync(int userId);
        Task<List<ApplicationUser>> GetExpiredPendingVerificationAsync(DateTime cutoff);
        Task<List<ApplicationUser>> GetSuspendedWithExpiredBanAsync(DateTime now);
        Task RemoveUserRolesAsync(int userId);
        void Remove(ApplicationUser user);
        void Update(ApplicationUser user);
    }
}
