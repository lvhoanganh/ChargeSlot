using Xunit;
using Moq;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using ChargeSlot.Api.Services.Implementation;
using ChargeSlot.Api.Models.Identity;
using ChargeSlot.Api.Models;
using ChargeSlot.Api.Repositories.Interfaces;
using ChargeSlot.Api.Services.Interfaces;
using ChargeSlot.Api.Constants;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ChargeSlot.Tests.Services.AuthServiceTests
{
    public class RefreshTokenTests
    {
        // ===== MOCKS =====
        private readonly Mock<UserManager<ApplicationUser>> _userManagerMock;
        private readonly Mock<SignInManager<ApplicationUser>> _signInManagerMock;
        private readonly Mock<RoleManager<IdentityRole<int>>> _roleManagerMock;
        private readonly Mock<IConfiguration> _configMock;
        private readonly Mock<IUserOtpRepository> _otpRepoMock = new();
        private readonly Mock<IFirebaseAuthService> _firebaseMock = new();
        private readonly Mock<IOwnerRepository> _ownerRepoMock = new();
        private readonly Mock<IDriverRepository> _driverRepoMock = new();
        private readonly Mock<IRefreshTokenRepository> _refreshTokenMock = new();
        private readonly Mock<IUnitOfWork> _uowMock = new();
        private readonly Mock<IEmailService> _emailMock = new();
        private readonly Mock<ILogger<AuthService>> _loggerMock = new();

        private const string TOKEN = "test_refresh_token";

        // ===== CONSTRUCTOR =====
        public RefreshTokenTests()
        {
            var userStore = new Mock<IUserStore<ApplicationUser>>();
            _userManagerMock = new Mock<UserManager<ApplicationUser>>(
                userStore.Object, null, null, null, null, null, null, null, null);

            var httpCtx = new Mock<Microsoft.AspNetCore.Http.IHttpContextAccessor>();
            var claimFactory = new Mock<IUserClaimsPrincipalFactory<ApplicationUser>>();
            _signInManagerMock = new Mock<SignInManager<ApplicationUser>>(
                _userManagerMock.Object, httpCtx.Object, claimFactory.Object, null, null, null, null);

            var roleStore = new Mock<IRoleStore<IdentityRole<int>>>();
            _roleManagerMock = new Mock<RoleManager<IdentityRole<int>>>(roleStore.Object, null, null, null, null);

            _configMock = new Mock<IConfiguration>();

            // Setup JWT config section để GenerateUserJwt không throw
            var jwtSection = new Mock<IConfigurationSection>();
            jwtSection.Setup(s => s["Key"]).Returns("super-secret-key-that-is-long-enough-32chars");
            jwtSection.Setup(s => s["Issuer"]).Returns("TestIssuer");
            jwtSection.Setup(s => s["Audience"]).Returns("TestAudience");
            jwtSection.Setup(s => s["ExpiresMinutes"]).Returns("60");
            jwtSection.Setup(s => s["RefreshTokenExpiresInDays"]).Returns("7");
            _configMock.Setup(c => c.GetSection("Jwt")).Returns(jwtSection.Object);

            // Setup UoW và RefreshToken repo
            _uowMock.Setup(x => x.CompleteAsync()).ReturnsAsync(1);
            _refreshTokenMock.Setup(x => x.Add(It.IsAny<RefreshToken>()));

            // Default: token không tồn tại
            _refreshTokenMock.Setup(x => x.GetByTokenAsync(It.IsAny<string>()))
                .ReturnsAsync((RefreshToken?)null);

            _userManagerMock.Setup(x => x.GetRolesAsync(It.IsAny<ApplicationUser>()))
                .ReturnsAsync(new List<string>());
        }

        // ===== FACTORY =====
        private AuthService CreateService() => new AuthService(
            _userManagerMock.Object,
            _roleManagerMock.Object,
            _signInManagerMock.Object,
            _configMock.Object,
            _otpRepoMock.Object,
            _firebaseMock.Object,
            _ownerRepoMock.Object,
            _driverRepoMock.Object,
            _refreshTokenMock.Object,
            _uowMock.Object,
            _emailMock.Object,
            _loggerMock.Object);

        // ===== HELPER =====
        // IsExpired = computed (ExpiresAt <= UtcNow), IsRevoked = computed (RevokedAt != null)
        // → Phải set ExpiresAt/RevokedAt thay vì gán trực tiếp property
        private static RefreshToken BuildToken(
            ApplicationUser user,
            bool isExpired = false,
            bool isRevoked = false)
        {
            return new RefreshToken
            {
                Token = TOKEN,
                User = user,
                ExpiresAt = isExpired
                    ? DateTime.UtcNow.AddDays(-1)   // đã hết hạn
                    : DateTime.UtcNow.AddDays(7),   // còn hạn
                RevokedAt = isRevoked
                    ? DateTime.UtcNow                // đã bị revoke
                    : (DateTime?)null
            };
        }

        // ===============================
        // TC01 - SUCCESS DRIVER
        // ===============================
        [Fact]
        public async Task TC01_ValidDriver_ShouldSuccess()
        {
            var user = new ApplicationUser
            {
                Status = UserStatusConstants.Active,
                Email = "driver@gmail.com",
                EmailConfirmed = true
            };

            var token = BuildToken(user);

            _refreshTokenMock.Setup(x => x.GetByTokenAsync(TOKEN))
                .ReturnsAsync(token);

            _userManagerMock.Setup(x => x.GetRolesAsync(user))
                .ReturnsAsync(new List<string> { RoleConstants.Driver });

            var result = await CreateService().RefreshTokenAsync(TOKEN);

            Assert.NotNull(result);
            Assert.Equal(RoleConstants.Driver, result.Role);
            Assert.False(result.RequiresEmail);
        }

        // ===============================
        // TC02 - SUCCESS OWNER
        // ===============================
        [Fact]
        public async Task TC02_ValidOwner_ShouldSuccess()
        {
            var user = new ApplicationUser
            {
                Status = UserStatusConstants.Active,
                Email = "owner@gmail.com",
                EmailConfirmed = true
            };

            var token = BuildToken(user);

            _refreshTokenMock.Setup(x => x.GetByTokenAsync(TOKEN))
                .ReturnsAsync(token);

            _userManagerMock.Setup(x => x.GetRolesAsync(user))
                .ReturnsAsync(new List<string> { RoleConstants.Owner });

            var result = await CreateService().RefreshTokenAsync(TOKEN);

            Assert.NotNull(result);
            Assert.Equal(RoleConstants.Owner, result.Role);
        }

        // ===============================
        // TC03 - TOKEN NOT EXIST
        // ===============================
        [Fact]
        public async Task TC03_TokenNotExist_ShouldThrow()
        {
            // Default mock đã trả null → không cần setup thêm

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().RefreshTokenAsync(TOKEN));
        }

        // ===============================
        // TC04 - TOKEN REVOKED
        // ===============================
        [Fact]
        public async Task TC04_TokenRevoked_ShouldThrow()
        {
            var user = new ApplicationUser { Status = UserStatusConstants.Active };
            // isRevoked: true → RevokedAt = UtcNow → IsRevoked computed = true
            var token = BuildToken(user, isRevoked: true);

            _refreshTokenMock.Setup(x => x.GetByTokenAsync(TOKEN))
                .ReturnsAsync(token);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().RefreshTokenAsync(TOKEN));
        }

        // ===============================
        // TC05 - TOKEN EXPIRED
        // ===============================
        [Fact]
        public async Task TC05_TokenExpired_ShouldThrow()
        {
            var user = new ApplicationUser { Status = UserStatusConstants.Active };
            // isExpired: true → ExpiresAt = yesterday → IsExpired computed = true
            var token = BuildToken(user, isExpired: true);

            _refreshTokenMock.Setup(x => x.GetByTokenAsync(TOKEN))
                .ReturnsAsync(token);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().RefreshTokenAsync(TOKEN));
        }

        // ===============================
        // TC06 - USER SUSPENDED
        // ===============================
        // UserStatusConstants.Inactive không tồn tại trong hệ thống.
        // Thay bằng Suspended — cũng không phải Active, logic service sẽ throw.
        [Fact]
        public async Task TC06_UserSuspended_ShouldThrow()
        {
            var user = new ApplicationUser { Status = UserStatusConstants.Suspended };
            var token = BuildToken(user);

            _refreshTokenMock.Setup(x => x.GetByTokenAsync(TOKEN))
                .ReturnsAsync(token);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().RefreshTokenAsync(TOKEN));
        }

        // ===============================
        // TC07 - USER BANNED
        // ===============================
        [Fact]
        public async Task TC07_UserBanned_ShouldThrow()
        {
            var user = new ApplicationUser { Status = UserStatusConstants.Banned };
            var token = BuildToken(user);

            _refreshTokenMock.Setup(x => x.GetByTokenAsync(TOKEN))
                .ReturnsAsync(token);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().RefreshTokenAsync(TOKEN));
        }

        // ===============================
        // TC08 - EMAIL NOT CONFIRMED
        // ===============================
        [Fact]
        public async Task TC08_EmailNotConfirmed_ShouldRequireEmail()
        {
            var user = new ApplicationUser
            {
                Status = UserStatusConstants.Active,
                Email = "user@gmail.com",
                EmailConfirmed = false
            };

            var token = BuildToken(user);

            _refreshTokenMock.Setup(x => x.GetByTokenAsync(TOKEN))
                .ReturnsAsync(token);

            _userManagerMock.Setup(x => x.GetRolesAsync(user))
                .ReturnsAsync(new List<string> { RoleConstants.Driver });

            var result = await CreateService().RefreshTokenAsync(TOKEN);

            Assert.NotNull(result);
            Assert.True(result.RequiresEmail);
        }

        // ===============================
        // TC09 - EMAIL NULL
        // ===============================
        [Fact]
        public async Task TC09_EmailNull_ShouldRequireEmail()
        {
            var user = new ApplicationUser
            {
                Status = UserStatusConstants.Active,
                Email = null,
                EmailConfirmed = false
            };

            var token = BuildToken(user);

            _refreshTokenMock.Setup(x => x.GetByTokenAsync(TOKEN))
                .ReturnsAsync(token);

            _userManagerMock.Setup(x => x.GetRolesAsync(user))
                .ReturnsAsync(new List<string> { RoleConstants.Driver });

            var result = await CreateService().RefreshTokenAsync(TOKEN);

            Assert.NotNull(result);
            Assert.True(result.RequiresEmail);
        }

        // ===============================
        // TC10 - ADMIN BYPASS EMAIL
        // ===============================
        [Fact]
        public async Task TC10_Admin_ShouldNotRequireEmail()
        {
            var user = new ApplicationUser
            {
                Status = UserStatusConstants.Active,
                Email = null,
                EmailConfirmed = false
            };

            var token = BuildToken(user);

            _refreshTokenMock.Setup(x => x.GetByTokenAsync(TOKEN))
                .ReturnsAsync(token);

            _userManagerMock.Setup(x => x.GetRolesAsync(user))
                .ReturnsAsync(new List<string> { RoleConstants.Admin });

            var result = await CreateService().RefreshTokenAsync(TOKEN);

            Assert.NotNull(result);
            Assert.False(result.RequiresEmail);
        }

        // ===============================
        // TC11 - TOKEN ROTATION
        // ===============================
        [Fact]
        public async Task TC11_TokenRotation_ShouldUpdateFields()
        {
            var user = new ApplicationUser
            {
                Status = UserStatusConstants.Active,
                Email = "test@gmail.com",
                EmailConfirmed = true
            };

            var token = BuildToken(user);

            _refreshTokenMock.Setup(x => x.GetByTokenAsync(TOKEN))
                .ReturnsAsync(token);

            _userManagerMock.Setup(x => x.GetRolesAsync(user))
                .ReturnsAsync(new List<string> { RoleConstants.Driver });

            var result = await CreateService().RefreshTokenAsync(TOKEN);

            // Token cũ phải bị revoke và gắn token mới
            Assert.NotNull(token.RevokedAt);
            Assert.NotNull(token.ReplacedByToken);
            Assert.NotEmpty(token.ReplacedByToken!);
            Assert.Equal(result.RefreshToken, token.ReplacedByToken);
        }
    }
}
