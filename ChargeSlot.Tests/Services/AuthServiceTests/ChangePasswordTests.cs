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
using System.Threading.Tasks;

namespace ChargeSlot.Tests.Services.AuthServiceTests
{
    public class ChangePasswordTests
    {
        private readonly Mock<UserManager<ApplicationUser>> _userManagerMock;
        private readonly Mock<SignInManager<ApplicationUser>> _signInManagerMock;
        private readonly Mock<RoleManager<IdentityRole<int>>> _roleManagerMock;
        private readonly Mock<IConfiguration> _configMock = new();
        private readonly Mock<IUserOtpRepository> _otpRepoMock = new();
        private readonly Mock<IFirebaseAuthService> _firebaseMock = new();
        private readonly Mock<IOwnerRepository> _ownerRepoMock = new();
        private readonly Mock<IDriverRepository> _driverRepoMock = new();
        private readonly Mock<IRefreshTokenRepository> _refreshTokenMock = new();
        private readonly Mock<IUnitOfWork> _uowMock = new();
        private readonly Mock<IEmailService> _emailMock = new();
        private readonly Mock<ILogger<AuthService>> _loggerMock = new();

        private const int USER_ID = 1;
        private const string OLD_PASS = "OldPass@123";
        private const string NEW_PASS = "NewPass@123";

        public ChangePasswordTests()
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

            // Default: user không tồn tại
            _userManagerMock.Setup(x => x.FindByIdAsync(USER_ID.ToString()))
                .ReturnsAsync((ApplicationUser?)null);
        }

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

        // HELPER 
        private static ApplicationUser BuildUser() => new ApplicationUser
        {
            Id = USER_ID,
            FullName = "Test User"
        };

