using ChargeSlot.Api.Data;
using ChargeSlot.Api.DTOs.Auth;
using ChargeSlot.Api.Enums;
using ChargeSlot.Api.Constants;
using ChargeSlot.Api.Helpers;
using ChargeSlot.Api.Models;
using ChargeSlot.Api.Models.Identity;
using ChargeSlot.Api.Repositories.Interfaces;
using ChargeSlot.Api.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
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
        private readonly ChargeSlotDbContext _context;

        public AuthService(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole<int>> roleManager,
            SignInManager<ApplicationUser> signInManager,
            IConfiguration config,
            IUserOtpRepository otpRepository,
            ChargeSlotDbContext context)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _signInManager = signInManager;
            _config = config;
            _otpRepository = otpRepository;
            _context = context;
        }


        public async Task RegisterAsync(RegisterDto dto)
        {
            var phone = NormalizePhone(dto.PhoneNumber);
            var canRegister = await _otpRepository.HasRecentlyVerifiedOtpAsync(
                phone,
                OtpPurpose.Register,
                TimeSpan.FromMinutes(5)
            );

            if (!canRegister)
                throw new InvalidOperationException(
                    "OTP verification required before registration."
                );

            var existing = await _userManager.FindByNameAsync(phone);
            if (existing != null)
                throw new InvalidOperationException("Phone number already registered.");

            var role = string.IsNullOrWhiteSpace(dto.Role) ? RoleConstants.Driver : dto.Role.Trim();
            if (!RoleConstants.Allowed.Contains(role))
                throw new InvalidOperationException("Invalid role.");

            await EnsureRolesExistAsync();

            var user = new ApplicationUser
            {
                UserName = phone,
                PhoneNumber = phone,
                FullName = dto.FullName,
                Status = "ACTIVE",
                IsPhoneVerified = false,
                CreatedAt = DateTime.UtcNow
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
                _context.Owner.Add(new Owner
                {
                    UserId = user.Id,
                    BusinessName = dto.FullName,
                    TaxCode = "N/A",
                    CreatedAt = DateTime.UtcNow
                });
            }
            else if (role == RoleConstants.Driver)
            {
                _context.Driver.Add(new Driver
                {
                    UserId = user.Id,
                    CreatedAt = DateTime.UtcNow
                });
            }

            await _otpRepository.InvalidateAllOtpsAsync(phone);
            await _otpRepository.SaveChangesAsync();
            await _context.SaveChangesAsync();
        }

        public async Task<AuthResponseDto> LoginAsync(LoginDto dto)
        {
            var phone = NormalizePhone(dto.PhoneNumber);

            var user = await _userManager.FindByNameAsync(phone);
            if (user == null)
                throw new InvalidOperationException("Invalid phone number or password.");

            if (user.Status != "ACTIVE")
                throw new InvalidOperationException("Account is inactive/banned.");

            var signIn = await _signInManager.CheckPasswordSignInAsync(user, dto.Password, lockoutOnFailure: true);
            if (!signIn.Succeeded)
                throw new InvalidOperationException("Invalid phone number or password.");

            var roles = await _userManager.GetRolesAsync(user);
            var role = roles.SingleOrDefault() ?? RoleConstants.Driver;

            return await GenerateAuthResponseAsync(user, role);
        }

        public async Task<AuthResponseDto> RefreshTokenAsync(string refreshToken)
        {
            var storedToken = await _context.RefreshTokens
                .Include(rt => rt.User)
                .FirstOrDefaultAsync(rt => rt.Token == refreshToken);

            if (storedToken == null)
                throw new InvalidOperationException("Invalid refresh token.");

            if (storedToken.IsRevoked)
                throw new InvalidOperationException("Refresh token has been revoked.");

            if (storedToken.IsExpired)
                throw new InvalidOperationException("Refresh token has expired.");

            // Revoke old token (rotation)
            storedToken.RevokedAt = DateTime.UtcNow;

            var user = storedToken.User;
            if (user.Status != "ACTIVE")
                throw new InvalidOperationException("Account is inactive/banned.");

            var roles = await _userManager.GetRolesAsync(user);
            var role = roles.SingleOrDefault() ?? RoleConstants.Driver;

            // Generate new tokens
            var response = await GenerateAuthResponseAsync(user, role);

            // Link old → new for audit trail
            storedToken.ReplacedByToken = response.RefreshToken;

            await _context.SaveChangesAsync();
            return response;
        }

        public async Task RevokeTokenAsync(string refreshToken, int userId)
        {
            var storedToken = await _context.RefreshTokens
                .FirstOrDefaultAsync(rt => rt.Token == refreshToken && rt.UserId == userId);

            if (storedToken == null)
                throw new InvalidOperationException("Invalid refresh token.");

            if (storedToken.IsRevoked)
                throw new InvalidOperationException("Token already revoked.");

            storedToken.RevokedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
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

        private async Task<AuthResponseDto> GenerateAuthResponseAsync(ApplicationUser user, string role)
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
                Role = role
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
                CreatedAt = DateTime.UtcNow
            };

            _context.RefreshTokens.Add(refreshToken);
            await _context.SaveChangesAsync();

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

        private static string NormalizePhone(string phone) =>
            PhoneNumberHelper.NormalizeAndValidate(phone);

        public async Task ResetPasswordAsync(string phoneNumber, string newPassword)
        {
            var phone = NormalizePhone(phoneNumber);

            var canReset = await _otpRepository.HasRecentlyVerifiedOtpAsync(
                phone,
                OtpPurpose.ResetPassword,
                TimeSpan.FromMinutes(5)
            );

            if (!canReset)
                throw new InvalidOperationException(
                    "OTP verification required before resetting password."
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
            await _otpRepository.SaveChangesAsync();
        }

    }
}
