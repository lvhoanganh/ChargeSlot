using ChargeSlot.Api.Models.Identity;
using ChargeSlot.Api.Models;
using ChargeSlot.Api.Constants;
using Microsoft.AspNetCore.Identity;
using Moq;

namespace ChargeSlot.Tests.Services.AuthServiceTests
{
    public class RefreshTokenTests : AuthServiceTestBase
    {
        private const string TOKEN = "test_refresh_token";

        // TC01 - SUCCESS DRIVER
        [Fact]
        public async Task TC01_ValidDriver_ShouldSuccess()
        {
            var user = CreateActiveUser(email: "driver@gmail.com");
            var token = CreateRefreshToken(user, TOKEN);
            _refreshTokenMock.Setup(x => x.GetByTokenAsync(TOKEN)).ReturnsAsync(token);
            _userManagerMock.Setup(x => x.GetRolesAsync(user))
                .ReturnsAsync(new List<string> { RoleConstants.Driver });

            var result = await CreateService().RefreshTokenAsync(TOKEN);

            Assert.NotNull(result);
            Assert.Equal(RoleConstants.Driver, result.Role);
            Assert.False(result.RequiresEmail);
        }

        // TC02 - SUCCESS OWNER
        [Fact]
        public async Task TC02_ValidOwner_ShouldSuccess()
        {
            var user = CreateActiveUser(email: "owner@gmail.com");
            var token = CreateRefreshToken(user, TOKEN);
            _refreshTokenMock.Setup(x => x.GetByTokenAsync(TOKEN)).ReturnsAsync(token);
            _userManagerMock.Setup(x => x.GetRolesAsync(user))
                .ReturnsAsync(new List<string> { RoleConstants.Owner });

            var result = await CreateService().RefreshTokenAsync(TOKEN);

            Assert.NotNull(result);
            Assert.Equal(RoleConstants.Owner, result.Role);
        }

        // TC03 - TOKEN NOT EXIST
        [Fact]
        public async Task TC03_TokenNotExist_ShouldThrow()
        {
            _refreshTokenMock.Setup(x => x.GetByTokenAsync(It.IsAny<string>()))
                .ReturnsAsync((RefreshToken?)null);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().RefreshTokenAsync(TOKEN));
        }

        // TC04 - TOKEN REVOKED
        [Fact]
        public async Task TC04_TokenRevoked_ShouldThrow()
        {
            var user = CreateActiveUser();
            var token = CreateRefreshToken(user, TOKEN, isRevoked: true);
            _refreshTokenMock.Setup(x => x.GetByTokenAsync(TOKEN)).ReturnsAsync(token);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().RefreshTokenAsync(TOKEN));
        }

        // TC05 - TOKEN EXPIRED
        [Fact]
        public async Task TC05_TokenExpired_ShouldThrow()
        {
            var user = CreateActiveUser();
            var token = CreateRefreshToken(user, TOKEN, isExpired: true);
            _refreshTokenMock.Setup(x => x.GetByTokenAsync(TOKEN)).ReturnsAsync(token);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().RefreshTokenAsync(TOKEN));
        }

        // TC06 - USER SUSPENDED
        [Fact]
        public async Task TC06_UserSuspended_ShouldThrow()
        {
            var user = CreateActiveUser(status: UserStatusConstants.Suspended);
            var token = CreateRefreshToken(user, TOKEN);
            _refreshTokenMock.Setup(x => x.GetByTokenAsync(TOKEN)).ReturnsAsync(token);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().RefreshTokenAsync(TOKEN));
        }

        // TC07 - USER BANNED
        [Fact]
        public async Task TC07_UserBanned_ShouldThrow()
        {
            var user = CreateActiveUser(status: UserStatusConstants.Banned);
            var token = CreateRefreshToken(user, TOKEN);
            _refreshTokenMock.Setup(x => x.GetByTokenAsync(TOKEN)).ReturnsAsync(token);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().RefreshTokenAsync(TOKEN));
        }

        // TC08 - EMAIL NOT CONFIRMED
        [Fact]
        public async Task TC08_EmailNotConfirmed_ShouldRequireEmail()
        {
            var user = CreateActiveUser(emailConfirmed: false, email: "user@gmail.com");
            var token = CreateRefreshToken(user, TOKEN);
            _refreshTokenMock.Setup(x => x.GetByTokenAsync(TOKEN)).ReturnsAsync(token);
            _userManagerMock.Setup(x => x.GetRolesAsync(user))
                .ReturnsAsync(new List<string> { RoleConstants.Driver });

            var result = await CreateService().RefreshTokenAsync(TOKEN);

            Assert.NotNull(result);
            Assert.True(result.RequiresEmail);
        }

        // TC09 - EMAIL NULL
        [Fact]
        public async Task TC09_EmailNull_ShouldRequireEmail()
        {
            var user = CreateActiveUser(emailConfirmed: false, email: "");
            user.Email = null;
            var token = CreateRefreshToken(user, TOKEN);
            _refreshTokenMock.Setup(x => x.GetByTokenAsync(TOKEN)).ReturnsAsync(token);
            _userManagerMock.Setup(x => x.GetRolesAsync(user))
                .ReturnsAsync(new List<string> { RoleConstants.Driver });

            var result = await CreateService().RefreshTokenAsync(TOKEN);

            Assert.NotNull(result);
            Assert.True(result.RequiresEmail);
        }

        // TC10 - ADMIN BYPASS EMAIL
        [Fact]
        public async Task TC10_Admin_ShouldNotRequireEmail()
        {
            var user = CreateActiveUser(emailConfirmed: false);
            user.Email = null;
            var token = CreateRefreshToken(user, TOKEN);
            _refreshTokenMock.Setup(x => x.GetByTokenAsync(TOKEN)).ReturnsAsync(token);
            _userManagerMock.Setup(x => x.GetRolesAsync(user))
                .ReturnsAsync(new List<string> { RoleConstants.Admin });

            var result = await CreateService().RefreshTokenAsync(TOKEN);

            Assert.NotNull(result);
            Assert.False(result.RequiresEmail);
        }

        // TC11 - TOKEN ROTATION
        [Fact]
        public async Task TC11_TokenRotation_ShouldUpdateFields()
        {
            var user = CreateActiveUser(email: "test@gmail.com");
            var token = CreateRefreshToken(user, TOKEN);
            _refreshTokenMock.Setup(x => x.GetByTokenAsync(TOKEN)).ReturnsAsync(token);
            _userManagerMock.Setup(x => x.GetRolesAsync(user))
                .ReturnsAsync(new List<string> { RoleConstants.Driver });

            var result = await CreateService().RefreshTokenAsync(TOKEN);

            Assert.NotNull(token.RevokedAt);
            Assert.NotNull(token.ReplacedByToken);
            Assert.NotEmpty(token.ReplacedByToken!);
            Assert.Equal(result.RefreshToken, token.ReplacedByToken);
        }
    }
}
