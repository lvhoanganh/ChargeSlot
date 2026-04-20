using Xunit;
using Moq;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using ChargeSlot.Api.Services.Implementation;
using ChargeSlot.Api.Models.Identity;
using ChargeSlot.Api.DTOs.Auth;
using ChargeSlot.Api.Repositories.Interfaces;
using ChargeSlot.Api.Services.Interfaces;
using ChargeSlot.Api.Models;
using ChargeSlot.Api.Constants;
using Microsoft.Extensions.Logging;

namespace ChargeSlot.Tests.Services.AuthServiceTests
{
    public class RegisterTests
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

        private const string NormalizedPhone = "+84123456789";

        // ===== CONSTRUCTOR =====
        public RegisterTests()
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

            _uowMock.Setup(x => x.CompleteAsync()).ReturnsAsync(1);
            _emailMock.Setup(x => x.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                      .Returns(Task.CompletedTask);

            _roleManagerMock.Setup(x => x.RoleExistsAsync(It.IsAny<string>()))
                            .ReturnsAsync(true);

            _otpRepoMock.Setup(x => x.InvalidateAllOtpsAsync(It.IsAny<string>()))
                        .Returns(Task.CompletedTask);

            _userManagerMock.Setup(x => x.GetRolesAsync(It.IsAny<ApplicationUser>()))
                            .ReturnsAsync(new List<string>());

            _userManagerMock.Setup(x => x.AddToRoleAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
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

        // ===== HELPER DTO =====
        private RegisterDto CreateValidDto(string email = "a@test.com", string role = "Driver")
        {
            return new RegisterDto
            {
                PhoneNumber = "0123456789",
                FirebaseIdToken = "token",
                Email = email,
                Password = "Abc@12345",
                FullName = "Nguyen Van A",
                Role = role
            };
        }

        // ================== TEST CASES ==================

        // TC01
        [Fact]
        public async Task Register_Success_Driver()
        {
            _firebaseMock.Setup(x => x.VerifyTokenAndGetPhoneAsync(It.IsAny<string>()))
                         .ReturnsAsync(NormalizedPhone);

            _userManagerMock.Setup(x => x.FindByNameAsync(It.IsAny<string>()))
                            .ReturnsAsync((ApplicationUser?)null);

            _userManagerMock.Setup(x => x.FindByEmailAsync(It.IsAny<string>()))
                            .ReturnsAsync((ApplicationUser?)null);

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

            _userManagerMock.Setup(x => x.FindByNameAsync(It.IsAny<string>()))
                            .ReturnsAsync((ApplicationUser?)null);

            _userManagerMock.Setup(x => x.FindByEmailAsync(It.IsAny<string>()))
                            .ReturnsAsync((ApplicationUser?)null);

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

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().RegisterAsync(CreateValidDto()));
        }

        // TC05
        [Fact]
        public async Task Register_PhoneExists_ShouldThrow()
        {
            _firebaseMock.Setup(x => x.VerifyTokenAndGetPhoneAsync(It.IsAny<string>()))
                         .ReturnsAsync(NormalizedPhone);

            _userManagerMock.Setup(x => x.FindByNameAsync(It.IsAny<string>()))
                            .ReturnsAsync(new ApplicationUser { Status = UserStatusConstants.Active });

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().RegisterAsync(CreateValidDto()));
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

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().RegisterAsync(CreateValidDto()));
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

            _userManagerMock.Setup(x => x.FindByNameAsync(It.IsAny<string>()))
                            .ReturnsAsync(oldUser);

            _userManagerMock.Setup(x => x.DeleteAsync(oldUser))
                            .ReturnsAsync(IdentityResult.Success);

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

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().RegisterAsync(CreateValidDto("taken@test.com")));
        }

        // TC09
        [Fact]
        public async Task Register_InvalidRole_ShouldThrow()
        {
            _firebaseMock.Setup(x => x.VerifyTokenAndGetPhoneAsync(It.IsAny<string>()))
                         .ReturnsAsync(NormalizedPhone);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().RegisterAsync(CreateValidDto("a@test.com", "Fake")));
        }

        // TC10
        [Fact]
        public async Task Register_CreateFail_ShouldThrow()
        {
            _firebaseMock.Setup(x => x.VerifyTokenAndGetPhoneAsync(It.IsAny<string>()))
                         .ReturnsAsync(NormalizedPhone);

            _userManagerMock.Setup(x => x.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
                .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Error" }));

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().RegisterAsync(CreateValidDto()));
        }
    }
}