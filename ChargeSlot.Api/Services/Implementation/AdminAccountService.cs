using ChargeSlot.Api.Models.Identity;
using ChargeSlot.Api.DTOs.Admin;
using ChargeSlot.Api.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using ChargeSlot.Api.Services.Implementation;
using Microsoft.EntityFrameworkCore;
using ChargeSlot.Api.Helpers;
using ChargeSlot.Api.Constants;
namespace ChargeSlot.Api.Services.Implementation
{
    public class AdminAccountService : IAdminAccountService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole<int>> _roleManager;

        public AdminAccountService(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole<int>> roleManager)
        {
            _userManager = userManager;
            _roleManager = roleManager;
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

                if (!await _roleManager.RoleExistsAsync(roleName))
                {
                    return new PagedResultDto<AccountListItemDto>
                    {
                        Page = page,
                        PageSize = pageSize,
                        TotalItems = 0,
                        Items = new List<AccountListItemDto>()
                    };
                }

                users = (await _userManager.GetUsersInRoleAsync(roleName)).ToList();
            }
            else
            {
                users = await _userManager.Users
                    .AsNoTracking()
                    .OrderByDescending(x => x.CreatedAt)
                    .ToListAsync();
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
                var s = search.Trim().ToLower();
                users = users.Where(x =>
                    (!string.IsNullOrEmpty(x.FullName) && x.FullName.ToLower().Contains(s)) ||
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
                var roles = await _userManager.GetRolesAsync(u);

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

            var user = await _userManager.FindByIdAsync(targetUserId.ToString());
            if (user == null)
                throw new InvalidOperationException("User not found.");

            var roles = await _userManager.GetRolesAsync(user);

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

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                var msg = string.Join("; ", result.Errors.Select(e => e.Description));
                throw new InvalidOperationException(msg);
            }

            return user.Status;
        }
    }
}



//    public async Task<PagedResultDto<AccountListItemDto>> GetAccountsAsync(
//string? search, string? role, bool? isActive, int page, int pageSize)
//    {
//        // Fix các thông số paging cơ bản
//        page = page <= 0 ? 1 : page;
//        pageSize = pageSize <= 0 ? 20 : pageSize;

//        // TẠO DỮ LIỆU GIẢ TẠI ĐÂY
//        var items = new List<AccountListItemDto>
//{
//    new AccountListItemDto {
//        Id = 1,
//        FullName = "Nguyễn Văn Admin",
//        PhoneNumber = "0901234567",
//        Role = "Admin",
//        IsActive = true,
//        CreatedAt = DateTime.Now
//    },
//    new AccountListItemDto {
//        Id = 2,
//        FullName = "Trần Thị Staff",
//        PhoneNumber = "0988888888",
//        Role = "Driver",
//        IsActive = false,
//        CreatedAt = DateTime.Now.AddDays(-1)
//    }
//};
//        if (!string.IsNullOrWhiteSpace(search))
//        {
//            var s = search.Trim().ToLower(); // Viết thường để search không phân biệt hoa thường
//            items = items.Where(x =>
//                x.FullName.ToLower().Contains(s) ||
//                x.PhoneNumber != null && x.PhoneNumber.Contains(s)
//            ).ToList();
//        }

//        // 3. CHÈN THÊM LOGIC FILTER ROLE (Nếu muốn test nút lọc Role)
//        if (!string.IsNullOrWhiteSpace(role))
//        {
//            items = items.Where(x => x.Role == role).ToList();
//        }

//        // Trả về kết quả luôn, không gọi tới _userManager hay _context
//        return new PagedResultDto<AccountListItemDto>
//        {
//            Page = page,
//            PageSize = pageSize,
//            TotalItems = items.Count,
//            Items = items
//        };
//    }