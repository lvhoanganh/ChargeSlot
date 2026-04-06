using System;
using System.Threading.Tasks;
using ChargeSlot.Api.Data;
using ChargeSlot.Api.Enums;
using ChargeSlot.Api.Models;
using ChargeSlot.Api.Repositories.Interfaces;
using ChargeSlot.Api.Services.Implementation;
using ChargeSlot.Api.Services.Interfaces;
using ChargeSlot.Tests.Helpers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace ChargeSlot.Tests.Services
{
    public class PaymentServiceTests
    {
        private readonly Mock<IBookingRepository> _bookingRepo = new Mock<IBookingRepository>();
        private readonly Mock<IPaymentRepository> _paymentRepo = new Mock<IPaymentRepository>();
        private readonly Mock<IChargingSlotRepository> _slotRepo = new Mock<IChargingSlotRepository>();
        private readonly Mock<INotificationService> _notificationService = new Mock<INotificationService>();
        private readonly Mock<IWalletRepository> _walletRepo = new Mock<IWalletRepository>();
        private readonly Mock<ILogger<PaymentService>> _logger = new Mock<ILogger<PaymentService>>();
        private readonly Mock<IConfiguration> _configuration = new Mock<IConfiguration>();
        private readonly Mock<ISystemConfigService> _configService = new Mock<ISystemConfigService>();
        private readonly ChargeSlotDbContext _db = TestDbHelper.CreateInMemoryDb();
        private readonly PaymentService _service;

        public PaymentServiceTests()
        {
            _configService.Setup(c => c.GetCurrentConfigsAsync()).ReturnsAsync(TestDbHelper.GetDefaultConfigs());
            _service = new PaymentService(_bookingRepo.Object, _paymentRepo.Object, _slotRepo.Object, _notificationService.Object, _walletRepo.Object, _db, _logger.Object, _configuration.Object, _configService.Object);
        }

        [Fact]
        public async Task Payment_PlaceholderTest() { Assert.True(true); }
    }
}
