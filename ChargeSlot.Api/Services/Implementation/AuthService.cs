using ChargeSlot.Api.DTOs.Auth;
using ChargeSlot.Api.Enums;
using ChargeSlot.Api.Constants;
using ChargeSlot.Api.Helpers;
using ChargeSlot.Api.Models;
using ChargeSlot.Api.Models.Identity;
using ChargeSlot.Api.Repositories.Interfaces;
using ChargeSlot.Api.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace ChargeSlot.Api.Services.Implementation
{
    public class AuthService : IAuthService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole<int>> _roleManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IConfiguration _config;
        private readonly IUserOtpRepository _otpRepository;
        private readonly IFirebaseAuthService _firebaseAuthService;
        private readonly IOwnerRepository _ownerRepo;
        private readonly IDriverRepository _driverRepo;
        private readonly IRefreshTokenRepository _refreshTokenRepo;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IEmailService _emailService;
        private readonly ILogger<AuthService> _logger;

        private const string VerifyEmailFrontendUrl = "https://chargeslot.vercel.app/verify-email";

        public AuthService(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole<int>> roleManager,
            SignInManager<ApplicationUser> signInManager,
            IConfiguration config,
            IUserOtpRepository otpRepository,
            IFirebaseAuthService firebaseAuthService,
            IOwnerRepository ownerRepo,
            IDriverRepository driverRepo,
            IRefreshTokenRepository refreshTokenRepo,
            IUnitOfWork unitOfWork,
            IEmailService emailService,
            ILogger<AuthService> logger)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _signInManager = signInManager;
            _config = config;
            _otpRepository = otpRepository;
            _firebaseAuthService = firebaseAuthService;
            _ownerRepo = ownerRepo;
            _driverRepo = driverRepo;
            _refreshTokenRepo = refreshTokenRepo;
            _unitOfWork = unitOfWork;
            _emailService = emailService;
            _logger = logger;
        }


        public async Task RegisterAsync(RegisterDto dto)
        {
            var phone = NormalizePhone(dto.PhoneNumber);

            // Verify Firebase token → confirm SĐT đã được xác thực qua OTP của Google
            var verifiedPhone = await _firebaseAuthService.VerifyTokenAndGetPhoneAsync(dto.FirebaseIdToken);
            if (verifiedPhone != phone)
                throw new InvalidOperationException(
                    "SĐT trong Firebase token không khớp với SĐT đăng ký."
                );

            var existing = await _userManager.FindByNameAsync(phone);
            if (existing != null)
            {
                // Nếu account cũ đang PENDING quá 24h, xoá đi cho đăng ký lại
                if (existing.Status == UserStatusConstants.PendingEmailVerification
                    && existing.CreatedAt < DateTimeHelper.VietnamNow().AddHours(-24))
                {
                    await _userManager.DeleteAsync(existing);
                }
                else
                {
                    throw new InvalidOperationException("Phone number already registered.");
                }
            }

            // Kiểm tra email đã được dùng chưa
            var existingByEmail = await _userManager.FindByEmailAsync(dto.Email);
            if (existingByEmail != null)
                throw new InvalidOperationException("Email đã được sử dụng bởi tài khoản khác.");

            var role = string.IsNullOrWhiteSpace(dto.Role) ? RoleConstants.Driver : dto.Role.Trim();
            if (!RoleConstants.Allowed.Contains(role))
                throw new InvalidOperationException("Invalid role.");

            await EnsureRolesExistAsync();

            var user = new ApplicationUser
            {
                UserName = phone,
                PhoneNumber = phone,
                FullName = dto.FullName,
                Email = dto.Email,
                EmailConfirmed = false,
                Status = UserStatusConstants.PendingEmailVerification,
                IsPhoneVerified = true, // Đã verify qua Firebase OTP
                CreatedAt = DateTimeHelper.VietnamNow()
            };

            var createResult = await _userManager.CreateAsync(user, dto.Password);
            if (!createResult.Succeeded)
                throw new InvalidOperationException(string.Join("; ", createResult.Errors.Select(e => e.Description)));

            var currentRoles = await _userManager.GetRolesAsync(user);
            if (currentRoles.Any())
                await _userManager.RemoveFromRolesAsync(user, currentRoles);

            await _userManager.AddToRoleAsync(user, role);

            // Create the corresponding profile record
            if (role == RoleConstants.Owner)
            {
                await _ownerRepo.AddAsync(new Owner
                {
                    UserId = user.Id,
                    BusinessName = dto.FullName,
                    TaxCode = "N/A",
                    CreatedAt = DateTimeHelper.VietnamNow()
                });
            }
            else if (role == RoleConstants.Driver)
            {
                await _driverRepo.AddAsync(new Driver
                {
                    UserId = user.Id,
                    CreatedAt = DateTimeHelper.VietnamNow()
                });
            }

            await _otpRepository.InvalidateAllOtpsAsync(phone);
            await _unitOfWork.CompleteAsync();

            // Gửi email xác thực
            await SendVerificationEmailAsync(user);
        }

        public async Task<AuthResponseDto> LoginAsync(LoginDto dto)
        {
            var phone = NormalizePhone(dto.PhoneNumber);

            var user = await _userManager.FindByNameAsync(phone);
            if (user == null)
                throw new InvalidOperationException("Invalid phone number or password.");

            // Block login nếu đang chờ verify email
            if (user.Status == UserStatusConstants.PendingEmailVerification)
                throw new InvalidOperationException("Tài khoản chưa xác thực email. Vui lòng kiểm tra hộp thư email để xác thực.");

            if (user.Status != UserStatusConstants.Active)
                throw new InvalidOperationException("Account is inactive/banned.");

            var signIn = await _signInManager.CheckPasswordSignInAsync(user, dto.Password, lockoutOnFailure: true);
            if (!signIn.Succeeded)
                throw new InvalidOperationException("Invalid phone number or password.");

            var roles = await _userManager.GetRolesAsync(user);
            var role = roles.SingleOrDefault() ?? RoleConstants.Driver;

            // Kiểm tra user cũ chưa có email
            var requiresEmail = string.IsNullOrEmpty(user.Email) || !user.EmailConfirmed;
            // Admin miễn email
            if (role == RoleConstants.Admin) requiresEmail = false;

            return await GenerateAuthResponseAsync(user, role, requiresEmail);
        }

        public async Task<AuthResponseDto> RefreshTokenAsync(string refreshToken)
        {
            var storedToken = await _refreshTokenRepo.GetByTokenAsync(refreshToken);

            if (storedToken == null)
                throw new InvalidOperationException("Invalid refresh token.");

            if (storedToken.IsRevoked)
                throw new InvalidOperationException("Refresh token has been revoked.");

            if (storedToken.IsExpired)
                throw new InvalidOperationException("Refresh token has expired.");

            // Revoke old token (rotation)
            storedToken.RevokedAt = DateTimeHelper.VietnamNow();

            var user = storedToken.User;
            if (user.Status != UserStatusConstants.Active)
                throw new InvalidOperationException("Account is inactive/banned.");

            var roles = await _userManager.GetRolesAsync(user);
            var role = roles.SingleOrDefault() ?? RoleConstants.Driver;

            // Kiểm tra user cũ chưa có email (giống LoginAsync)
            var requiresEmail = string.IsNullOrEmpty(user.Email) || !user.EmailConfirmed;
            if (role == RoleConstants.Admin) requiresEmail = false;

            // Generate new tokens
            var response = await GenerateAuthResponseAsync(user, role, requiresEmail);

            // Link old → new for audit trail
            storedToken.ReplacedByToken = response.RefreshToken;

            await _unitOfWork.CompleteAsync();
            return response;
        }

        public async Task RevokeTokenAsync(string refreshToken, int userId)
        {
            var storedToken = await _refreshTokenRepo.GetByTokenAndUserAsync(refreshToken, userId);

            if (storedToken == null)
                throw new InvalidOperationException("Invalid refresh token.");

            if (storedToken.IsRevoked)
                throw new InvalidOperationException("Token already revoked.");

            storedToken.RevokedAt = DateTimeHelper.VietnamNow();
            await _unitOfWork.CompleteAsync();
        }

        private async Task EnsureRolesExistAsync()
        {
            foreach (var role in RoleConstants.DbRoles)
            {
                if (!await _roleManager.RoleExistsAsync(role))
                    await _roleManager.CreateAsync(new IdentityRole<int>(role));
            }
        }

        // ─────────────── TOKEN GENERATION ───────────────

        private async Task<AuthResponseDto> GenerateAuthResponseAsync(ApplicationUser user, string role, bool requiresEmail = false)
        {
            var (accessToken, accessExpiresAtUtc) = GenerateUserJwt(user, role);
            var refreshToken = await CreateRefreshTokenAsync(user.Id);

            return new AuthResponseDto
            {
                AccessToken = accessToken,
                ExpiresAtUtc = accessExpiresAtUtc,
                RefreshToken = refreshToken.Token,
                RefreshTokenExpiresAtUtc = refreshToken.ExpiresAt,
                UserId = user.Id,
                PhoneNumber = user.PhoneNumber ?? user.UserName ?? "",
                Role = role,
                Email = user.Email,
                RequiresEmail = requiresEmail
            };
        }

        private async Task<RefreshToken> CreateRefreshTokenAsync(int userId)
        {
            var jwtSection = _config.GetSection("Jwt");
            var refreshDays = int.TryParse(jwtSection["RefreshTokenExpiresInDays"], out var d) ? d : 7;

            var refreshToken = new RefreshToken
            {
                Token = GenerateRefreshTokenString(),
                UserId = userId,
                ExpiresAt = DateTime.UtcNow.AddDays(refreshDays),
                CreatedAt = DateTimeHelper.VietnamNow()
            };

            _refreshTokenRepo.Add(refreshToken);
            await _unitOfWork.CompleteAsync();

            return refreshToken;
        }

        private (string token, DateTime expiresAtUtc) GenerateUserJwt(ApplicationUser user, string role)
        {
            var jwtSection = _config.GetSection("Jwt");
            var key = jwtSection["Key"] ?? throw new InvalidOperationException("Jwt:Key missing");
            var issuer = jwtSection["Issuer"];
            var audience = jwtSection["Audience"];
            var expiresMinutes = int.TryParse(jwtSection["ExpiresMinutes"], out var m) ? m : 120;

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.MobilePhone, user.PhoneNumber ?? user.UserName ?? ""),
                new Claim(ClaimTypes.Role, role),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
            var creds = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

            var expiresAtUtc = DateTime.UtcNow.AddMinutes(expiresMinutes);

            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: expiresAtUtc,
                signingCredentials: creds
            );

            var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
            return (tokenString, expiresAtUtc);
        }

        private static string GenerateRefreshTokenString()
        {
            var randomBytes = RandomNumberGenerator.GetBytes(64);
            return Convert.ToBase64String(randomBytes);
        }

        // ─────────────── HELPERS ───────────────

        private static string NormalizePhone(string phone)
        {
            if (phone.Equals("admin", StringComparison.OrdinalIgnoreCase)) return "admin";
            return PhoneNumberHelper.NormalizeAndValidate(phone);
        }

        public async Task ResetPasswordAsync(string phoneNumber, string newPassword, string firebaseIdToken)
        {
            var phone = NormalizePhone(phoneNumber);

            // Verify Firebase token → confirm SĐT đã được xác thực qua OTP của Google
            var verifiedPhone = await _firebaseAuthService.VerifyTokenAndGetPhoneAsync(firebaseIdToken);
            if (verifiedPhone != phone)
                throw new InvalidOperationException(
                    "SĐT trong Firebase token không khớp."
                );

            var user = await _userManager.FindByNameAsync(phone);
            if (user == null)
                throw new InvalidOperationException("User not found.");

            var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);

            var result = await _userManager.ResetPasswordAsync(
                user,
                resetToken,
                newPassword
            );

            if (!result.Succeeded)
                throw new InvalidOperationException(
                    string.Join("; ", result.Errors.Select(e => e.Description))
                );

            await _otpRepository.InvalidateAllOtpsAsync(phone);
            await _unitOfWork.CompleteAsync();
        }

        public async Task ChangePasswordAsync(int userId, string currentPassword, string newPassword)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString())
                ?? throw new InvalidOperationException("User không tồn tại.");

            var result = await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);
            if (!result.Succeeded)
                throw new InvalidOperationException(
                    string.Join("; ", result.Errors.Select(e => e.Description)));
        }

        public async Task<bool> CheckPhoneExistsAsync(string phoneNumber)
        {
            var phone = NormalizePhone(phoneNumber);
            var user = await _userManager.FindByNameAsync(phone);
            return user != null;
        }

        // ─────────────── EMAIL VERIFICATION ───────────────

        public async Task VerifyEmailAsync(int userId, string token)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString())
                ?? throw new InvalidOperationException("Tài khoản không tồn tại.");

            // Decode token (frontend gửi URL-encoded)
            var decodedToken = Uri.UnescapeDataString(token);
            IdentityResult result;

            if (!string.IsNullOrEmpty(user.PendingEmail))
            {
                // Đổi email: user đã verify email cũ, giờ muốn chuyển sang email mới
                result = await _userManager.ChangeEmailAsync(user, user.PendingEmail, decodedToken);
                if (result.Succeeded)
                {
                    user.EmailConfirmed = true;
                    user.PendingEmail = null; // Clear pending email after success
                    await _userManager.UpdateAsync(user);
                }
            }
            else
            {
                // Xác thực email lần đầu (đăng ký mới)
                if (user.EmailConfirmed)
                    throw new InvalidOperationException("Email đã được xác thực trước đó.");
                
                result = await _userManager.ConfirmEmailAsync(user, decodedToken);
            }

            if (!result.Succeeded)
                throw new InvalidOperationException("Link xác thực không hợp lệ hoặc đã hết hạn. Vui lòng yêu cầu gửi lại.");

            // Nếu account đang PENDING → chuyển sang ACTIVE
            if (user.Status == UserStatusConstants.PendingEmailVerification)
            {
                user.Status = UserStatusConstants.Active;
                await _userManager.UpdateAsync(user);
            }

            _logger.LogInformation("[Auth] Email verified for User {UserId}: {Email}", user.Id, user.Email);
        }

        public async Task AddEmailAsync(int userId, string email)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString())
                ?? throw new InvalidOperationException("Tài khoản không tồn tại.");

            // Kiểm tra email đã được dùng chưa
            var existingByEmail = await _userManager.FindByEmailAsync(email);
            if (existingByEmail != null && existingByEmail.Id != userId)
                throw new InvalidOperationException("Email đã được sử dụng bởi tài khoản khác.");

            // Cập nhật PendingEmail thay vì Email trực tiếp, đảm bảo không mất Email đang dùng
            user.PendingEmail = email;
            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
                throw new InvalidOperationException("Không thể cập nhật email: " + string.Join(", ", updateResult.Errors.Select(e => e.Description)));

            // Gửi verification link cho email chờ xác nhận
            await SendVerificationEmailAsync(user, email);

            _logger.LogInformation("[Auth] Verification email sent to User {UserId}: {Email}", user.Id, email);
        }

        public async Task ResendVerificationEmailAsync(int userId)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString())
                ?? throw new InvalidOperationException("Tài khoản không tồn tại.");

            string? targetEmail = user.PendingEmail ?? user.Email;

            if (string.IsNullOrEmpty(targetEmail))
                throw new InvalidOperationException("Tài khoản chưa có email. Vui lòng thêm email trước.");

            if (user.EmailConfirmed && string.IsNullOrEmpty(user.PendingEmail))
                throw new InvalidOperationException("Email đã được xác thực.");

            // Reset confirmation token (generate new)
            await SendVerificationEmailAsync(user, user.PendingEmail);

            _logger.LogInformation("[Auth] Resent verification email to User {UserId}: {Email}", user.Id, targetEmail);
        }

        private async Task SendVerificationEmailAsync(ApplicationUser user, string? pendingEmail = null)
        {
            string token;
            string targetEmail;
            if (!string.IsNullOrEmpty(pendingEmail))
            {
                token = await _userManager.GenerateChangeEmailTokenAsync(user, pendingEmail);
                targetEmail = pendingEmail;
            }
            else
            {
                token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                targetEmail = user.Email!;
            }
            
            var encodedToken = Uri.EscapeDataString(token);

            var verifyUrl = $"{VerifyEmailFrontendUrl}?token={encodedToken}&userId={user.Id}";

            var emailBody = $@"
                <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;'>
                    <h2 style='color: #2563eb;'>🔌 ChargeSlot — Xác Thực Email</h2>
                    <p>Xin chào <strong>{user.FullName}</strong>,</p>
                    <p>Cảm ơn bạn đã đăng ký tài khoản ChargeSlot. Vui lòng nhấn nút bên dưới để xác thực email của bạn:</p>
                    <div style='text-align: center; margin: 30px 0;'>
                        <a href='{verifyUrl}' 
                           style='background-color: #2563eb; color: white; padding: 14px 28px; 
                                  text-decoration: none; border-radius: 8px; font-size: 16px;
                                  display: inline-block;'>
                            ✅ Xác Thực Email
                        </a>
                    </div>
                    <p style='color: #6b7280; font-size: 14px;'>Link có hiệu lực trong 24 giờ. Nếu bạn không yêu cầu đăng ký, vui lòng bỏ qua email này.</p>
                    <hr style='border: none; border-top: 1px solid #e5e7eb; margin: 20px 0;'/>
                    <p style='color: #9ca3af; font-size: 12px;'>© ChargeSlot - Hệ thống đặt chỗ sạc xe điện</p>
                </div>";

            try
            {
                await _emailService.SendEmailAsync(
                    to: targetEmail,
                    subject: "[ChargeSlot] Xác Thực Email Đăng Ký",
                    body: emailBody
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Auth] Failed to send verification email to {Email}", user.Email);
                // Không throw — account đã tạo, user có thể dùng resend
            }
        }

        // ─────────────── USER INFO ───────────────

        public async Task<DTOs.Auth.UserInfoDto> GetCurrentUserInfoAsync(int userId)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString())
                ?? throw new InvalidOperationException("Tài khoản không tồn tại.");

            var roles = await _userManager.GetRolesAsync(user);
            var role = roles.SingleOrDefault() ?? RoleConstants.Driver;

            var requiresEmail = string.IsNullOrEmpty(user.Email) || !user.EmailConfirmed;
            if (role == RoleConstants.Admin) requiresEmail = false;

            string? kycStatus = null;
            string? kycRejectReason = null;

            if (role == RoleConstants.Owner)
            {
                var owner = await _ownerRepo.GetByUserIdAsync(user.Id);
                if (owner != null)
                {
                    kycStatus = owner.KycStatus.ToString();
                    kycRejectReason = owner.KycRejectReason;
                }
            }

            return new DTOs.Auth.UserInfoDto
            {
                UserId = user.Id,
                FullName = user.FullName ?? "",
                PhoneNumber = user.PhoneNumber ?? user.UserName ?? "",
                Email = user.Email,
                EmailConfirmed = user.EmailConfirmed,
                Role = role,
                AvatarUrl = user.AvatarUrl,
                Status = user.Status ?? "",
                RequiresEmail = requiresEmail,
                KycStatus = kycStatus,
                KycRejectReason = kycRejectReason,
                CreatedAt = user.CreatedAt
            };
        }

        public async Task UpdateCurrentUserInfoAsync(int userId, DTOs.Auth.UpdateUserInfoDto dto)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString())
                ?? throw new InvalidOperationException("Tài khoản không tồn tại.");

            user.FullName = dto.FullName;
            
            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
                throw new InvalidOperationException("Không thể cập nhật thông tin: " + string.Join(", ", updateResult.Errors.Select(e => e.Description)));
        }

    }
}

