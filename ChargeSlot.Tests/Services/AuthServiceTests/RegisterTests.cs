using ChargeSlot.Api.DTOs.Auth;
using ChargeSlot.Api.Models.Identity;
using ChargeSlot.Api.Models;
using ChargeSlot.Api.Constants;
using Microsoft.AspNetCore.Identity;
using Moq;

namespace ChargeSlot.Tests.Services.AuthServiceTests
{
    public class RegisterTests : AuthServiceTestBase
    {
        // PhoneNumberHelper.NormalizeAndValidate("0123456789")
        //   → trả "0123456789" (giữ nguyên 0-prefixed, bỏ +84 thành 0)
        // Firebase mock phải trả đúng "0123456789" để match
        private const string NormalizedPhone = "0123456789";

        private RegisterDto CreateValidDto(string email = "a@test.com", string role = "Driver") =>
            new RegisterDto
            {
                PhoneNumber = "0123456789",
                FirebaseIdToken = "token",
                Email = email,
                Password = "Abc@12345",
                FullName = "Nguyen Van A",
                Role = role
            };

        // TC01
        [Fact]
        public async Task Register_Success_Driver()
        {
            _firebaseMock.Setup(x => x.VerifyTokenAndGetPhoneAsync(It.IsAny<string>()))
                         .ReturnsAsync(NormalizedPhone);
            _userManagerMock.Setup(x => x.FindByNameAsync(It.IsAny<string>())).ReturnsAsync((ApplicationUser?)null);
            _userManagerMock.Setup(x => x.FindByEmailAsync(It.IsAny<string>())).ReturnsAsync((ApplicationUser?)null);
            _userManagerMock.Setup(x => x.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
                            .ReturnsAsync(IdentityResult.Success);

            await CreateService().RegisterAsync(CreateValidDto("driver@test.com", "Driver"));

            _driverRepoMock.Verify(x => x.AddAsync(It.IsAny<Driver>()), Times.Once);
        }

        // TC02
        [Fact]
        public async Task Register_Success_Owner()
        {
            _firebaseMock.Setup(x => x.VerifyTokenAndGetPhoneAsync(It.IsAny<string>()))
                         .ReturnsAsync(NormalizedPhone);
            _userManagerMock.Setup(x => x.FindByNameAsync(It.IsAny<string>())).ReturnsAsync((ApplicationUser?)null);
            _userManagerMock.Setup(x => x.FindByEmailAsync(It.IsAny<string>())).ReturnsAsync((ApplicationUser?)null);
            _userManagerMock.Setup(x => x.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
                            .ReturnsAsync(IdentityResult.Success);

            await CreateService().RegisterAsync(CreateValidDto("owner@test.com", "Owner"));

            _ownerRepoMock.Verify(x => x.AddAsync(It.IsAny<Owner>()), Times.Once);
        }

        // TC03
        [Fact]
        public async Task Register_DefaultRole_ShouldBeDriver()
        {
            _firebaseMock.Setup(x => x.VerifyTokenAndGetPhoneAsync(It.IsAny<string>()))
                         .ReturnsAsync(NormalizedPhone);
            string? role = null;
            _userManagerMock.Setup(x => x.AddToRoleAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
                .Callback<ApplicationUser, string>((_, r) => role = r)
                .ReturnsAsync(IdentityResult.Success);

            await CreateService().RegisterAsync(CreateValidDto("default@test.com", ""));

            Assert.Equal("Driver", role);
        }

        // TC04
        [Fact]
        public async Task Register_FirebaseMismatch_ShouldThrow()
        {
            _firebaseMock.Setup(x => x.VerifyTokenAndGetPhoneAsync(It.IsAny<string>()))
                         .ReturnsAsync("+84999999999");

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().RegisterAsync(CreateValidDto()));
            Assert.Contains("không khớp", ex.Message);
        }

        // TC05
        [Fact]
        public async Task Register_PhoneExists_ShouldThrow()
        {
            _firebaseMock.Setup(x => x.VerifyTokenAndGetPhoneAsync(It.IsAny<string>()))
                         .ReturnsAsync(NormalizedPhone);
            _userManagerMock.Setup(x => x.FindByNameAsync(It.IsAny<string>()))
                            .ReturnsAsync(new ApplicationUser { Status = UserStatusConstants.Active });

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().RegisterAsync(CreateValidDto()));
            Assert.Contains("already registered", ex.Message);
        }

        // TC06
        [Fact]
        public async Task Register_PhonePendingRecent_ShouldThrow()
        {
            _firebaseMock.Setup(x => x.VerifyTokenAndGetPhoneAsync(It.IsAny<string>()))
                         .ReturnsAsync(NormalizedPhone);
            _userManagerMock.Setup(x => x.FindByNameAsync(It.IsAny<string>()))
                            .ReturnsAsync(new ApplicationUser
                            {
                                Status = UserStatusConstants.PendingEmailVerification,
                                CreatedAt = DateTime.Now
                            });

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().RegisterAsync(CreateValidDto()));
            Assert.Contains("already registered", ex.Message);
        }

        // TC07
        [Fact]
        public async Task Register_PhonePendingExpired_ShouldDeleteThenCreate()
        {
            _firebaseMock.Setup(x => x.VerifyTokenAndGetPhoneAsync(It.IsAny<string>()))
                         .ReturnsAsync(NormalizedPhone);
            var oldUser = new ApplicationUser
            {
                Status = UserStatusConstants.PendingEmailVerification,
                CreatedAt = DateTime.Now.AddDays(-2)
            };
            _userManagerMock.Setup(x => x.FindByNameAsync(It.IsAny<string>())).ReturnsAsync(oldUser);
            _userManagerMock.Setup(x => x.DeleteAsync(oldUser)).ReturnsAsync(IdentityResult.Success);
            _userManagerMock.Setup(x => x.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
                            .ReturnsAsync(IdentityResult.Success);

            await CreateService().RegisterAsync(CreateValidDto("new@test.com"));

            _userManagerMock.Verify(x => x.DeleteAsync(oldUser), Times.Once);
        }

        // TC08
        [Fact]
        public async Task Register_EmailExists_ShouldThrow()
        {
            _firebaseMock.Setup(x => x.VerifyTokenAndGetPhoneAsync(It.IsAny<string>()))
                         .ReturnsAsync(NormalizedPhone);
            _userManagerMock.Setup(x => x.FindByEmailAsync(It.IsAny<string>()))
                            .ReturnsAsync(new ApplicationUser());

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().RegisterAsync(CreateValidDto("taken@test.com")));
            Assert.Contains("Email đã được sử dụng", ex.Message);
        }

        // TC09
        [Fact]
        public async Task Register_InvalidRole_ShouldThrow()
        {
            _firebaseMock.Setup(x => x.VerifyTokenAndGetPhoneAsync(It.IsAny<string>()))
                         .ReturnsAsync(NormalizedPhone);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().RegisterAsync(CreateValidDto("a@test.com", "Fake")));
            Assert.Contains("Invalid role", ex.Message);
        }

        // TC10
        [Fact]
        public async Task Register_CreateFail_ShouldThrow()
        {
            _firebaseMock.Setup(x => x.VerifyTokenAndGetPhoneAsync(It.IsAny<string>()))
                         .ReturnsAsync(NormalizedPhone);
            _userManagerMock.Setup(x => x.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
                .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Error" }));

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().RegisterAsync(CreateValidDto()));
            Assert.Contains("Error", ex.Message);
        }
    }
}