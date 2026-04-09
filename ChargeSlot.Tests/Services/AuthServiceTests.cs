using Xunit;
using Moq;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using ChargeSlot.Api.Services.Implementation;
using ChargeSlot.Api.Models.Identity;
using ChargeSlot.Api.DTOs.Auth;
using ChargeSlot.Api.Repositories.Interfaces;
using ChargeSlot.Api.Data;
using ChargeSlot.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;

namespace ChargeSlot.Tests.Services
{
    /// <summary>
    /// Unit tests cho AuthService - các luồng xác thực cốt lõi:
    ///   1. Login (success / fail cases)
    ///   2. Refresh Token
    ///   3. Register (validation)
    ///   4. Reset Password
    /// </summary>
    public class AuthServiceTests
    {
        private readonly Mock<UserManager<ApplicationUser>>      _userManagerMock;
        private readonly Mock<SignInManager<ApplicationUser>>    _signInManagerMock;
        private readonly Mock<RoleManager<IdentityRole<int>>>    _roleManagerMock;
        private readonly Mock<IConfiguration>                    _configMock;
        private readonly Mock<IUserOtpRepository>                _otpRepoMock;
        private readonly ChargeSlotDbContext                     _context;

        public AuthServiceTests()
        {
            // UserManager mock
            var userStore = new Mock<IUserStore<ApplicationUser>>();
            _userManagerMock = new Mock<UserManager<ApplicationUser>>(
                userStore.Object, null, null, null, null, null, null, null, null);

            // SignInManager mock
            var contextAccessor = new Mock<Microsoft.AspNetCore.Http.IHttpContextAccessor>();
            var claimsFactory   = new Mock<IUserClaimsPrincipalFactory<ApplicationUser>>();
            _signInManagerMock  = new Mock<SignInManager<ApplicationUser>>(
                _userManagerMock.Object, contextAccessor.Object,
                claimsFactory.Object, null, null, null, null);

            // RoleManager mock
            var roleStore    = new Mock<IRoleStore<IdentityRole<int>>>();
            _roleManagerMock = new Mock<RoleManager<IdentityRole<int>>>(
                roleStore.Object, null, null, null, null);

            // IConfiguration mock với JWT config hợp lệ
            _configMock = new Mock<IConfiguration>();
            var jwtSection = new Mock<IConfigurationSection>();
            jwtSection.Setup(x => x["Key"]).Returns("THIS_IS_A_SUPER_SECRET_KEY_AT_LEAST_32_CHARS");
            jwtSection.Setup(x => x["Issuer"]).Returns("chargeSlot");
            jwtSection.Setup(x => x["Audience"]).Returns("chargeSlot");
            jwtSection.Setup(x => x["ExpiresMinutes"]).Returns("60");
            jwtSection.Setup(x => x["RefreshTokenExpiresInDays"]).Returns("7");
            _configMock.Setup(x => x.GetSection("Jwt")).Returns(jwtSection.Object);

            _otpRepoMock = new Mock<IUserOtpRepository>();

            // InMemory DB (unique per test class instance)
            var options = new DbContextOptionsBuilder<ChargeSlotDbContext>()
                .UseInMemoryDatabase($"AuthTestDb_{Guid.NewGuid()}")
                .Options;
            _context = new ChargeSlotDbContext(options);
        }

        private AuthService CreateService() =>
            new AuthService(
                _userManagerMock.Object,
                _roleManagerMock.Object,
                _signInManagerMock.Object,
                _configMock.Object,
                _otpRepoMock.Object,
                _context);

        // ─────────────────────────────────────────────
        // LOGIN
        // ─────────────────────────────────────────────

        /// <summary>
        /// ✅ Login thành công → trả về AccessToken + RefreshToken.
        /// </summary>
        [Fact]
        public async Task Login_ShouldSuccess_AndReturnTokens()
        {
            var user = new ApplicationUser
            {
                Id          = 1,
                Status      = "ACTIVE",
                UserName    = "+84912345678",
                PhoneNumber = "+84912345678"
            };

            _userManagerMock
                .Setup(x => x.FindByNameAsync(It.IsAny<string>()))
                .ReturnsAsync(user);

            _signInManagerMock
                .Setup(x => x.CheckPasswordSignInAsync(user, It.IsAny<string>(), true))
                .ReturnsAsync(SignInResult.Success);

            _userManagerMock
                .Setup(x => x.GetRolesAsync(user))
                .ReturnsAsync(new List<string> { "Driver" });

            var result = await CreateService().LoginAsync(new LoginDto
            {
                PhoneNumber = "0912345678",
                Password    = "Abc@12345"
            });

            Assert.NotNull(result);
            Assert.NotEmpty(result.AccessToken);
            Assert.NotEmpty(result.RefreshToken);
            Assert.Equal("Driver", result.Role);
            Assert.Equal(1, result.UserId);
        }

