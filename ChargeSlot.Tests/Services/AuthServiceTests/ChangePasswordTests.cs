using ChargeSlot.Api.Models.Identity;
using ChargeSlot.Api.Constants;
using Microsoft.AspNetCore.Identity;
using Moq;

namespace ChargeSlot.Tests.Services.AuthServiceTests
{
    public class ChangePasswordTests : AuthServiceTestBase
    {
        private const int USER_ID = 1;
        private const string OLD_PASS = "OldPass@123";
        private const string NEW_PASS = "NewPass@123";

        private static ApplicationUser BuildUser() => new ApplicationUser
        {
            Id = USER_ID,
            FullName = "Test User"
        };

        // TC01 - User not found → throw
        [Fact]
        public async Task TC01_UserNotFound_ShouldThrow()
        {
            _userManagerMock.Setup(x => x.FindByIdAsync(USER_ID.ToString()))
                .ReturnsAsync((ApplicationUser?)null);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().ChangePasswordAsync(USER_ID, OLD_PASS, NEW_PASS));
        }

        // TC02 - Success
        [Fact]
        public async Task TC02_Success_ShouldPass()
        {
            var user = BuildUser();
            _userManagerMock.Setup(x => x.FindByIdAsync(USER_ID.ToString())).ReturnsAsync(user);
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
            _userManagerMock.Setup(x => x.FindByIdAsync(USER_ID.ToString())).ReturnsAsync(user);
            _userManagerMock.Setup(x => x.ChangePasswordAsync(user, "WrongPass", NEW_PASS))
                .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Incorrect password." }));

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().ChangePasswordAsync(USER_ID, "WrongPass", NEW_PASS));

            Assert.Contains("Incorrect password.", ex.Message);
        }

        // TC04 - Mật khẩu mới không đủ mạnh → throw
        [Fact]
        public async Task TC04_NewPasswordTooWeak_ShouldThrow()
        {
            var user = BuildUser();
            _userManagerMock.Setup(x => x.FindByIdAsync(USER_ID.ToString())).ReturnsAsync(user);
            _userManagerMock.Setup(x => x.ChangePasswordAsync(user, OLD_PASS, "123"))
                .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Password too weak" }));

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().ChangePasswordAsync(USER_ID, OLD_PASS, "123"));

            Assert.Contains("Password too weak", ex.Message);
        }

        // TC05 - Nhiều lỗi cùng lúc → message gộp đủ các lỗi
        [Fact]
        public async Task TC05_MultipleErrors_ShouldThrowCombinedMessage()
        {
            var user = BuildUser();
            _userManagerMock.Setup(x => x.FindByIdAsync(USER_ID.ToString())).ReturnsAsync(user);
            _userManagerMock.Setup(x => x.ChangePasswordAsync(user, OLD_PASS, "123"))
                .ReturnsAsync(IdentityResult.Failed(
                    new IdentityError { Description = "Too short" },
                    new IdentityError { Description = "Need digit" }));

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().ChangePasswordAsync(USER_ID, OLD_PASS, "123"));

            Assert.Contains("Too short", ex.Message);
            Assert.Contains("Need digit", ex.Message);
        }

        // TC06 - currentPassword null → Identity xử lý và trả lỗi → throw
        [Fact]
        public async Task TC06_CurrentPasswordNull_ShouldThrow()
        {
            var user = BuildUser();
            _userManagerMock.Setup(x => x.FindByIdAsync(USER_ID.ToString())).ReturnsAsync(user);
            _userManagerMock.Setup(x => x.ChangePasswordAsync(user, null!, NEW_PASS))
                .ReturnsAsync(IdentityResult.Failed(
                    new IdentityError { Description = "Current password cannot be null" }));

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().ChangePasswordAsync(USER_ID, null!, NEW_PASS));
        }

        // TC07 - newPassword null → throw
        [Fact]
        public async Task TC07_NewPasswordNull_ShouldThrow()
        {
            var user = BuildUser();
            _userManagerMock.Setup(x => x.FindByIdAsync(USER_ID.ToString())).ReturnsAsync(user);
            _userManagerMock.Setup(x => x.ChangePasswordAsync(user, OLD_PASS, null!))
                .ReturnsAsync(IdentityResult.Failed(
                    new IdentityError { Description = "New password cannot be null" }));

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().ChangePasswordAsync(USER_ID, OLD_PASS, null!));
        }

        // TC08 - newPassword == currentPassword → policy cho pass
        [Fact]
        public async Task TC08_NewEqualsCurrent_PolicyAllows_ShouldPass()
        {
            var user = BuildUser();
            _userManagerMock.Setup(x => x.FindByIdAsync(USER_ID.ToString())).ReturnsAsync(user);
            _userManagerMock.Setup(x => x.ChangePasswordAsync(user, OLD_PASS, OLD_PASS))
                .ReturnsAsync(IdentityResult.Success);

            await CreateService().ChangePasswordAsync(USER_ID, OLD_PASS, OLD_PASS);

            _userManagerMock.Verify(x => x.ChangePasswordAsync(user, OLD_PASS, OLD_PASS), Times.Once);
        }

        // TC09 - IdentityReturnsLockedError → throw
        [Fact]
        public async Task TC09_IdentityReturnsLockedError_ShouldThrow()
        {
            var user = BuildUser();
            _userManagerMock.Setup(x => x.FindByIdAsync(USER_ID.ToString())).ReturnsAsync(user);
            _userManagerMock.Setup(x => x.ChangePasswordAsync(user, OLD_PASS, NEW_PASS))
                .ReturnsAsync(IdentityResult.Failed(
                    new IdentityError { Description = "User is locked out" }));

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().ChangePasswordAsync(USER_ID, OLD_PASS, NEW_PASS));

            Assert.Contains("User is locked out", ex.Message);
        }
    }
}