        // TC01 - User not found → throw
        [Fact]
        public async Task TC01_UserNotFound_ShouldThrow()
        {
            // Default mock đã trả null → không cần setup thêm

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().ChangePasswordAsync(USER_ID, OLD_PASS, NEW_PASS));
        }

        // TC02 - Success
        [Fact]
        public async Task TC02_Success_ShouldPass()
        {
            var user = BuildUser();

            _userManagerMock.Setup(x => x.FindByIdAsync(USER_ID.ToString()))
                .ReturnsAsync(user);

            _userManagerMock.Setup(x => x.ChangePasswordAsync(user, OLD_PASS, NEW_PASS))
                .ReturnsAsync(IdentityResult.Success);

            await CreateService().ChangePasswordAsync(USER_ID, OLD_PASS, NEW_PASS);

            _userManagerMock.Verify(x => x.ChangePasswordAsync(user, OLD_PASS, NEW_PASS), Times.Once);
        }

        // TC03 - Sai mật khẩu hiện tại → throw
        [Fact]
        public async Task TC03_WrongCurrentPassword_ShouldThrow()
        {
            var user = BuildUser();

            _userManagerMock.Setup(x => x.FindByIdAsync(USER_ID.ToString()))
                .ReturnsAsync(user);

            _userManagerMock.Setup(x => x.ChangePasswordAsync(user, "WrongPass", NEW_PASS))
                .ReturnsAsync(IdentityResult.Failed(
                    new IdentityError { Description = "Incorrect password." }));

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().ChangePasswordAsync(USER_ID, "WrongPass", NEW_PASS));

            Assert.Contains("Incorrect password.", ex.Message);
        }

        // TC04 - Mật khẩu mới không đủ mạnh → throw
        [Fact]
        public async Task TC04_NewPasswordTooWeak_ShouldThrow()
        {
            var user = BuildUser();

            _userManagerMock.Setup(x => x.FindByIdAsync(USER_ID.ToString()))
                .ReturnsAsync(user);

            _userManagerMock.Setup(x => x.ChangePasswordAsync(user, OLD_PASS, "123"))
                .ReturnsAsync(IdentityResult.Failed(
                    new IdentityError { Description = "Password too weak" }));

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().ChangePasswordAsync(USER_ID, OLD_PASS, "123"));

            Assert.Contains("Password too weak", ex.Message);
        }

        // TC05 - Nhiều lỗi cùng lúc → message gộp đủ các lỗi
        [Fact]
        public async Task TC05_MultipleErrors_ShouldThrowCombinedMessage()
        {
            var user = BuildUser();

            _userManagerMock.Setup(x => x.FindByIdAsync(USER_ID.ToString()))
                .ReturnsAsync(user);

            _userManagerMock.Setup(x => x.ChangePasswordAsync(user, OLD_PASS, "123"))
                .ReturnsAsync(IdentityResult.Failed(
                    new IdentityError { Description = "Too short" },
                    new IdentityError { Description = "Need digit" }));

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().ChangePasswordAsync(USER_ID, OLD_PASS, "123"));

            // Service join bằng "; "
            Assert.Contains("Too short", ex.Message);
            Assert.Contains("Need digit", ex.Message);
        }

        // TC06 - currentPassword null → Identity xử lý và trả lỗi → throw
        [Fact]
        public async Task TC06_CurrentPasswordNull_ShouldThrow()
        {
            var user = BuildUser();

            _userManagerMock.Setup(x => x.FindByIdAsync(USER_ID.ToString()))
                .ReturnsAsync(user);

            _userManagerMock.Setup(x => x.ChangePasswordAsync(user, null!, NEW_PASS))
                .ReturnsAsync(IdentityResult.Failed(
                    new IdentityError { Description = "Current password cannot be null" }));

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().ChangePasswordAsync(USER_ID, null!, NEW_PASS));
        }

        // TC07 - newPassword null → Identity xử lý và trả lỗi → throw
        [Fact]
        public async Task TC07_NewPasswordNull_ShouldThrow()
        {
            var user = BuildUser();

            _userManagerMock.Setup(x => x.FindByIdAsync(USER_ID.ToString()))
                .ReturnsAsync(user);

            _userManagerMock.Setup(x => x.ChangePasswordAsync(user, OLD_PASS, null!))
                .ReturnsAsync(IdentityResult.Failed(
                    new IdentityError { Description = "New password cannot be null" }));

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().ChangePasswordAsync(USER_ID, OLD_PASS, null!));
        }

        // TC08 - newPassword == currentPassword → tuỳ policy (mock cho pass)
        [Fact]
        public async Task TC08_NewEqualsCurrent_PolicyAllows_ShouldPass()
        {
            var user = BuildUser();

            _userManagerMock.Setup(x => x.FindByIdAsync(USER_ID.ToString()))
                .ReturnsAsync(user);

            // Mock cho phép đổi cùng mật khẩu (policy không chặn)
            _userManagerMock.Setup(x => x.ChangePasswordAsync(user, OLD_PASS, OLD_PASS))
                .ReturnsAsync(IdentityResult.Success);

            await CreateService().ChangePasswordAsync(USER_ID, OLD_PASS, OLD_PASS);

            _userManagerMock.Verify(x => x.ChangePasswordAsync(user, OLD_PASS, OLD_PASS), Times.Once);
        }

        // TC09 - ChangePasswordAsync trả lỗi "User locked" → throw
        [Fact]
        public async Task TC09_IdentityReturnsLockedError_ShouldThrow()
        {
            var user = BuildUser();

            _userManagerMock.Setup(x => x.FindByIdAsync(USER_ID.ToString()))
                .ReturnsAsync(user);

            _userManagerMock.Setup(x => x.ChangePasswordAsync(user, OLD_PASS, NEW_PASS))
                .ReturnsAsync(IdentityResult.Failed(
                    new IdentityError { Description = "User is locked out" }));

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().ChangePasswordAsync(USER_ID, OLD_PASS, NEW_PASS));

            Assert.Contains("User is locked out", ex.Message);
        }

        // TC10 - Lỗi không xác định → throw với message từ Identity
        public async Task TC10_UnknownIdentityError_ShouldThrow()
        {
            var user = BuildUser();

            _userManagerMock.Setup(x => x.FindByIdAsync(USER_ID.ToString()))
                .ReturnsAsync(user);

            _userManagerMock.Setup(x => x.ChangePasswordAsync(user, OLD_PASS, NEW_PASS))
                .ReturnsAsync(IdentityResult.Failed(
                    new IdentityError { Description = "Unknown error occurred" }));

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().ChangePasswordAsync(USER_ID, OLD_PASS, NEW_PASS));

            Assert.Contains("Unknown error occurred", ex.Message);
        }
    }
}
