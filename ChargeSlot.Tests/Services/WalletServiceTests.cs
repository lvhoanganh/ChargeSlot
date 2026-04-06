using System;
using System.Threading.Tasks;
using ChargeSlot.Api.Data;
using ChargeSlot.Api.DTOs.Wallet;
using ChargeSlot.Api.Enums;
using ChargeSlot.Api.Models;
using ChargeSlot.Api.Models.Identity;
using ChargeSlot.Api.Repositories.Interfaces;
using ChargeSlot.Api.Services.Implementation;
using ChargeSlot.Api.Services.Interfaces;
using ChargeSlot.Tests.Helpers;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace ChargeSlot.Tests.Services
{
    public class WalletServiceTests
    {
        private readonly Mock<IWalletRepository> _walletRepo = new Mock<IWalletRepository>();
        private readonly Mock<IBookingRepository> _bookingRepo = new Mock<IBookingRepository>();
        private readonly Mock<IPaymentRepository> _paymentRepo = new Mock<IPaymentRepository>();
        private readonly Mock<IChargingSlotRepository> _slotRepo = new Mock<IChargingSlotRepository>();
        private readonly Mock<INotificationService> _notificationService = new Mock<INotificationService>();
        private readonly Mock<IFileStorageService> _fileStorageService = new Mock<IFileStorageService>();
        private readonly Mock<UserManager<ApplicationUser>> _userManager;
        private readonly Mock<IConfiguration> _configuration = new Mock<IConfiguration>();
        private readonly Mock<ISystemConfigService> _configService = new Mock<ISystemConfigService>();
        private readonly ChargeSlotDbContext _db = TestDbHelper.CreateInMemoryDb();
        private readonly WalletService _service;

        public WalletServiceTests()
        {
            var store = new Mock<IUserStore<ApplicationUser>>();
            _userManager = new Mock<UserManager<ApplicationUser>>(store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
            _configService.Setup(c => c.GetCurrentConfigsAsync()).ReturnsAsync(TestDbHelper.GetDefaultConfigs());
            _service = new WalletService(_walletRepo.Object, _bookingRepo.Object, _paymentRepo.Object, _slotRepo.Object, _notificationService.Object, _fileStorageService.Object, _db, _userManager.Object, _configuration.Object, _configService.Object);
        }

        [Fact]
        public async Task Wallet_PlaceholderTest() { Assert.True(true); }
    }
}
