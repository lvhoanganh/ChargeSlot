using Xunit;
using Moq;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using ChargeSlot.Api.Services.Implementation;
using ChargeSlot.Api.Models.Identity;
using ChargeSlot.Api.DTOs.Auth;
using ChargeSlot.Api.Repositories.Interfaces;
using ChargeSlot.Api.Data;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.Primitives;

namespace ChargeSlot.Tests.Services
{
    public class AuthServiceTests
    {
        private readonly Mock<UserManager<ApplicationUser>> _userManagerMock;
        private readonly Mock<SignInManager<ApplicationUser>> _signInManagerMock;
        private readonly Mock<RoleManager<IdentityRole<int>>> _roleManagerMock;
        private readonly Mock<IConfiguration> _configMock;
        private readonly Mock<IUserOtpRepository> _otpRepoMock;
        private readonly ChargeSlotDbContext _context;

        public AuthServiceTests()
        {
            // UserManager mock
            var userStore = new Mock<IUserStore<ApplicationUser>>();
            _userManagerMock = new Mock<UserManager<ApplicationUser>>(
                userStore.Object, null, null, null, null, null, null, null, null
            );

            // SignInManager mock
            var contextAccessor = new Mock<Microsoft.AspNetCore.Http.IHttpContextAccessor>();
            var claimsFactory = new Mock<IUserClaimsPrincipalFactory<ApplicationUser>>();
            _signInManagerMock = new Mock<SignInManager<ApplicationUser>>(
                _userManagerMock.Object,
                contextAccessor.Object,
                claimsFactory.Object,
                null, null, null, null
            );

            // RoleManager mock
            var roleStore = new Mock<IRoleStore<IdentityRole<int>>>();
            _roleManagerMock = new Mock<RoleManager<IdentityRole<int>>>(
                roleStore.Object, null, null, null, null
            );

            // Config mock (fake JWT config)
            var inMemorySettings = new Dictionary<string, string>
            {
                {"Jwt:Key", "THIS_IS_A_SUPER_SECRET_KEY_123456"},
                {"Jwt:Issuer", "test"},
                {"Jwt:Audience", "test"},
                {"Jwt:ExpiresMinutes", "60"},
                {"Jwt:RefreshTokenExpiresInDays", "7"}
            };

            _configMock = new Mock<IConfiguration>();
            _configMock.Setup(x => x.GetSection("Jwt").GetChildren())
                .Returns(inMemorySettings.Select(kv => new ConfigurationSectionStub(kv.Key, kv.Value)));

            _configMock.Setup(x => x["Jwt:Key"]).Returns("THIS_IS_A_SUPER_SECRET_KEY_123456");
            _configMock.Setup(x => x["Jwt:Issuer"]).Returns("test");
            _configMock.Setup(x => x["Jwt:Audience"]).Returns("test");
            _configMock.Setup(x => x["Jwt:ExpiresMinutes"]).Returns("60");
            _configMock.Setup(x => x["Jwt:RefreshTokenExpiresInDays"]).Returns("7");

            _otpRepoMock = new Mock<IUserOtpRepository>();

            // 🔥 QUAN TRỌNG (fix lỗi của mày)
            _configMock.Setup(x => x.GetSection("Jwt")["Key"])
                .Returns("THIS_IS_A_SUPER_SECRET_KEY_123456");

            // InMemory DB
            var options = new DbContextOptionsBuilder<ChargeSlotDbContext>()
                .UseInMemoryDatabase(databaseName: "TestDb")
                .Options;

            _context = new ChargeSlotDbContext(options);
        }

        private AuthService CreateService()
        {
            return new AuthService(
                _userManagerMock.Object,
                _roleManagerMock.Object,
                _signInManagerMock.Object,
                _configMock.Object,
                _otpRepoMock.Object,
                _context
            );
        }

        // ❌ User không tồn tại
        [Fact]
        public async Task LoginAsync_UserNotFound_ThrowsException()
        {
            _userManagerMock
                .Setup(x => x.FindByNameAsync(It.IsAny<string>()))
                .ReturnsAsync((ApplicationUser)null);

            var service = CreateService();

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.LoginAsync(new LoginDto
                {
                    PhoneNumber = "0123",
                    Password = "@123456"
                })
            );
        }

        // ❌ User inactive
        [Fact]
        public async Task LoginAsync_UserInactive_ThrowsException()
        {
            var user = new ApplicationUser { Status = "INACTIVE" };

            _userManagerMock
                .Setup(x => x.FindByNameAsync(It.IsAny<string>()))
                .ReturnsAsync(user);

            var service = CreateService();

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.LoginAsync(new LoginDto
                {
                    PhoneNumber = "0123",
                    Password = "123"
                })
            );
        }

        // ❌ Sai password
        [Fact]
        public async Task LoginAsync_WrongPassword_ThrowsException()
        {
            var user = new ApplicationUser { Status = "ACTIVE" };

            _userManagerMock
                .Setup(x => x.FindByNameAsync(It.IsAny<string>()))
                .ReturnsAsync(user);

            _signInManagerMock
                .Setup(x => x.CheckPasswordSignInAsync(user, It.IsAny<string>(), true))
                .ReturnsAsync(SignInResult.Failed);

            var service = CreateService();

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.LoginAsync(new LoginDto
                {
                    PhoneNumber = "0899839102",
                    Password = "@123456"
                })
            );
        }

        // ✅ Thành công
        [Fact]
        public async Task LoginAsync_Success_ReturnsToken()
        {
            var user = new ApplicationUser
            {
                Id = 1,
                Status = "ACTIVE",
                UserName = "0899839102",
                PhoneNumber = "Admin123!"
            };

            _userManagerMock
                .Setup(x => x.FindByNameAsync(It.IsAny<string>()))
                .ReturnsAsync(user);

            _signInManagerMock
                .Setup(x => x.CheckPasswordSignInAsync(user, It.IsAny<string>(), true))
                .ReturnsAsync(SignInResult.Success);

            _userManagerMock
                .Setup(x => x.GetRolesAsync(user))
                .ReturnsAsync(new List<string> { "Driver" });

            var service = CreateService();

            var result = await service.LoginAsync(new LoginDto
            {
                PhoneNumber = "0899839102",
                Password = "Admin123!"
            });

            Assert.NotNull(result);
            Assert.NotNull(result.AccessToken);
        }
    }

    // helper fake config section
    public class ConfigurationSectionStub : IConfigurationSection
    {
        public ConfigurationSectionStub(string key, string value)
        {
            Key = key;
            Value = value;
        }

        public string this[string key] { get => Value; set { } }
        public string Key { get; }
        public string Path => Key;
        public string Value { get; set; }
        public IEnumerable<IConfigurationSection> GetChildren() => new List<IConfigurationSection>();
        public IChangeToken GetReloadToken() => null;
        public IConfigurationSection GetSection(string key) => this;
    }
}