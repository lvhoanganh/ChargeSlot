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

namespace ChargeSlot.Tests.Services.AuthServiceTests
{
    /// <summary>
    /// Base class chứa toàn bộ mock chung cho AuthService tests.
    /// Kế thừa class này thay vì copy-paste mock setup vào mỗi test class.
    /// </summary>
    public abstract class AuthServiceTestBase
    {
        // ─── Mocks ───
        protected readonly Mock<UserManager<ApplicationUser>> _userManagerMock;
        protected readonly Mock<SignInManager<ApplicationUser>> _signInManagerMock;
        protected readonly Mock<RoleManager<IdentityRole<int>>> _roleManagerMock;
        protected readonly Mock<IConfiguration> _configMock;
        protected readonly Mock<IUserOtpRepository> _otpRepoMock = new();
        protected readonly Mock<IFirebaseAuthService> _firebaseMock = new();
        protected readonly Mock<IOwnerRepository> _ownerRepoMock = new();
        protected readonly Mock<IDriverRepository> _driverRepoMock = new();
        protected readonly Mock<IRefreshTokenRepository> _refreshTokenMock = new();
        protected readonly Mock<IUnitOfWork> _uowMock = new();
        protected readonly Mock<IEmailService> _emailMock = new();
        protected readonly Mock<ILogger<AuthService>> _loggerMock = new();

        protected AuthServiceTestBase()
        {
            // ── UserManager ──
            var userStore = new Mock<IUserStore<ApplicationUser>>();
            _userManagerMock = new Mock<UserManager<ApplicationUser>>(
                userStore.Object, null, null, null, null, null, null, null, null);

            // ── SignInManager ──
            var httpCtx = new Mock<Microsoft.AspNetCore.Http.IHttpContextAccessor>();
            var claimFactory = new Mock<IUserClaimsPrincipalFactory<ApplicationUser>>();
            _signInManagerMock = new Mock<SignInManager<ApplicationUser>>(
                _userManagerMock.Object, httpCtx.Object, claimFactory.Object, null, null, null, null);

            // ── RoleManager ──
            var roleStore = new Mock<IRoleStore<IdentityRole<int>>>();
            _roleManagerMock = new Mock<RoleManager<IdentityRole<int>>>(
                roleStore.Object, null, null, null, null);

            // ── Configuration (JWT) ──
            _configMock = new Mock<IConfiguration>();
            var jwtSection = new Mock<IConfigurationSection>();
            jwtSection.Setup(s => s["Key"]).Returns("super-secret-key-that-is-long-enough-32chars");
            jwtSection.Setup(s => s["Issuer"]).Returns("TestIssuer");
            jwtSection.Setup(s => s["Audience"]).Returns("TestAudience");
            jwtSection.Setup(s => s["ExpiresMinutes"]).Returns("60");
            jwtSection.Setup(s => s["RefreshTokenExpiresInDays"]).Returns("7");
            _configMock.Setup(c => c.GetSection("Jwt")).Returns(jwtSection.Object);

            // ── Default behaviors ──
            _uowMock.Setup(x => x.CompleteAsync()).ReturnsAsync(1);
            _emailMock.Setup(x => x.SendEmailAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);
            _refreshTokenMock.Setup(x => x.Add(It.IsAny<RefreshToken>()));
            _roleManagerMock.Setup(x => x.RoleExistsAsync(It.IsAny<string>()))
                .ReturnsAsync(true);
            _userManagerMock.Setup(x => x.GetRolesAsync(It.IsAny<ApplicationUser>()))
                .ReturnsAsync(new List<string>());
            _userManagerMock.Setup(x => x.AddToRoleAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
                .ReturnsAsync(IdentityResult.Success);
            _otpRepoMock.Setup(x => x.InvalidateAllOtpsAsync(It.IsAny<string>()))
                .Returns(Task.CompletedTask);
            // Default: không tìm thấy user (các test cụ thể override nếu cần)
            _userManagerMock.Setup(x => x.FindByNameAsync(It.IsAny<string>()))
                .ReturnsAsync((ApplicationUser?)null);
            _userManagerMock.Setup(x => x.FindByEmailAsync(It.IsAny<string>()))
                .ReturnsAsync((ApplicationUser?)null);
            _userManagerMock.Setup(x => x.FindByIdAsync(It.IsAny<string>()))
                .ReturnsAsync((ApplicationUser?)null);
            _refreshTokenMock.Setup(x => x.GetByTokenAsync(It.IsAny<string>()))
                .ReturnsAsync((RefreshToken?)null);
            // Default Firebase mock → trả null (tests override khi cần match phone)
            _firebaseMock.Setup(x => x.VerifyTokenAndGetPhoneAsync(It.IsAny<string>()))
                .ReturnsAsync((string?)null);
        }

        /// <summary>Tạo instance AuthService với toàn bộ mock đã cấu hình.</summary>
        protected AuthService CreateService() => new AuthService(
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

        // ─── Common Helpers ───

        /// <summary>Tạo ApplicationUser đang Active với email đã xác thực.</summary>
        protected static ApplicationUser CreateActiveUser(
            int id = 1,
            string status = UserStatusConstants.Active,
            bool emailConfirmed = true,
            string email = "user@test.com") => new ApplicationUser
            {
                Id = id,
                UserName = "+84123456789",
                FullName = "Test User",
                Status = status,
                Email = email,
                EmailConfirmed = emailConfirmed
            };

        /// <summary>Tạo RefreshToken hợp lệ (hoặc expired/revoked tùy param).</summary>
        protected static RefreshToken CreateRefreshToken(
            ApplicationUser user,
            string token = "test_token",
            bool isExpired = false,
            bool isRevoked = false) => new RefreshToken
            {
                Token = token,
                User = user,
                ExpiresAt = isExpired ? DateTime.UtcNow.AddDays(-1) : DateTime.UtcNow.AddDays(7),
                RevokedAt = isRevoked ? DateTime.UtcNow : (DateTime?)null
            };
    }
}
