using System;
using System.Threading.Tasks;
using ChargeSlot.Api.Data;
using ChargeSlot.Api.Enums;
using ChargeSlot.Api.Models;
using ChargeSlot.Api.Models.Identity;
using ChargeSlot.Api.Services.Implementation;
using ChargeSlot.Api.Services.Interfaces;
using ChargeSlot.Tests.Helpers;
using Microsoft.AspNetCore.Identity;
using Moq;
using Xunit;
using Microsoft.Extensions.DependencyInjection;

namespace ChargeSlot.Tests.Services
{
    public class DisputeServiceTests
    {
        private readonly Mock<INotificationService> _notificationService = new Mock<INotificationService>();
        private readonly Mock<UserManager<ApplicationUser>> _userManager;
        private readonly Mock<IFileStorageService> _fileStorageService = new Mock<IFileStorageService>();
        private readonly ChargeSlotDbContext _db = TestDbHelper.CreateInMemoryDb();
        private readonly DisputeService _service;

        public DisputeServiceTests()
        {
            var store = new Mock<IUserStore<ApplicationUser>>();
            _userManager = new Mock<UserManager<ApplicationUser>>(store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
            
            var provider = new Mock<IServiceProvider>();
            var bookingService = new Mock<IBookingService>();
            var configService = new Mock<ISystemConfigService>();
            configService.Setup(c => c.GetCurrentConfigsAsync()).ReturnsAsync(TestDbHelper.GetDefaultConfigs());
            provider.Setup(p => p.GetService(typeof(IBookingService))).Returns(bookingService.Object);
            provider.Setup(p => p.GetService(typeof(ISystemConfigService))).Returns(configService.Object);

            _service = new DisputeService(_notificationService.Object, _db, _userManager.Object, _fileStorageService.Object, provider.Object);
        }

        [Fact]
        public async Task Dispute_PlaceholderTest() { Assert.True(true); }
    }
}
