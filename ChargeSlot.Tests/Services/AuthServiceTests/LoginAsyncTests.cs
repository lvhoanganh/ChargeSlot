using Xunit;
using Moq;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using ChargeSlot.Api.Services.Implementation;
using ChargeSlot.Api.Models.Identity;
using ChargeSlot.Api.DTOs.Auth;
using ChargeSlot.Api.Services.Interfaces;
using ChargeSlot.Api.Repositories.Interfaces;
using ChargeSlot.Api.Constants;
using ChargeSlot.Api.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ChargeSlot.Tests.Services.AuthServiceTests
{
    public class LoginTests
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

        // ===== CONSTRUCTOR =====
        public LoginTests()
        {
            var userStore = new Mock<IUserStore<ApplicationUser>>();
            _userManagerMock = new Mock<UserManager<ApplicationUser>>(
                userStore.Object, null, null, null, null, null, null, null, null);

            var httpCtx = new Mock<Microsoft.AspNetCore.Http.IHttpContextAccessor>();
            var claimFactory = new Mock<IUserClaimsPrincipalFactory<ApplicationUser>>();
            _signInManagerMock = new Mock<SignInManager<ApplicationUser>>(
                _userManagerMock.Object, httpCtx.Object, claimFactory.Object, null, null, null, null);

            // Fix: đóng đúng generic type RoleManager<IdentityRole<int>>
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

            // Default: user not found
            _userManagerMock.Setup(x => x.FindByNameAsync(It.IsAny<string>()))
                .ReturnsAsync((ApplicationUser?)null);

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
        private static LoginDto CreateDto(string password = "Abc@12345") => new LoginDto
        {
            PhoneNumber = "0123456789",
            Password = password
        };

        private static ApplicationUser CreateUser(
            string status = UserStatusConstants.Active,
            bool emailConfirmed = true)
        {
            return new ApplicationUser
            {
                UserName = "+84123456789",
                Status = status,
                EmailConfirmed = emailConfirmed,
                Email = "test@mail.com"
            };
        }

        // ================= TEST CASES =================

        // TC01: Đăng nhập thành công với role Driver
        [Fact]
        public async Task Login_Success_Driver()
        {
            var user = CreateUser();

            _userManagerMock.Setup(x => x.FindByNameAsync(It.IsAny<string>()))
                .ReturnsAsync(user);

            _signInManagerMock.Setup(x =>
                x.CheckPasswordSignInAsync(user, It.IsAny<string>(), true))
                .ReturnsAsync(SignInResult.Success);

            _userManagerMock.Setup(x => x.GetRolesAsync(user))
                .ReturnsAsync(new List<string> { RoleConstants.Driver });

            var result = await CreateService().LoginAsync(CreateDto());

            Assert.NotNull(result);
            Assert.Equal(RoleConstants.Driver, result.Role);
        }

        // TC02: Đăng nhập thành công với role Owner
        [Fact]
        public async Task Login_Success_Owner()
        {
            var user = CreateUser();

            _userManagerMock.Setup(x => x.FindByNameAsync(It.IsAny<string>()))
                .ReturnsAsync(user);

            _signInManagerMock.Setup(x =>
                x.CheckPasswordSignInAsync(user, It.IsAny<string>(), true))
                .ReturnsAsync(SignInResult.Success);

            _userManagerMock.Setup(x => x.GetRolesAsync(user))
                .ReturnsAsync(new List<string> { RoleConstants.Owner });

            var result = await CreateService().LoginAsync(CreateDto());

            Assert.NotNull(result);
            Assert.Equal(RoleConstants.Owner, result.Role);
        }

        // TC03: Không tìm thấy user → throw
        [Fact]
        public async Task Login_UserNotFound_ShouldThrow()
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().LoginAsync(CreateDto()));
        }

        // TC04: Tài khoản đang chờ xác thực email → throw
        [Fact]
        public async Task Login_PendingEmail_ShouldThrow()
        {
            var user = CreateUser(UserStatusConstants.PendingEmailVerification);

            _userManagerMock.Setup(x => x.FindByNameAsync(It.IsAny<string>()))
                .ReturnsAsync(user);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().LoginAsync(CreateDto()));
        }

        // TC05: Tài khoản bị banned → throw
        [Fact]
        public async Task Login_Banned_ShouldThrow()
        {
            var user = CreateUser(UserStatusConstants.Banned);

            _userManagerMock.Setup(x => x.FindByNameAsync(It.IsAny<string>()))
                .ReturnsAsync(user);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().LoginAsync(CreateDto()));
        }

        // TC06: Sai mật khẩu → throw
        [Fact]
        public async Task Login_WrongPassword_ShouldThrow()
        {
            var user = CreateUser();

            _userManagerMock.Setup(x => x.FindByNameAsync(It.IsAny<string>()))
                .ReturnsAsync(user);

            _signInManagerMock.Setup(x =>
                x.CheckPasswordSignInAsync(user, It.IsAny<string>(), true))
                .ReturnsAsync(SignInResult.Failed);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().LoginAsync(CreateDto("WrongPass")));
        }

        // TC07: Email chưa xác thực (non-Admin) → đăng nhập thành công nhưng RequiresEmail = true
        [Fact]
        public async Task Login_EmailNotConfirmed_Driver_ShouldReturnRequiresEmail()
        {
            var user = CreateUser(emailConfirmed: false);

            _userManagerMock.Setup(x => x.FindByNameAsync(It.IsAny<string>()))
                .ReturnsAsync(user);

            _signInManagerMock.Setup(x =>
                x.CheckPasswordSignInAsync(user, It.IsAny<string>(), true))
                .ReturnsAsync(SignInResult.Success);

            _userManagerMock.Setup(x => x.GetRolesAsync(user))
                .ReturnsAsync(new List<string> { RoleConstants.Driver });

            var result = await CreateService().LoginAsync(CreateDto());

            Assert.NotNull(result);
            Assert.True(result.RequiresEmail);
        }

        // TC08: Admin chưa confirm email → RequiresEmail = false (Admin miễn email)
        [Fact]
        public async Task Login_Admin_EmailNotConfirmed_ShouldNotRequireEmail()
        {
            var user = CreateUser(emailConfirmed: false);

            _userManagerMock.Setup(x => x.FindByNameAsync(It.IsAny<string>()))
                .ReturnsAsync(user);

            _signInManagerMock.Setup(x =>
                x.CheckPasswordSignInAsync(user, It.IsAny<string>(), true))
                .ReturnsAsync(SignInResult.Success);

            _userManagerMock.Setup(x => x.GetRolesAsync(user))
                .ReturnsAsync(new List<string> { RoleConstants.Admin });

            var result = await CreateService().LoginAsync(CreateDto());

            Assert.NotNull(result);
            Assert.False(result.RequiresEmail);
        }

        // TC09: User đã confirm email → RequiresEmail = false
        [Fact]
        public async Task Login_EmailConfirmed_ShouldNotRequireEmail()
        {
            var user = CreateUser(emailConfirmed: true);

            _userManagerMock.Setup(x => x.FindByNameAsync(It.IsAny<string>()))
                .ReturnsAsync(user);

            _signInManagerMock.Setup(x =>
                x.CheckPasswordSignInAsync(user, It.IsAny<string>(), true))
                .ReturnsAsync(SignInResult.Success);

            _userManagerMock.Setup(x => x.GetRolesAsync(user))
                .ReturnsAsync(new List<string> { RoleConstants.Driver });

            var result = await CreateService().LoginAsync(CreateDto());

            Assert.NotNull(result);
            Assert.False(result.RequiresEmail);
        }
    }
}
