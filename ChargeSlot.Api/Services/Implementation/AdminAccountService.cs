using ChargeSlot.Api.Models.Identity;
using ChargeSlot.Api.DTOs.Admin;
using ChargeSlot.Api.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using ChargeSlot.Api.Services.Implementation;
using Microsoft.EntityFrameworkCore;
using ChargeSlot.Api.Constants;
using ChargeSlot.Api.Repositories.Interfaces;
using ChargeSlot.Api.Helpers;

namespace ChargeSlot.Api.Services.Implementation
{
    public class AdminAccountService : IAdminAccountService
    {
        private readonly IAdminAccountRepository _adminAccountRepository;
        private readonly ChargeSlot.Api.Data.ChargeSlotDbContext _db;
        private readonly IBookingService _bookingService;
        private readonly INotificationService _notificationService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IEmailService _emailService;
        private readonly IUserOtpRepository _otpRepository;

        public AdminAccountService(
            IAdminAccountRepository adminAccountRepository,
            ChargeSlot.Api.Data.ChargeSlotDbContext db,
            IBookingService bookingService,
            INotificationService notificationService,
            UserManager<ApplicationUser> userManager,
            IEmailService emailService,
            IUserOtpRepository otpRepository)
        {
            _adminAccountRepository = adminAccountRepository;
            _db = db;
            _bookingService = bookingService;
            _notificationService = notificationService;
            _userManager = userManager;
            _emailService = emailService;
            _otpRepository = otpRepository;
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
                user.BannedUntil = null; // Vĩnh viễn — đồng nhất với DisputeService (UnbanAutoJob chỉ xử lý SUSPENDED)
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

        public async Task SetupSecondaryPasswordAsync(int adminUserId, SetupSecondaryPasswordDto dto)
        {
            var adminUser = await _userManager.FindByIdAsync(adminUserId.ToString()) 
                ?? throw new InvalidOperationException("Tài khoản không tồn tại.");

            if (!string.IsNullOrEmpty(adminUser.SecondaryPasswordHash))
                throw new InvalidOperationException("Mật khẩu cấp 2 đã được thiết lập trước đó.");

            var isPrimaryValid = await _userManager.CheckPasswordAsync(adminUser, dto.PrimaryPassword);
            if (!isPrimaryValid)
                throw new InvalidOperationException("Mật khẩu cấp 1 không chính xác.");

            adminUser.SecondaryPasswordHash = _userManager.PasswordHasher.HashPassword(adminUser, dto.NewSecondaryPassword);
            var result = await _userManager.UpdateAsync(adminUser);
            if (!result.Succeeded)
                throw new InvalidOperationException("Không thể thiết lập mật khẩu cấp 2: " + string.Join(", ", result.Errors.Select(e => e.Description)));
        }

        public async Task RequestResetSecondaryPasswordAsync(int adminUserId)
        {
            var adminUser = await _userManager.FindByIdAsync(adminUserId.ToString())
                ?? throw new InvalidOperationException("Tài khoản không tồn tại.");

            // Fake Phone Number field for UserOtp storage mechanism since we are reusing it
            // We use the admin's email as the identifier.
            var identifier = "admin_sec_pass_" + adminUserId.ToString();

            // Check Cooldown 30s
            var remainingSeconds = await _otpRepository.GetRemainingCooldownSecondsAsync(identifier, TimeSpan.FromSeconds(30));
            if (remainingSeconds > 0)
                throw new InvalidOperationException($"Vui lòng đợi {remainingSeconds} giây trước khi gửi lại yêu cầu.");

            // Generate OTP
            var otp = Random.Shared.Next(100000, 999999).ToString();
            var otpHash = BCrypt.Net.BCrypt.HashPassword(otp);

            var entity = new ChargeSlot.Api.Models.UserOtp
            {
                PhoneNumber = identifier, // Storing identifier in PhoneNumber column temporarily
                OtpHash = otpHash,
                Purpose = ChargeSlot.Api.Enums.OtpPurpose.ResetSecondaryPassword,
                ExpiredAt = DateTimeHelper.VietnamNow().AddMinutes(5),
                IsUsed = false,
                CreatedAt = DateTimeHelper.VietnamNow()
            };

            await _otpRepository.AddAsync(entity);
            await _otpRepository.SaveChangesAsync();

            // Send via mocked / real email service
            // Gửi OTP thẳng tới laivuhoanganh.fj@gmail.com như Sếp chỉ định
            string targetEmail = "laivuhoanganh.fj@gmail.com"; 
            
            await _emailService.SendEmailAsync(
                to: targetEmail, 
                subject: "[ChargeSlot] Khôi phục Mật Khẩu Cấp 2", 
                body: $"Mã OTP của bạn là: {otp}. Mã có hiệu lực trong 5 phút. Nếu không phải bạn yêu cầu, vui lòng đổi mật khẩu ngay lập tức!"
            );
        }

        public async Task ConfirmResetSecondaryPasswordAsync(int adminUserId, ConfirmResetSecondaryPasswordDto dto)
        {
            var adminUser = await _userManager.FindByIdAsync(adminUserId.ToString())
                ?? throw new InvalidOperationException("Tài khoản không tồn tại.");

            var identifier = "admin_sec_pass_" + adminUserId.ToString();

            // Validate OTP
            var record = await _otpRepository.GetLatestValidOtpAsync(identifier, ChargeSlot.Api.Enums.OtpPurpose.ResetSecondaryPassword);
            if (record == null)
                throw new InvalidOperationException("Mã OTP không hợp lệ hoặc đã hết hạn.");

            if (!BCrypt.Net.BCrypt.Verify(dto.OtpCode, record.OtpHash))
                throw new InvalidOperationException("Mã OTP không chính xác.");

            // Mark OTP as used
            record.VerifiedAt = DateTimeHelper.VietnamNow();
            record.IsUsed = true;
            await _otpRepository.SaveChangesAsync();

            // Reset Password
            adminUser.SecondaryPasswordHash = _userManager.PasswordHasher.HashPassword(adminUser, dto.NewSecondaryPassword);
            await _userManager.UpdateAsync(adminUser);
        }
    }
}