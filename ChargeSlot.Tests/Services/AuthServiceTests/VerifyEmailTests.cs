using ChargeSlot.Api.Models.Identity;
using ChargeSlot.Api.Constants;
using Microsoft.AspNetCore.Identity;
using Moq;

namespace ChargeSlot.Tests.Services.AuthServiceTests
{
    public class VerifyEmailTests : AuthServiceTestBase
    {
        private const int USER_ID = 1;
        private const string TOKEN = "test_token";

        private static ApplicationUser BuildUser(
            string? pendingEmail = null,
            bool emailConfirmed = false,
            string status = UserStatusConstants.Active) => new ApplicationUser
            {
                Id = USER_ID,
                FullName = "Test User",
                Email = "old@gmail.com",
                PendingEmail = pendingEmail,
                EmailConfirmed = emailConfirmed,
                Status = status
            };

        public VerifyEmailTests()
        {
            // Default: UpdateAsync luôn thành công
            _userManagerMock.Setup(x => x.UpdateAsync(It.IsAny<ApplicationUser>()))
                .ReturnsAsync(IdentityResult.Success);
            // Default: user không tồn tại
            _userManagerMock.Setup(x => x.FindByIdAsync(USER_ID.ToString()))
                .ReturnsAsync((ApplicationUser?)null);
        }

        // TC01 - USER NOT FOUND
        [Fact]
        public async Task TC01_UserNotFound_ShouldThrow()
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().VerifyEmailAsync(USER_ID, TOKEN));
        }

        // TC02 - CONFIRM EMAIL FAIL (token sai/hết hạn)
        [Fact]
        public async Task TC02_ConfirmEmailFail_ShouldThrow()
        {
            var user = BuildUser();
            _userManagerMock.Setup(x => x.FindByIdAsync(USER_ID.ToString())).ReturnsAsync(user);
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
            _userManagerMock.Setup(x => x.FindByIdAsync(USER_ID.ToString())).ReturnsAsync(user);
            _userManagerMock.Setup(x => x.ChangeEmailAsync(user, "new@gmail.com", TOKEN))
                .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Invalid token" }));

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().VerifyEmailAsync(USER_ID, TOKEN));
        }

        // TC04 - EMAIL ALREADY CONFIRMED
        [Fact]
        public async Task TC04_EmailAlreadyConfirmed_ShouldThrow()
        {
            var user = BuildUser(emailConfirmed: true);
            _userManagerMock.Setup(x => x.FindByIdAsync(USER_ID.ToString())).ReturnsAsync(user);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().VerifyEmailAsync(USER_ID, TOKEN));
        }

        // TC05 - CHANGE EMAIL SUCCESS
        [Fact]
        public async Task TC05_ChangeEmailSuccess_ShouldUpdateUser()
        {
            var user = BuildUser(pendingEmail: "new@gmail.com");
            _userManagerMock.Setup(x => x.FindByIdAsync(USER_ID.ToString())).ReturnsAsync(user);
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
            var user = BuildUser();
            _userManagerMock.Setup(x => x.FindByIdAsync(USER_ID.ToString())).ReturnsAsync(user);
            _userManagerMock.Setup(x => x.ConfirmEmailAsync(user, TOKEN))
                .ReturnsAsync(IdentityResult.Success);

            await CreateService().VerifyEmailAsync(USER_ID, TOKEN);

            _userManagerMock.Verify(x => x.ConfirmEmailAsync(user, TOKEN), Times.Once);
        }

        // TC07 - VERIFY EMAIL FAIL
        [Fact]
        public async Task TC07_VerifyEmailFail_ShouldThrow()
        {
            var user = BuildUser();
            _userManagerMock.Setup(x => x.FindByIdAsync(USER_ID.ToString())).ReturnsAsync(user);
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
            _userManagerMock.Setup(x => x.FindByIdAsync(USER_ID.ToString())).ReturnsAsync(user);
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
            _userManagerMock.Setup(x => x.FindByIdAsync(USER_ID.ToString())).ReturnsAsync(user);
            _userManagerMock.Setup(x => x.ConfirmEmailAsync(user, TOKEN))
                .ReturnsAsync(IdentityResult.Success);

            await CreateService().VerifyEmailAsync(USER_ID, TOKEN);

            Assert.Equal(UserStatusConstants.Active, user.Status);
        }

        // TC10 - PENDING EMAIL CLEARED sau ChangeEmail
        [Fact]
        public async Task TC10_PendingEmail_ShouldBeCleared()
        {
            var user = BuildUser(pendingEmail: "new@gmail.com");
            _userManagerMock.Setup(x => x.FindByIdAsync(USER_ID.ToString())).ReturnsAsync(user);
            _userManagerMock.Setup(x => x.ChangeEmailAsync(user, "new@gmail.com", TOKEN))
                .ReturnsAsync(IdentityResult.Success);

            await CreateService().VerifyEmailAsync(USER_ID, TOKEN);

            Assert.Null(user.PendingEmail);
        }

        // TC11 - PENDING → ACTIVE sau ChangeEmail success
        [Fact]
        public async Task TC11_ChangeEmailSuccess_PendingStatus_ShouldUpdateToActive()
        {
            var user = BuildUser(pendingEmail: "new@gmail.com",
                status: UserStatusConstants.PendingEmailVerification);
            _userManagerMock.Setup(x => x.FindByIdAsync(USER_ID.ToString())).ReturnsAsync(user);
            _userManagerMock.Setup(x => x.ChangeEmailAsync(user, "new@gmail.com", TOKEN))
                .ReturnsAsync(IdentityResult.Success);

            await CreateService().VerifyEmailAsync(USER_ID, TOKEN);

            Assert.Equal(UserStatusConstants.Active, user.Status);
            Assert.Null(user.PendingEmail);
        }
    }
}