        /// <summary>
        /// ❌ Số điện thoại không tồn tại → throw InvalidOperationException.
        /// </summary>
        [Fact]
        public async Task Login_ShouldFail_WhenUserNotFound()
        {
            _userManagerMock
                .Setup(x => x.FindByNameAsync(It.IsAny<string>()))
                .ReturnsAsync((ApplicationUser?)null);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().LoginAsync(new LoginDto
                {
                    PhoneNumber = "0999999999",
                    Password    = "password"
                }));
        }

        /// <summary>
        /// ❌ Tài khoản bị INACTIVE/BANNED → throw InvalidOperationException.
        /// Dù mật khẩu đúng vẫn không cho đăng nhập.
        /// </summary>
        [Theory]
        [InlineData("INACTIVE")]
        [InlineData("BANNED")]
        public async Task Login_ShouldFail_WhenAccountNotActive(string status)
        {
            var user = new ApplicationUser { Status = status };

            _userManagerMock
                .Setup(x => x.FindByNameAsync(It.IsAny<string>()))
                .ReturnsAsync(user);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().LoginAsync(new LoginDto
                {
                    PhoneNumber = "0912345678",
                    Password    = "Abc@12345"
                }));

            // Không gọi CheckPassword khi đã biết tài khoản không active
            _signInManagerMock.Verify(x =>
                x.CheckPasswordSignInAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>(), It.IsAny<bool>()),
                Times.Never);
        }

        /// <summary>
        /// ❌ Sai mật khẩu → throw InvalidOperationException.
        /// </summary>
        [Fact]
        public async Task Login_ShouldFail_WhenWrongPassword()
        {
            var user = new ApplicationUser { Status = "ACTIVE" };

            _userManagerMock
                .Setup(x => x.FindByNameAsync(It.IsAny<string>()))
                .ReturnsAsync(user);

            _signInManagerMock
                .Setup(x => x.CheckPasswordSignInAsync(user, It.IsAny<string>(), true))
                .ReturnsAsync(SignInResult.Failed);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().LoginAsync(new LoginDto
                {
                    PhoneNumber = "0912345678",
                    Password    = "WrongPass"
                }));
        }

        // ─────────────────────────────────────────────
        // REFRESH TOKEN
        // ─────────────────────────────────────────────

        /// <summary>
        /// ✅ Refresh token hợp lệ → trả về access token mới, old token bị revoke.
        /// </summary>
        [Fact]
        public async Task RefreshToken_ShouldSuccess_AndRotateToken()
        {
            var user = new ApplicationUser
            {
                Id          = 1,
                FullName    = "Test Driver",
                Status      = "ACTIVE",
                UserName    = "+84912345678",
                PhoneNumber = "+84912345678"
            };

            // Tạo refresh token trong DB
            var existingToken = new RefreshToken
            {
                Token     = "valid-refresh-token",
                UserId    = 1,
                User      = user,
                ExpiresAt = DateTime.UtcNow.AddDays(7),
                CreatedAt = DateTime.UtcNow
            };
            _context.RefreshTokens.Add(existingToken);
            await _context.SaveChangesAsync();

            _userManagerMock
                .Setup(x => x.GetRolesAsync(user))
                .ReturnsAsync(new List<string> { "Driver" });

            var result = await CreateService().RefreshTokenAsync("valid-refresh-token");

            Assert.NotNull(result);
            Assert.NotEmpty(result.AccessToken);
            Assert.NotEmpty(result.RefreshToken);

            // Token cũ phải bị revoke
            Assert.NotNull(existingToken.RevokedAt);
            // Ghi lại token mới thay thế
            Assert.NotNull(existingToken.ReplacedByToken);
            Assert.Equal(result.RefreshToken, existingToken.ReplacedByToken);
        }

        /// <summary>
        /// ❌ Token không tồn tại → throw InvalidOperationException.
        /// </summary>
        [Fact]
        public async Task RefreshToken_ShouldFail_WhenTokenNotFound()
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().RefreshTokenAsync("non-existent-token"));
        }

        /// <summary>
        /// ❌ Token đã bị revoke → throw InvalidOperationException.
        /// Ngăn tái sử dụng token cũ sau khi logout hoặc rotate.
        /// </summary>
        [Fact]
        public async Task RefreshToken_ShouldFail_WhenTokenRevoked()
        {
            var user = new ApplicationUser
            {
                Id          = 1,
                FullName    = "Test User",
                PhoneNumber = "+84900000001",
                Status      = "ACTIVE"
            };

            var revokedToken = new RefreshToken
            {
                Token     = "revoked-token",
                UserId    = 1,
                User      = user,
                ExpiresAt = DateTime.UtcNow.AddDays(7),
                RevokedAt = DateTime.UtcNow.AddMinutes(-10), // đã bị revoke
                CreatedAt = DateTime.UtcNow
            };
            _context.RefreshTokens.Add(revokedToken);
            await _context.SaveChangesAsync();

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().RefreshTokenAsync("revoked-token"));
        }

        /// <summary>
        /// ❌ Token đã hết hạn → throw InvalidOperationException.
        /// </summary>
        [Fact]
        public async Task RefreshToken_ShouldFail_WhenTokenExpired()
        {
            var user = new ApplicationUser
            {
                Id          = 1,
                FullName    = "Test User",
                PhoneNumber = "+84900000002",
                Status      = "ACTIVE"
            };

            var expiredToken = new RefreshToken
            {
                Token     = "expired-token",
                UserId    = 1,
                User      = user,
                ExpiresAt = DateTime.UtcNow.AddDays(-1), // đã hết hạn
                CreatedAt = DateTime.UtcNow.AddDays(-8)
            };
            _context.RefreshTokens.Add(expiredToken);
            await _context.SaveChangesAsync();

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().RefreshTokenAsync("expired-token"));
        }

        /// <summary>
        /// ❌ Token hợp lệ nhưng user bị INACTIVE → throw InvalidOperationException.
        /// </summary>
        [Fact]
        public async Task RefreshToken_ShouldFail_WhenUserInactive()
        {
            var user = new ApplicationUser
            {
                Id          = 1,
                FullName    = "Inactive User",
                PhoneNumber = "+84900000003",
                Status      = "INACTIVE"
            };

            var token = new RefreshToken
            {
                Token     = "active-but-banned-user-token",
                UserId    = 1,
                User      = user,
                ExpiresAt = DateTime.UtcNow.AddDays(7),
                CreatedAt = DateTime.UtcNow
            };
            _context.RefreshTokens.Add(token);
            await _context.SaveChangesAsync();

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().RefreshTokenAsync("active-but-banned-user-token"));
        }

        // ─────────────────────────────────────────────
        // REGISTER
        // ─────────────────────────────────────────────

        /// <summary>
        /// ❌ OTP chưa được verify trước khi register → throw.
        /// Require OTP verification flow trước đăng ký.
        /// </summary>
        [Fact]
        public async Task Register_ShouldFail_WhenOtpNotVerified()
        {
            _otpRepoMock
                .Setup(x => x.HasRecentlyVerifiedOtpAsync(
                    It.IsAny<string>(),
                    It.IsAny<ChargeSlot.Api.Enums.OtpPurpose>(),
                    It.IsAny<TimeSpan>()))
                .ReturnsAsync(false); // OTP chưa verify

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().RegisterAsync(new RegisterDto
                {
                    PhoneNumber = "0900000001",
                    Password    = "Abc@12345",
                    FullName    = "Test User"
                }));
        }

        /// <summary>
        /// ❌ Số điện thoại đã đăng ký → throw (OTP đã verify nhưng phone trùng).
        /// </summary>
        [Fact]
        public async Task Register_ShouldFail_WhenPhoneAlreadyExists()
        {
            _otpRepoMock
                .Setup(x => x.HasRecentlyVerifiedOtpAsync(
                    It.IsAny<string>(),
                    It.IsAny<ChargeSlot.Api.Enums.OtpPurpose>(),
                    It.IsAny<TimeSpan>()))
                .ReturnsAsync(true); // OTP verified

            _userManagerMock
                .Setup(x => x.FindByNameAsync(It.IsAny<string>()))
                .ReturnsAsync(new ApplicationUser()); // phone đã tồn tại

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().RegisterAsync(new RegisterDto
                {
                    PhoneNumber = "0900000001",
                    Password    = "Abc@12345",
                    FullName    = "Test"
                }));
        }

        /// <summary>
        /// ❌ Role không hợp lệ (không nằm trong danh sách cho phép) → throw.
        /// </summary>
        [Fact]
        public async Task Register_ShouldFail_WhenRoleInvalid()
        {
            _otpRepoMock
                .Setup(x => x.HasRecentlyVerifiedOtpAsync(
                    It.IsAny<string>(),
                    It.IsAny<ChargeSlot.Api.Enums.OtpPurpose>(),
                    It.IsAny<TimeSpan>()))
                .ReturnsAsync(true);

            _userManagerMock
                .Setup(x => x.FindByNameAsync(It.IsAny<string>()))
                .ReturnsAsync((ApplicationUser?)null);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().RegisterAsync(new RegisterDto
                {
                    PhoneNumber = "0900000001",
                    Password    = "Abc@12345",
                    FullName    = "Test",
                    Role        = "SuperHacker" // role không tồn tại trong hệ thống
                }));
        }

        // ─────────────────────────────────────────────
        // RESET PASSWORD
        // ─────────────────────────────────────────────

        /// <summary>
        /// ❌ Reset password khi OTP chưa verify → throw.
        /// </summary>
        [Fact]
        public async Task ResetPassword_ShouldFail_WhenOtpNotVerified()
        {
            _otpRepoMock
                .Setup(x => x.HasRecentlyVerifiedOtpAsync(
                    It.IsAny<string>(),
                    ChargeSlot.Api.Enums.OtpPurpose.ResetPassword,
                    It.IsAny<TimeSpan>()))
                .ReturnsAsync(false);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().ResetPasswordAsync("0900000001", "NewPass@123"));
        }

        /// <summary>
        /// ❌ User không tồn tại → throw (dù OTP đã verify).
        /// </summary>
        [Fact]
        public async Task ResetPassword_ShouldFail_WhenUserNotFound()
        {
            _otpRepoMock
                .Setup(x => x.HasRecentlyVerifiedOtpAsync(
                    It.IsAny<string>(),
                    ChargeSlot.Api.Enums.OtpPurpose.ResetPassword,
                    It.IsAny<TimeSpan>()))
                .ReturnsAsync(true);

            _userManagerMock
                .Setup(x => x.FindByNameAsync(It.IsAny<string>()))
                .ReturnsAsync((ApplicationUser?)null);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().ResetPasswordAsync("0999999999", "NewPass@123"));
        }

        // ─────────────────────────────────────────────
        // REVOKE TOKEN
        // ─────────────────────────────────────────────

        /// <summary>
        /// ✅ Revoke token hợp lệ → RevokedAt được set.
        /// </summary>
        [Fact]
        public async Task RevokeToken_ShouldSuccess()
        {
            var token = new RefreshToken
            {
                Token     = "token-to-revoke",
                UserId    = 1,
                ExpiresAt = DateTime.UtcNow.AddDays(7),
                CreatedAt = DateTime.UtcNow
            };
            _context.RefreshTokens.Add(token);
            await _context.SaveChangesAsync();

            await CreateService().RevokeTokenAsync("token-to-revoke", userId: 1);

            Assert.NotNull(token.RevokedAt);
        }

        /// <summary>
        /// ❌ Token không thuộc user → throw InvalidOperationException.
        /// Ngăn user revoke token của người khác.
        /// </summary>
        [Fact]
        public async Task RevokeToken_ShouldFail_WhenTokenNotOwnedByUser()
        {
            var token = new RefreshToken
            {
                Token     = "other-user-token",
                UserId    = 99, // thuộc user 99
                ExpiresAt = DateTime.UtcNow.AddDays(7),
                CreatedAt = DateTime.UtcNow
            };
            _context.RefreshTokens.Add(token);
            await _context.SaveChangesAsync();

            // User 10 cố revoke token của user 99
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().RevokeTokenAsync("other-user-token", userId: 10));
        }

        /// <summary>
        /// ❌ Token đã bị revoke → throw "Token already revoked".
        /// </summary>
        [Fact]
        public async Task RevokeToken_ShouldFail_WhenAlreadyRevoked()
        {
            var token = new RefreshToken
            {
                Token     = "already-revoked",
                UserId    = 1,
                ExpiresAt = DateTime.UtcNow.AddDays(7),
                RevokedAt = DateTime.UtcNow.AddMinutes(-5),
                CreatedAt = DateTime.UtcNow
            };
            _context.RefreshTokens.Add(token);
            await _context.SaveChangesAsync();

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().RevokeTokenAsync("already-revoked", userId: 1));
        }
    }

    // ─────────────────────────────────────────────
    // Helper: IConfigurationSection stub
    // ─────────────────────────────────────────────

    public class ConfigurationSectionStub : IConfigurationSection
    {
        private readonly string _value;

        public ConfigurationSectionStub(string key, string value)
        {
            Key    = key;
            _value = value;
        }

        public string this[string key] { get => _value; set { } }
        public string  Key   { get; }
        public string  Path  => Key;
        public string? Value { get => _value; set { } }

        public IEnumerable<IConfigurationSection> GetChildren() =>
            Enumerable.Empty<IConfigurationSection>();

        public IChangeToken GetReloadToken() => new CancellationChangeToken(CancellationToken.None);

        public IConfigurationSection GetSection(string key) => this;
    }
}