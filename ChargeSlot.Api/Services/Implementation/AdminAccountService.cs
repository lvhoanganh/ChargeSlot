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
        private readonly ChargeSlot.Api.Data.ChargeSlotDbContext _db;
        private readonly IBookingService _bookingService;
        private readonly INotificationService _notificationService;

        public AdminAccountService(
            IAdminAccountRepository adminAccountRepository,
            ChargeSlot.Api.Data.ChargeSlotDbContext db,
            IBookingService bookingService,
            INotificationService notificationService)
        {
            _adminAccountRepository = adminAccountRepository;
            _db = db;
            _bookingService = bookingService;
            _notificationService = notificationService;
        }

        public async Task<PagedResultDto<AccountListItemDto>> GetAccountsAsync(
            string? search,
            string? role,
            string? status,
            int page,
            int pageSize)
        {
            page = page <= 0 ? 1 : page;
            pageSize = pageSize <= 0 ? 10 : pageSize;
            if (pageSize > 100) pageSize = 100;

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
                    BanCount = u.BanCount,
                    BannedUntil = u.BannedUntil,
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

            // Chỉ toggle ACTIVE <-> BANNED / SUSPENDED
            if (user.Status == UserStatusConstants.Active)
            {
                user.Status = UserStatusConstants.Banned;
                user.BannedUntil = DateTimeHelper.VietnamNow().AddYears(100); // Vĩnh viễn mốc tượng trưng
            }
            else if (user.Status == UserStatusConstants.Banned || user.Status == UserStatusConstants.Suspended)
            {
                user.Status = UserStatusConstants.Active;
                user.BannedUntil = null;
                user.BanCount = 0; // Khi được Admin ân xá, xóa quota vi phạm
            }
            else
            {
                throw new InvalidOperationException("Only ACTIVE, SUSPENDED or BANNED accounts can be toggled.");
            }

            var updated = await _adminAccountRepository.UpdateAsync(user);
            if (!updated)
                throw new InvalidOperationException("Failed to update user status.");

            // Cascading Cancel
            if (user.Status == UserStatusConstants.Banned)
            {
                await ProcessCascadingCancelAsync(user.Id, roles.FirstOrDefault() ?? "");
            }

            return user.Status;
        }

        private async Task ProcessCascadingCancelAsync(int userId, string role)
        {
            var targetStatuses = new[] { 
                ChargeSlot.Api.Enums.BookingStatus.WaitingOwner, 
                ChargeSlot.Api.Enums.BookingStatus.PendingPayment, 
                ChargeSlot.Api.Enums.BookingStatus.Paid 
            };

            if (role == RoleConstants.Driver)
            {
                var bookings = await _db.Bookings
                    .Include(b => b.ChargingSlot).ThenInclude(s => s.ChargingStation)
                    .Where(b => b.DriverUserId == userId && targetStatuses.Contains(b.Status))
                    .ToListAsync();
                    
                foreach (var b in bookings)
                {
                    await _bookingService.CancelSystemBookingAsync(b.Id, "Tài xế bị hệ thống khóa tài khoản.");
                    
                    var ownerId = b.ChargingSlot?.ChargingStation?.OwnerUserId;
                    if (ownerId.HasValue)
                    {
                        await _notificationService.SendAsync(
                            ownerId.Value, 
                            "Lịch đặt đã bị hủy", 
                            $"Tài xế đặt slot {b.ChargingSlot?.SlotName} tại trạm {b.ChargingSlot?.ChargingStation?.Name} vừa bị khóa tài khoản hệ thống. Lịch đã tự động hủy.", 
                            ChargeSlot.Api.Enums.NotificationType.System);
                    }
                }
            }
            else if (role == RoleConstants.Owner)
            {
                var stations = await _db.ChargingStations
                    .Where(s => s.OwnerUserId == userId)
                    .ToListAsync();
                    
                foreach(var s in stations)
                {
                    s.OperationalStatus = ChargeSlot.Api.Enums.OperationalStatus.Inactive;
                }
                await _db.SaveChangesAsync();

                var stationIds = stations.Select(s => s.Id).ToList();
                var bookings = await _db.Bookings
                    .Include(b => b.ChargingSlot).ThenInclude(s => s.ChargingStation)
                    .Where(b => stationIds.Contains(b.ChargingSlot!.StationId) && targetStatuses.Contains(b.Status))
                    .ToListAsync();
                    
                foreach (var b in bookings)
                {
                    await _bookingService.CancelSystemBookingAsync(b.Id, "Trạm sạc bị hệ thống khóa do vi phạm.");
                    
                    await _notificationService.SendAsync(
                        b.DriverUserId, 
                        "Lịch đặt đã bị hủy hệ thống", 
                        $"Trạm sạc {b.ChargingSlot?.ChargingStation?.Name} do vi phạm nên đã bị hệ thống khóa. Lịch đặt của bạn tại slot {b.ChargingSlot?.SlotName} bị hủy, tiền cọc (nếu có) đã hoàn 100% vào ví.", 
                        ChargeSlot.Api.Enums.NotificationType.System);
                }
            }
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