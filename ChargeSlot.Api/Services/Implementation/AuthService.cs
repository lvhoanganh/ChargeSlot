using ChargeSlot.Api.DTOs.Auth;
using ChargeSlot.Api.Enums;
using ChargeSlot.Api.Helpers;
using ChargeSlot.Api.Models.Identity;
using ChargeSlot.Api.Repositories.Interfaces;
using ChargeSlot.Api.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
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

        public AuthService(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole<int>> roleManager,
            SignInManager<ApplicationUser> signInManager,
            IConfiguration config,
            IUserOtpRepository otpRepository)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _signInManager = signInManager;
            _config = config;
            _otpRepository = otpRepository;
        }


        public async Task RegisterAsync(RegisterDto dto)
        {
            var phone = NormalizePhone(dto.PhoneNumber);
            // 🔒 CHECK OTP ĐÃ VERIFY CHƯA
            var canRegister = await _otpRepository.HasRecentlyVerifiedOtpAsync(
                phone,
                OtpPurpose.Register,
                TimeSpan.FromMinutes(5)
            );

            if (!canRegister)
                throw new InvalidOperationException(
                    "OTP verification required before registration."
                );

            // Check tồn tại theo UserName (vì ta sẽ set UserName = phone)
            var existing = await _userManager.FindByNameAsync(phone);
            if (existing != null)
                throw new InvalidOperationException("Phone number already registered.");

            var role = string.IsNullOrWhiteSpace(dto.Role) ? RoleConstants.Driver : dto.Role.Trim();
            if (!RoleConstants.Allowed.Contains(role))
                throw new InvalidOperationException("Invalid role.");

            await EnsureRolesExistAsync();

            var user = new ApplicationUser
            {
                UserName = phone,          // ⭐ PHONE-FIRST: phone là username
                PhoneNumber = phone,
                FullName = dto.FullName,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                PhoneNumberConfirmed = false // sau này OTP xong set true
            };

            var createResult = await _userManager.CreateAsync(user, dto.Password);
            if (!createResult.Succeeded)
                throw new InvalidOperationException(string.Join("; ", createResult.Errors.Select(e => e.Description)));

            // ⭐ ÉP 1 USER = 1 ROLE (register thì chưa có role cũ nhưng làm luôn cho chắc)
            var currentRoles = await _userManager.GetRolesAsync(user);
            if (currentRoles.Any())
                await _userManager.RemoveFromRolesAsync(user, currentRoles);

            await _userManager.AddToRoleAsync(user, role);
            // 🔥 VÔ HIỆU HOÁ TẤT CẢ OTP CŨ
            await _otpRepository.InvalidateAllOtpsAsync(phone);
            await _otpRepository.SaveChangesAsync();

        }

        public async Task<AuthResponseDto> LoginAsync(LoginDto dto)
        {
            var phone = NormalizePhone(dto.PhoneNumber);

            var user = await _userManager.FindByNameAsync(phone);
            if (user == null)
                throw new InvalidOperationException("Invalid phone number or password.");

            if (!user.IsActive)
                throw new InvalidOperationException("Account is inactive/banned.");

            // Nếu bạn muốn chặn login khi chưa OTP:
            // if (!user.PhoneNumberConfirmed)
            //     throw new InvalidOperationException("Phone number not verified.");

            var signIn = await _signInManager.CheckPasswordSignInAsync(user, dto.Password, lockoutOnFailure: true);
            if (!signIn.Succeeded)
                throw new InvalidOperationException("Invalid phone number or password.");

            var roles = await _userManager.GetRolesAsync(user);
            var role = roles.SingleOrDefault() ?? RoleConstants.Driver; // bạn đang hướng 1 role/user

            var (token, expiresAtUtc) = GenerateJwt(user, role);

            return new AuthResponseDto
            {
                AccessToken = token,
                ExpiresAtUtc = expiresAtUtc,
                UserId = user.Id,
                PhoneNumber = user.PhoneNumber ?? phone,
                Role = role
            };
        }

        private async Task EnsureRolesExistAsync()
        {
            foreach (var role in RoleConstants.Allowed)
            {
                if (!await _roleManager.RoleExistsAsync(role))
                    await _roleManager.CreateAsync(new IdentityRole<int>(role));
            }
        }

        private (string token, DateTime expiresAtUtc) GenerateJwt(ApplicationUser user, string role)
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

        private static string NormalizePhone(string phone)
        {
            // Tối thiểu: trim + bỏ space.
            // Bạn có thể chuẩn hoá theo VN (84/0) sau.
            return phone.Trim().Replace(" ", "");
        }
        public async Task ResetPasswordAsync(string phoneNumber, string newPassword)
        {
            var phone = NormalizePhone(phoneNumber);

            // 1️⃣ Check OTP đã verify chưa
            var canReset = await _otpRepository.HasRecentlyVerifiedOtpAsync(
                phone,
                OtpPurpose.ResetPassword,
                TimeSpan.FromMinutes(5)
            );

            if (!canReset)
                throw new InvalidOperationException(
                    "OTP verification required before resetting password."
                );

            // 2️⃣ Tìm user theo phone (UserName)
            var user = await _userManager.FindByNameAsync(phone);
            if (user == null)
                throw new InvalidOperationException("User not found.");

            // 3️⃣ Generate reset token (Identity chuẩn)
            var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);

            // 4️⃣ Reset password
            var result = await _userManager.ResetPasswordAsync(
                user,
                resetToken,
                newPassword
            );

            if (!result.Succeeded)
                throw new InvalidOperationException(
                    string.Join("; ", result.Errors.Select(e => e.Description))
                );

            // 5️⃣ Vô hiệu hoá OTP sau khi reset password
            await _otpRepository.InvalidateAllOtpsAsync(phone);
            await _otpRepository.SaveChangesAsync();
        }

    }
}
