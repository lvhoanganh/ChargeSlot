using System;
using System.Threading.Tasks;
using ChargeSlot.Api.Data;
using ChargeSlot.Api.Enums;
using ChargeSlot.Api.Models;
using ChargeSlot.Api.Repositories.Interfaces;
using ChargeSlot.Api.Services.Implementation;
using ChargeSlot.Api.Services.Interfaces;
using ChargeSlot.Tests.Helpers;
using Moq;
using Xunit;

namespace ChargeSlot.Tests.Services
{
    public class ChargingSessionServiceTests
    {
        private readonly Mock<IChargingSessionRepository> _sessionRepo = new Mock<IChargingSessionRepository>();
        private readonly Mock<IInvoiceRepository> _invoiceRepo = new Mock<IInvoiceRepository>();
        private readonly Mock<IBookingRepository> _bookingRepo = new Mock<IBookingRepository>();
        private readonly Mock<IChargingSlotRepository> _slotRepo = new Mock<IChargingSlotRepository>();
        private readonly Mock<IWalletRepository> _walletRepo = new Mock<IWalletRepository>();
        private readonly Mock<INotificationService> _notificationService = new Mock<INotificationService>();
        private readonly Mock<ISystemConfigService> _configService = new Mock<ISystemConfigService>();
        private readonly ChargeSlotDbContext _db = TestDbHelper.CreateInMemoryDb();
        private readonly ChargingSessionService _service;

        public ChargingSessionServiceTests()
        {
            _configService.Setup(c => c.GetCurrentConfigsAsync()).ReturnsAsync(TestDbHelper.GetDefaultConfigs());
            _service = new ChargingSessionService(_sessionRepo.Object, _invoiceRepo.Object, _bookingRepo.Object, _slotRepo.Object, _walletRepo.Object, _notificationService.Object, _db, _configService.Object);
        }

        [Fact]
        public async Task Session_PlaceholderTest() { Assert.True(true); }
    }
}
