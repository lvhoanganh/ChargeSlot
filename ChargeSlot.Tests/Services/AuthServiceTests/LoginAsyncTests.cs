using ChargeSlot.Api.DTOs.Auth;
using ChargeSlot.Api.Models.Identity;
using ChargeSlot.Api.Constants;
using Microsoft.AspNetCore.Identity;
using Moq;

namespace ChargeSlot.Tests.Services.AuthServiceTests
{
    public class LoginTests : AuthServiceTestBase
    {
        private static LoginDto CreateDto(string password = "Abc@12345") => new LoginDto
        {
            PhoneNumber = "0123456789",
            Password = password
        };

        // TC01: Đăng nhập thành công với role Driver
        [Fact]
        public async Task Login_Success_Driver()
        {
            var user = CreateActiveUser();
            _userManagerMock.Setup(x => x.FindByNameAsync(It.IsAny<string>())).ReturnsAsync(user);
            _signInManagerMock.Setup(x => x.CheckPasswordSignInAsync(user, It.IsAny<string>(), true))
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
            var user = CreateActiveUser();
            _userManagerMock.Setup(x => x.FindByNameAsync(It.IsAny<string>())).ReturnsAsync(user);
            _signInManagerMock.Setup(x => x.CheckPasswordSignInAsync(user, It.IsAny<string>(), true))
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
            _userManagerMock.Setup(x => x.FindByNameAsync(It.IsAny<string>()))
                .ReturnsAsync((ApplicationUser?)null);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().LoginAsync(CreateDto()));
        }

        // TC04: Tài khoản đang chờ xác thực email → throw
        [Fact]
        public async Task Login_PendingEmail_ShouldThrow()
        {
            var user = CreateActiveUser(status: UserStatusConstants.PendingEmailVerification);
            _userManagerMock.Setup(x => x.FindByNameAsync(It.IsAny<string>())).ReturnsAsync(user);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().LoginAsync(CreateDto()));
        }

        // TC05: Tài khoản bị banned → throw
        [Fact]
        public async Task Login_Banned_ShouldThrow()
        {
            var user = CreateActiveUser(status: UserStatusConstants.Banned);
            _userManagerMock.Setup(x => x.FindByNameAsync(It.IsAny<string>())).ReturnsAsync(user);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().LoginAsync(CreateDto()));
        }

        // TC06: Sai mật khẩu → throw
        [Fact]
        public async Task Login_WrongPassword_ShouldThrow()
        {
            var user = CreateActiveUser();
            _userManagerMock.Setup(x => x.FindByNameAsync(It.IsAny<string>())).ReturnsAsync(user);
            _signInManagerMock.Setup(x => x.CheckPasswordSignInAsync(user, It.IsAny<string>(), true))
                .ReturnsAsync(SignInResult.Failed);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().LoginAsync(CreateDto("WrongPass")));
        }

        // TC07: Email chưa xác thực (non-Admin) → RequiresEmail = true
        [Fact]
        public async Task Login_EmailNotConfirmed_Driver_ShouldReturnRequiresEmail()
        {
            var user = CreateActiveUser(emailConfirmed: false);
            _userManagerMock.Setup(x => x.FindByNameAsync(It.IsAny<string>())).ReturnsAsync(user);
            _signInManagerMock.Setup(x => x.CheckPasswordSignInAsync(user, It.IsAny<string>(), true))
                .ReturnsAsync(SignInResult.Success);
            _userManagerMock.Setup(x => x.GetRolesAsync(user))
                .ReturnsAsync(new List<string> { RoleConstants.Driver });

            var result = await CreateService().LoginAsync(CreateDto());

            Assert.NotNull(result);
            Assert.True(result.RequiresEmail);
        }

        // TC08: Admin chưa confirm email → RequiresEmail = false
        [Fact]
        public async Task Login_Admin_EmailNotConfirmed_ShouldNotRequireEmail()
        {
            var user = CreateActiveUser(emailConfirmed: false);
            _userManagerMock.Setup(x => x.FindByNameAsync(It.IsAny<string>())).ReturnsAsync(user);
            _signInManagerMock.Setup(x => x.CheckPasswordSignInAsync(user, It.IsAny<string>(), true))
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
            var user = CreateActiveUser(emailConfirmed: true);
            _userManagerMock.Setup(x => x.FindByNameAsync(It.IsAny<string>())).ReturnsAsync(user);
            _signInManagerMock.Setup(x => x.CheckPasswordSignInAsync(user, It.IsAny<string>(), true))
                .ReturnsAsync(SignInResult.Success);
            _userManagerMock.Setup(x => x.GetRolesAsync(user))
                .ReturnsAsync(new List<string> { RoleConstants.Driver });

            var result = await CreateService().LoginAsync(CreateDto());

            Assert.NotNull(result);
            Assert.False(result.RequiresEmail);
        }
    }
}
