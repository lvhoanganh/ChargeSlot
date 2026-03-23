using ChargeSlot.Api.Models.Identity;
using ChargeSlot.Api.DTOs.Admin;
using ChargeSlot.Api.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using ChargeSlot.Api.Services.Implementation;
using Microsoft.EntityFrameworkCore;
using ChargeSlot.Api.Constants;
using ChargeSlot.Api.Repositories.Interfaces;
namespace ChargeSlot.Api.Services.Implementation
{
    public class AdminAccountService : IAdminAccountService
    {
        private readonly IAdminAccountRepository _adminAccountRepository;

        public AdminAccountService(IAdminAccountRepository adminAccountRepository)
        {
            _adminAccountRepository = adminAccountRepository;
        }

        public async Task<PagedResultDto<AccountListItemDto>> GetAccountsAsync(
            string? search,
            string? role,
            string? status,
            int page,
            int pageSize)
        {
            page = page <= 0 ? 1 : page;
            pageSize = pageSize <= 0 ? 20 : pageSize;
            if (pageSize > 200) pageSize = 200;

            List<ApplicationUser> users;

            // 1. Lấy user theo role nếu có filter role
            if (!string.IsNullOrWhiteSpace(role))
            {
                var roleName = role.Trim();

                if (!await _adminAccountRepository.RoleExistsAsync(roleName))
                {
                    return new PagedResultDto<AccountListItemDto>
                    {
                        Page = page,
                        PageSize = pageSize,
                        TotalItems = 0,
                        Items = new List<AccountListItemDto>()
                    };
                }

                users = await _adminAccountRepository.GetUsersByRoleAsync(roleName);
            }
            else
            {
                users = await _adminAccountRepository.GetAllUsersAsync();
            }

            // 2. Filter status
            if (!string.IsNullOrWhiteSpace(status))
            {
                var statusValue = status.Trim().ToUpper();
                users = users.Where(x => x.Status.ToUpper() == statusValue).ToList();
            }

            // 3. Search fullName / phone
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim();
                users = users.Where(x =>
                    (!string.IsNullOrEmpty(x.FullName) && x.FullName.Contains(s)) ||
                    (!string.IsNullOrEmpty(x.PhoneNumber) && x.PhoneNumber.Contains(s))
                ).ToList();
            }

            // 4. Total + paging
            var total = users.Count;

            var pageUsers = users
                .OrderByDescending(x => x.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            // 5. Map DTO
            var items = new List<AccountListItemDto>();
            foreach (var u in pageUsers)
            {
                var roles = await _adminAccountRepository.GetRolesAsync(u);

                items.Add(new AccountListItemDto
                {
                    Id = u.Id,
                    FullName = u.FullName,
                    PhoneNumber = u.PhoneNumber,
                    Role = roles.FirstOrDefault(),
                    Status = u.Status,
                    CreatedAt = u.CreatedAt
                });
            }

            return new PagedResultDto<AccountListItemDto>
            {
                Page = page,
                PageSize = pageSize,
                TotalItems = total,
                Items = items
            };
        }

        public async Task<string> ToggleBanStatusAsync(int targetUserId, int actingAdminUserId)
        {
            // Không cho admin tự ban/unban chính mình
            if (targetUserId == actingAdminUserId)
                throw new InvalidOperationException("You cannot ban/unban yourself.");

            var user = await _adminAccountRepository.FindByIdAsync(targetUserId);
            if (user == null)
                throw new InvalidOperationException("User not found.");

            var roles = await _adminAccountRepository.GetRolesAsync(user);

            // Không cho ban Admin
            if (roles.Contains(RoleConstants.Admin))
                throw new InvalidOperationException("Admin accounts cannot be banned.");

            // Chỉ toggle ACTIVE <-> BANNED
            if (user.Status == UserStatusConstants.Active)
            {
                user.Status = UserStatusConstants.Banned;
            }
            else if (user.Status == UserStatusConstants.Banned)
            {
                user.Status = UserStatusConstants.Active;
            }
            else
            {
                throw new InvalidOperationException("Only ACTIVE or BANNED accounts can be toggled.");
            }

            var updated = await _adminAccountRepository.UpdateAsync(user);
            if (!updated)
                throw new InvalidOperationException("Failed to update user status.");

            return user.Status;
        }

        public async Task<AccountStatisticsDto> GetAccountStatisticsAsync()
        {
            var users = await _adminAccountRepository.GetAllUsersForStatisticsAsync();

            return new AccountStatisticsDto
            {
                TotalAccounts = users.Count,
                ActiveAccounts = users.Count(x => x.Status == UserStatusConstants.Active),
                BannedAccounts = users.Count(x => x.Status == UserStatusConstants.Banned),
                //SuspendedAccounts = users.Count(x => x.Status == UserStatusConstants.Suspended)
            };
        }
    }
}