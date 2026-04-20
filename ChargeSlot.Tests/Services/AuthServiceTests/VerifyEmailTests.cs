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
    public class VerifyEmailTests
    {
        // ===== MOCKS =====
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
        // Dùng token plain (không có ký tự %-encoded) để sau Uri.UnescapeDataString vẫn
        // khớp chính xác với chuỗi được mock.
        private const string TOKEN = "test_token";

        public VerifyEmailTests()
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

            // Default UpdateAsync luôn thành công
            _userManagerMock.Setup(x => x.UpdateAsync(It.IsAny<ApplicationUser>()))
                .ReturnsAsync(IdentityResult.Success);
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

        private static ApplicationUser BuildUser(
            string? pendingEmail = null,
            bool emailConfirmed = false,
            string status = UserStatusConstants.Active)
        {
            return new ApplicationUser
            {
                Id = USER_ID,
                FullName = "Test User",
                Email = "old@gmail.com",
                PendingEmail = pendingEmail,
                EmailConfirmed = emailConfirmed,
                Status = status
            };
        }

        // TC01 - USER NOT FOUND
        [Fact]
        public async Task TC01_UserNotFound_ShouldThrow()
        {
            // Default mock đã trả null → không cần setup thêm
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().VerifyEmailAsync(USER_ID, TOKEN));
        }

        // TC02 - CONFIRM EMAIL FAIL (token sai/hết hạn)
        [Fact]
        public async Task TC02_ConfirmEmailFail_ShouldThrow()
        {
            var user = BuildUser();

            _userManagerMock.Setup(x => x.FindByIdAsync(USER_ID.ToString()))
                .ReturnsAsync(user);

            _userManagerMock.Setup(x => x.ConfirmEmailAsync(user, TOKEN))
                .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Invalid token" }));

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().VerifyEmailAsync(USER_ID, TOKEN));
        }

        // TC03 - CHANGE EMAIL FAIL
        [Fact]
        public async Task TC03_ChangeEmailFail_ShouldThrow()
        {
            var user = BuildUser(pendingEmail: "new@gmail.com");

            _userManagerMock.Setup(x => x.FindByIdAsync(USER_ID.ToString()))
                .ReturnsAsync(user);

            _userManagerMock.Setup(x => x.ChangeEmailAsync(user, "new@gmail.com", TOKEN))
                .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Invalid token" }));

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().VerifyEmailAsync(USER_ID, TOKEN));
        }

        // TC04 - EMAIL ALREADY CONFIRMED (không có PendingEmail)
        [Fact]
        public async Task TC04_EmailAlreadyConfirmed_ShouldThrow()
        {
            // emailConfirmed = true, không có pendingEmail
            // → vào nhánh xác thực lần đầu, nhưng EmailConfirmed đã true → throw
            var user = BuildUser(emailConfirmed: true);

            _userManagerMock.Setup(x => x.FindByIdAsync(USER_ID.ToString()))
                .ReturnsAsync(user);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().VerifyEmailAsync(USER_ID, TOKEN));
        }

        // TC05 - CHANGE EMAIL SUCCESS
        [Fact]
        public async Task TC05_ChangeEmailSuccess_ShouldUpdateUser()
        {
            var user = BuildUser(pendingEmail: "new@gmail.com");

            _userManagerMock.Setup(x => x.FindByIdAsync(USER_ID.ToString()))
                .ReturnsAsync(user);

            _userManagerMock.Setup(x => x.ChangeEmailAsync(user, "new@gmail.com", TOKEN))
                .ReturnsAsync(IdentityResult.Success);

            await CreateService().VerifyEmailAsync(USER_ID, TOKEN);

            Assert.True(user.EmailConfirmed);
            Assert.Null(user.PendingEmail);
            _userManagerMock.Verify(x => x.UpdateAsync(user), Times.AtLeastOnce);
        }

        // TC06 - VERIFY EMAIL SUCCESS (xác thực lần đầu)
        [Fact]
        public async Task TC06_VerifyEmailSuccess_ShouldConfirm()
        {
            var user = BuildUser(); // không có pendingEmail, emailConfirmed = false

            _userManagerMock.Setup(x => x.FindByIdAsync(USER_ID.ToString()))
                .ReturnsAsync(user);

            _userManagerMock.Setup(x => x.ConfirmEmailAsync(user, TOKEN))
                .ReturnsAsync(IdentityResult.Success);

            await CreateService().VerifyEmailAsync(USER_ID, TOKEN);

            // EmailConfirmed được set bởi ConfirmEmailAsync (Identity framework),
            // ta chỉ verify service không throw và flow hoàn thành
            _userManagerMock.Verify(x => x.ConfirmEmailAsync(user, TOKEN), Times.Once);
        }

        // TC07 - VERIFY EMAIL FAIL
        [Fact]
        public async Task TC07_VerifyEmailFail_ShouldThrow()
        {
            var user = BuildUser();

            _userManagerMock.Setup(x => x.FindByIdAsync(USER_ID.ToString()))
                .ReturnsAsync(user);

            _userManagerMock.Setup(x => x.ConfirmEmailAsync(user, TOKEN))
                .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Invalid token" }));

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().VerifyEmailAsync(USER_ID, TOKEN));
        }

        // TC08 - STATUS PENDING → ACTIVE
        [Fact]
        public async Task TC08_StatusPending_ShouldUpdateToActive()
        {
            var user = BuildUser(status: UserStatusConstants.PendingEmailVerification);

            _userManagerMock.Setup(x => x.FindByIdAsync(USER_ID.ToString()))
                .ReturnsAsync(user);

            _userManagerMock.Setup(x => x.ConfirmEmailAsync(user, TOKEN))
                .ReturnsAsync(IdentityResult.Success);

            await CreateService().VerifyEmailAsync(USER_ID, TOKEN);

            Assert.Equal(UserStatusConstants.Active, user.Status);
            _userManagerMock.Verify(x => x.UpdateAsync(user), Times.AtLeastOnce);
        }

        // TC09 - STATUS NOT PENDING (không thay đổi)
        [Fact]
        public async Task TC09_StatusNotPending_ShouldNotChange()
        {
            var user = BuildUser(status: UserStatusConstants.Active);

            _userManagerMock.Setup(x => x.FindByIdAsync(USER_ID.ToString()))
                .ReturnsAsync(user);

            _userManagerMock.Setup(x => x.ConfirmEmailAsync(user, TOKEN))
                .ReturnsAsync(IdentityResult.Success);

            await CreateService().VerifyEmailAsync(USER_ID, TOKEN);

            // Status vẫn là Active, không bị update thêm lần nào
            Assert.Equal(UserStatusConstants.Active, user.Status);
        }

        // TC10 - PENDING EMAIL CLEARED sau ChangeEmail
        [Fact]
        public async Task TC10_PendingEmail_ShouldBeCleared()
        {
            var user = BuildUser(pendingEmail: "new@gmail.com");

            _userManagerMock.Setup(x => x.FindByIdAsync(USER_ID.ToString()))
                .ReturnsAsync(user);

            _userManagerMock.Setup(x => x.ChangeEmailAsync(user, "new@gmail.com", TOKEN))
                .ReturnsAsync(IdentityResult.Success);

            await CreateService().VerifyEmailAsync(USER_ID, TOKEN);

            Assert.Null(user.PendingEmail);
        }

        // TC11 - PENDING → ACTIVE sau ChangeEmail success
        [Fact]
        public async Task TC11_ChangeEmailSuccess_PendingStatus_ShouldUpdateToActive()
        {
            var user = BuildUser(
                pendingEmail: "new@gmail.com",
                status: UserStatusConstants.PendingEmailVerification);

            _userManagerMock.Setup(x => x.FindByIdAsync(USER_ID.ToString()))
                .ReturnsAsync(user);

            _userManagerMock.Setup(x => x.ChangeEmailAsync(user, "new@gmail.com", TOKEN))
                .ReturnsAsync(IdentityResult.Success);

            await CreateService().VerifyEmailAsync(USER_ID, TOKEN);

            Assert.Equal(UserStatusConstants.Active, user.Status);
            Assert.Null(user.PendingEmail);
        }
    }
}
