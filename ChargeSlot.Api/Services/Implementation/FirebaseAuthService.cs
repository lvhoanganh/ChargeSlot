using ChargeSlot.Api.Constants;
using ChargeSlot.Api.Data;
using ChargeSlot.Api.DTOs.Auth;
using ChargeSlot.Api.Helpers;
using ChargeSlot.Api.Models;
using ChargeSlot.Api.Models.Identity;
using ChargeSlot.Api.Services.Interfaces;
using FirebaseAdmin.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace ChargeSlot.Api.Services.Implementation
{
    public class FirebaseAuthService : IFirebaseAuthService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole<int>> _roleManager;
        private readonly IConfiguration _config;
        private readonly ChargeSlotDbContext _context;
        private readonly ILogger<FirebaseAuthService> _logger;

        public FirebaseAuthService(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole<int>> roleManager,
            IConfiguration config,
            ChargeSlotDbContext context,
            ILogger<FirebaseAuthService> logger)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _config = config;
            _context = context;
            _logger = logger;
        }

        public async Task<AuthResponseDto> LoginWithFirebaseAsync(string firebaseIdToken, string? role, string? fullName = null)
        {
            // 1. Verify Firebase ID Token
            FirebaseToken decodedToken;
            try
            {
                decodedToken = await FirebaseAuth.DefaultInstance.VerifyIdTokenAsync(firebaseIdToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Firebase token verification failed");
                throw new InvalidOperationException("Firebase token không hợp lệ hoặc đã hết hạn.");
            }

            // 2. Lấy phone number từ Firebase token
            var phoneNumber = decodedToken.Claims.TryGetValue("phone_number", out var phoneClaim)
                ? phoneClaim?.ToString()
                : null;

            if (string.IsNullOrEmpty(phoneNumber))
                throw new InvalidOperationException("Firebase token không chứa số điện thoại.");

            // Normalize phone: +84xxx → 0xxx
            var normalizedPhone = PhoneNumberHelper.NormalizeAndValidate(phoneNumber);

            // 3. Tìm user trong DB
            var user = await _userManager.FindByNameAsync(normalizedPhone);

            if (user != null)
            {
                // User đã tồn tại → login
                if (user.Status != "ACTIVE")
                    throw new InvalidOperationException("Tài khoản đã bị khóa.");

                var existingRoles = await _userManager.GetRolesAsync(user);
                var userRole = existingRoles.SingleOrDefault() ?? RoleConstants.Driver;
                return await GenerateAuthResponseAsync(user, userRole);
            }

            // 4. User chưa tồn tại → auto-register
            var selectedRole = string.IsNullOrWhiteSpace(role) ? RoleConstants.Driver : role.Trim();
            if (!RoleConstants.Allowed.Contains(selectedRole))
                throw new InvalidOperationException("Role không hợp lệ.");

            await EnsureRolesExistAsync();

            var newUser = new ApplicationUser
            {
                UserName = normalizedPhone,
                PhoneNumber = normalizedPhone,
                FullName = !string.IsNullOrWhiteSpace(fullName) ? fullName : $"User {normalizedPhone}",
                Status = "ACTIVE",
                IsPhoneVerified = true, // Đã verify qua Firebase
                CreatedAt = DateTimeHelper.VietnamNow()
            };

            // Tạo user với random password (user dùng Firebase login, không cần password)
            var randomPassword = GenerateRandomPassword();
            var createResult = await _userManager.CreateAsync(newUser, randomPassword);
            if (!createResult.Succeeded)
                throw new InvalidOperationException(
                    string.Join("; ", createResult.Errors.Select(e => e.Description)));

            await _userManager.AddToRoleAsync(newUser, selectedRole);

            // Tạo profile tương ứng
            if (selectedRole == RoleConstants.Owner)
            {
                _context.Owner.Add(new Owner
                {
                    UserId = newUser.Id,
                    BusinessName = newUser.FullName,
                    TaxCode = "N/A",
                    CreatedAt = DateTimeHelper.VietnamNow()
                });
            }
            else if (selectedRole == RoleConstants.Driver)
            {
                _context.Driver.Add(new Driver
                {
                    UserId = newUser.Id,
                    CreatedAt = DateTimeHelper.VietnamNow()
                });
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("Auto-registered user {Phone} with role {Role} via Firebase", normalizedPhone, selectedRole);

            return await GenerateAuthResponseAsync(newUser, selectedRole);
        }

        // ─────────────── TOKEN GENERATION ───────────────

        private async Task<AuthResponseDto> GenerateAuthResponseAsync(ApplicationUser user, string role)
        {
            var (accessToken, accessExpiresAtUtc) = GenerateJwt(user, role);
            var refreshToken = await CreateRefreshTokenAsync(user.Id);

            return new AuthResponseDto
            {
                AccessToken = accessToken,
                ExpiresAtUtc = accessExpiresAtUtc,
                RefreshToken = refreshToken.Token,
                RefreshTokenExpiresAtUtc = refreshToken.ExpiresAt,
                UserId = user.Id,
                PhoneNumber = user.PhoneNumber ?? user.UserName ?? "",
                Role = role
            };
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

            return (new JwtSecurityTokenHandler().WriteToken(token), expiresAtUtc);
        }

        private async Task<RefreshToken> CreateRefreshTokenAsync(int userId)
        {
            var jwtSection = _config.GetSection("Jwt");
            var refreshDays = int.TryParse(jwtSection["RefreshTokenExpiresInDays"], out var d) ? d : 7;

            var refreshToken = new RefreshToken
            {
                Token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)),
                UserId = userId,
                ExpiresAt = DateTime.UtcNow.AddDays(refreshDays),
                CreatedAt = DateTimeHelper.VietnamNow()
            };

            _context.RefreshTokens.Add(refreshToken);
            await _context.SaveChangesAsync();
            return refreshToken;
        }

        // ─────────────── HELPERS ───────────────

        private async Task EnsureRolesExistAsync()
        {
            foreach (var role in RoleConstants.DbRoles)
            {
                if (!await _roleManager.RoleExistsAsync(role))
                    await _roleManager.CreateAsync(new IdentityRole<int>(role));
            }
        }

        private static string GenerateRandomPassword()
        {
            // Tạo password ngẫu nhiên đáp ứng Identity requirements
            var bytes = RandomNumberGenerator.GetBytes(16);
            return $"Fb!{Convert.ToBase64String(bytes)}";
        }
    }
}
