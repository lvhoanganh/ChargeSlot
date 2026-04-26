using ChargeSlot.Api.Data;
using ChargeSlot.Api.Constants;
using ChargeSlot.Api.Models.Identity;
using ChargeSlot.Api.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ChargeSlot.Api.Repositories.Implementation
{
    public class UserRepository : IUserRepository
    {
        private readonly ChargeSlotDbContext _context;

        public UserRepository(ChargeSlotDbContext context)
        {
            _context = context;
        }

        public async Task<ApplicationUser?> GetByIdAsync(int userId)
        {
            return await _context.Users.FindAsync(userId);
        }

        public async Task<string?> GetFullNameAsync(int userId)
        {
            return await _context.Users
                .Where(u => u.Id == userId)
                .Select(u => u.FullName)
                .FirstOrDefaultAsync();
        }

        public async Task<List<ApplicationUser>> GetExpiredPendingVerificationAsync(DateTime cutoff)
        {
            return await _context.Users
                .Where(u => u.Status == UserStatusConstants.PendingEmailVerification
                         && u.CreatedAt < cutoff)
                .ToListAsync();
        }

        public async Task<List<ApplicationUser>> GetSuspendedWithExpiredBanAsync(DateTime now)
        {
            return await _context.Users
                .Where(u => u.Status == UserStatusConstants.Suspended
                         && u.BannedUntil != null
                         && u.BannedUntil <= now)
                .ToListAsync();
        }

        public async Task RemoveUserRolesAsync(int userId)
        {
            var userRoles = await _context.UserRoles
                .Where(ur => ur.UserId == userId)
                .ToListAsync();
            _context.UserRoles.RemoveRange(userRoles);
        }

        public void Remove(ApplicationUser user)
        {
            _context.Users.Remove(user);
        }

        public void Update(ApplicationUser user)
        {
            _context.Users.Update(user);
        }
    }
}
