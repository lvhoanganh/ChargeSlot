using ChargeSlot.Api.Models.Identity;
using ChargeSlot.Api.DTOs.Admin;
using ChargeSlot.Api.DTOs;
using ChargeSlot.Api.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using ChargeSlot.Api.Constants;
using ChargeSlot.Api.Repositories.Interfaces;
using ChargeSlot.Api.Helpers;

namespace ChargeSlot.Api.Services.Implementation
{
    public class AdminAccountService : IAdminAccountService
    {
        private readonly IAdminAccountRepository _adminAccountRepository;
        private readonly IBookingRepository _bookingRepo;
        private readonly IChargingStationRepository _stationRepo;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IBookingService _bookingService;
        private readonly INotificationService _notificationService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IEmailService _emailService;
        private readonly IUserOtpRepository _otpRepository;
        private readonly IOwnerRepository _ownerRepo;
        private readonly IDriverRepository _driverRepo;
        private readonly IWalletRepository _walletRepo;
        private readonly ISystemConfigService _systemConfig;

        public AdminAccountService(
            IAdminAccountRepository adminAccountRepository,
            IBookingRepository bookingRepo,
            IChargingStationRepository stationRepo,
            IUnitOfWork unitOfWork,
            IBookingService bookingService,
            INotificationService notificationService,
            UserManager<ApplicationUser> userManager,
            IEmailService emailService,
            IUserOtpRepository otpRepository,
            IOwnerRepository ownerRepo,
            IDriverRepository driverRepo,
            IWalletRepository walletRepo,
            ISystemConfigService systemConfig)
        {
            _adminAccountRepository = adminAccountRepository;
            _bookingRepo = bookingRepo;
            _stationRepo = stationRepo;
            _unitOfWork = unitOfWork;
            _bookingService = bookingService;
            _notificationService = notificationService;
            _userManager = userManager;
            _emailService = emailService;
            _otpRepository = otpRepository;
            _ownerRepo = ownerRepo;
            _driverRepo = driverRepo;
            _walletRepo = walletRepo;
            _systemConfig = systemConfig;
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
                    Email = u.Email,
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

        public async Task<AdminOwnerDetailDto> GetOwnerDetailAsync(int ownerUserId)
        {
            var user = await _userManager.FindByIdAsync(ownerUserId.ToString()) 
                       ?? throw new InvalidOperationException("Owner User not found.");
            
            var owner = await _ownerRepo.GetByUserIdAsync(ownerUserId) 
                        ?? throw new InvalidOperationException("Owner profile not found.");

            // Get Owner Wallet
            var wallet = await _walletRepo.GetByUserIdAsync(ownerUserId)
                         ?? throw new InvalidOperationException("Owner wallet not found.");

            // Get Stations using IChargingStationRepository
            var stations = await _stationRepo.GetAllByOwnerTrackingAsync(ownerUserId);

            return new AdminOwnerDetailDto
            {
                UserId = user.Id,
                PhoneNumber = user.PhoneNumber ?? string.Empty,
                Email = user.Email,
                FullName = user.FullName ?? string.Empty,
                Status = user.Status ?? string.Empty,
                AvatarUrl = user.AvatarUrl,
                CreatedAt = user.CreatedAt,
                Kyc = new AdminOwnerKycDto
                {
                    BusinessName = owner.BusinessName ?? string.Empty,
                    TaxCode = owner.TaxCode ?? string.Empty,
                    IdCardNumber = owner.IdCardNumber,
                    IdCardDate = owner.IdCardDate,
                    BusinessLicenseNumber = owner.BusinessLicenseNumber,
                    BusinessLicenseUrl = owner.BusinessLicenseUrl,
                    Address = owner.Address,
                    KycStatus = owner.KycStatus.ToString(),
                    KycRejectReason = owner.KycRejectReason
                },
                Wallet = new AdminOwnerWalletDto
                {
                    WalletId = wallet.Id,
                    AvailableBalance = wallet.AvailableBalance,
                    FrozenBalance = wallet.FrozenBalance
                },
                Stations = stations.Select(s => new AdminOwnerStationDto
                {
                    StationId = s.Id,
                    Name = s.Name,
                    Address = s.Address,
                    ApprovalStatus = s.ApprovalStatus.ToString(),
                    OperationalStatus = s.OperationalStatus.ToString(),
                    AverageRating = s.AverageRating,
                    CreatedAt = s.CreatedAt
                }).ToList()
            };
        }

        public async Task<AdminDriverDetailDto> GetDriverDetailAsync(int driverUserId)
        {
            var user = await _userManager.FindByIdAsync(driverUserId.ToString()) 
                       ?? throw new InvalidOperationException("Driver User not found.");
            
            var driver = await _driverRepo.GetByUserIdAsync(driverUserId) 
                         ?? throw new InvalidOperationException("Driver profile not found.");

            // Get Driver Wallet
            var wallet = await _walletRepo.GetByUserIdAsync(driverUserId)
                         ?? throw new InvalidOperationException("Driver wallet not found.");

            // Get 10 recent bookings using IBookingRepository
            var recentBookings = await _bookingRepo.GetByDriverAsync(driverUserId);
            var topBookings = recentBookings.OrderByDescending(b => b.CreatedAt).Take(10).ToList();

            return new AdminDriverDetailDto
            {
                UserId = user.Id,
                PhoneNumber = user.PhoneNumber ?? string.Empty,
                Email = user.Email,
                FullName = user.FullName ?? string.Empty,
                Status = user.Status ?? string.Empty,
                AvatarUrl = user.AvatarUrl,
                CreatedAt = user.CreatedAt,
                VehicleType = driver.VehicleType,
                LicensePlate = driver.LicensePlate,
                LicenseNumber = driver.LicenseNumber,
                LoyaltyPoints = driver.LoyaltyPoints,
                Wallet = new AdminDriverWalletDto
                {
                    WalletId = wallet.Id,
                    AvailableBalance = wallet.AvailableBalance,
                    FrozenBalance = wallet.FrozenBalance
                },
                RecentBookings = topBookings.Select(b => new AdminDriverRecentBookingDto
                {
                    BookingId = b.Id,
                    StationName = b.ChargingSlot?.ChargingStation?.Name ?? string.Empty,
                    SlotName = b.ChargingSlot?.SlotName ?? string.Empty,
                    StartTime = b.StartTime,
                    EndTime = b.EndTime,
                    TotalAmount = b.TotalAmount,
                    Status = b.Status.ToString(),
                    CreatedAt = b.CreatedAt
                }).ToList()
            };
        }

        public async Task<string> ToggleBanStatusAsync(int targetUserId, int actingAdminUserId, string? reason)
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

            if (user.Status == UserStatusConstants.Active)
            {
                if (string.IsNullOrWhiteSpace(reason))
                    throw new InvalidOperationException("Vui lòng cung cấp lý do khi khóa tài khoản.");

                user.Status = UserStatusConstants.Banned;
                user.BannedUntil = null; // Khóa đến khi admin mở lại
                
                await _notificationService.SendAsync(
                    targetUserId, 
                    "Tài khoản bị khóa", 
                    $"Tài khoản của bạn đã bị Admin khóa. Lý do: {reason}", 
                    ChargeSlot.Api.Enums.NotificationType.System);
            }
            else if (user.Status == UserStatusConstants.Banned || user.Status == UserStatusConstants.Suspended)
            {
                user.Status = UserStatusConstants.Active;
                user.BannedUntil = null;
                user.BanCount = 0; // Khi được Admin ân xá, xóa quota vi phạm
                
                await _notificationService.SendAsync(
                    targetUserId, 
                    "Tài khoản được mở khóa", 
                    "Tài khoản của bạn đã được Admin mở khóa và có thể hoạt động bình thường.", 
                    ChargeSlot.Api.Enums.NotificationType.System);
            }
            else
            {
                throw new InvalidOperationException("Only ACTIVE, SUSPENDED or BANNED accounts can be toggled.");
            }

            var updated = await _adminAccountRepository.UpdateAsync(user);
            if (!updated)
                throw new InvalidOperationException("Failed to update user status.");

            // Cascading Cancel
            if (user.Status == UserStatusConstants.Suspended || user.Status == UserStatusConstants.Banned)
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
                var bookings = await _bookingRepo.GetActiveBookingsByDriverAsync(userId, targetStatuses);
                    
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
                var stations = await _stationRepo.GetAllByOwnerTrackingAsync(userId);
                    
                foreach(var s in stations)
                {
                    s.OperationalStatus = ChargeSlot.Api.Enums.OperationalStatus.Inactive;
                }
                await _unitOfWork.CompleteAsync();

                var stationIds = stations.Select(s => s.Id).ToList();
                var bookings = await _bookingRepo.GetActiveBookingsByStationIdsAsync(stationIds, targetStatuses);
                    
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

            var ownerCount = await _ownerRepo.CountAsync();
            var driverCount = await _driverRepo.CountAsync();
            var adminIds = await _adminAccountRepository.GetAdminUserIdsAsync();

            return new AccountStatisticsDto
            {
                TotalAccounts = users.Count,
                ActiveAccounts = users.Count(x => x.Status == UserStatusConstants.Active),
                BannedAccounts = users.Count(x => x.Status == UserStatusConstants.Banned || x.Status == UserStatusConstants.Suspended),
                TotalOwners = ownerCount,
                TotalDrivers = driverCount,
                TotalAdmins = adminIds.Count
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

            // Check Cooldown
            var cooldownSeconds = await _systemConfig.GetIntAsync(SystemConfigKeys.OTP_Cooldown_Seconds, 30);
            var remainingSeconds = await _otpRepository.GetRemainingCooldownSecondsAsync(identifier, TimeSpan.FromSeconds(cooldownSeconds));
            if (remainingSeconds > 0)
                throw new InvalidOperationException($"Vui lòng đợi {remainingSeconds} giây trước khi gửi lại yêu cầu.");

            // Generate OTP
            var otp = Random.Shared.Next(100000, 999999).ToString();
            var otpHash = BCrypt.Net.BCrypt.HashPassword(otp);

            var otpExpiryMinutes = await _systemConfig.GetIntAsync(SystemConfigKeys.OTP_Expiry_Minutes, 5);
            var entity = new ChargeSlot.Api.Models.UserOtp
            {
                PhoneNumber = identifier, // Storing identifier in PhoneNumber column temporarily
                OtpHash = otpHash,
                Purpose = ChargeSlot.Api.Enums.OtpPurpose.ResetSecondaryPassword,
                ExpiredAt = DateTimeHelper.VietnamNow().AddMinutes(otpExpiryMinutes),
                IsUsed = false,
                CreatedAt = DateTimeHelper.VietnamNow()
            };

            await _otpRepository.AddAsync(entity);
            await _unitOfWork.CompleteAsync();

            // Gửi OTP tới email của admin (fallback → email mặc định)
            string targetEmail = adminUser.Email ?? "laivuhoanganh.fj@gmail.com";
            
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
            await _unitOfWork.CompleteAsync();

            // Reset Password
            adminUser.SecondaryPasswordHash = _userManager.PasswordHasher.HashPassword(adminUser, dto.NewSecondaryPassword);
            await _userManager.UpdateAsync(adminUser);
        }
    }
}



