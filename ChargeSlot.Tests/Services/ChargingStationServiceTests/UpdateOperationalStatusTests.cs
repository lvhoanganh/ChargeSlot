using Moq;
using ChargeSlot.Api.Models;
using ChargeSlot.Api.Enums;
using ChargeSlot.Api.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace ChargeSlot.Tests.Services.ChargingStationServiceTests
{
    public class UpdateOperationalStatusTests : ChargingStationServiceTestBase
    {
        private void SetupApprovedStation(ChargingStation station)
        {
            _stationRepoMock.Setup(x => x.GetByIdAsync(station.Id, true, true)).ReturnsAsync(station);
        }

        private ChargingStation CreateApprovedStation(int id = 1, int ownerUserId = 1)
        {
            var s = CreateStation(id, ownerUserId, ApprovalStatus.Approved, OperationalStatus.Active);
            return s;
        }

        // TC01: Station không tồn tại → throw KeyNotFoundException
        [Fact]
        public async Task UpdateStatus_StationNotFound_Throws()
        {
            _stationRepoMock.Setup(x => x.GetByIdAsync(99, true, true)).ReturnsAsync((ChargingStation?)null);

            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                CreateService().UpdateOperationalStatusAsync(99, 1, "Inactive"));
        }

        // TC02: Không phải owner → throw UnauthorizedAccessException
        [Fact]
        public async Task UpdateStatus_NotOwner_Throws()
        {
            var station = CreateApprovedStation(ownerUserId: 99);
            SetupApprovedStation(station);

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                CreateService().UpdateOperationalStatusAsync(1, 1, "Inactive"));
        }

        // TC03: Station chưa được Approved → throw
        [Fact]
        public async Task UpdateStatus_NotApproved_Throws()
        {
            var station = CreateStation(id: 1, ownerUserId: 1, approval: ApprovalStatus.Draft);
            _stationRepoMock.Setup(x => x.GetByIdAsync(1, true, true)).ReturnsAsync(station);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().UpdateOperationalStatusAsync(1, 1, "Inactive"));

            Assert.Contains("Approved", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        // TC04: Status string không hợp lệ → throw InvalidOperationException
        [Fact]
        public async Task UpdateStatus_InvalidStatusString_Throws()
        {
            var station = CreateApprovedStation();
            SetupApprovedStation(station);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().UpdateOperationalStatusAsync(1, 1, "INVALID_STATUS"));

            Assert.Contains("OperationalStatus", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        // TC05: Chuyển sang Inactive, không có booking nào → đổi status bình thường
        [Fact]
        public async Task UpdateStatus_ToInactive_NoBookings_UpdatesStatus()
        {
            var station = CreateApprovedStation();
            SetupApprovedStation(station);
            _bookingRepoMock.Setup(x => x.GetActiveBookingsByStationIdsAsync(
                It.IsAny<List<int>>(), It.IsAny<BookingStatus[]>()))
                .ReturnsAsync(new List<Booking>());

            await CreateService().UpdateOperationalStatusAsync(1, 1, "Inactive");

            Assert.Equal(OperationalStatus.Inactive, station.OperationalStatus);
        }

        // TC06: Chuyển sang Inactive, có booking CheckedIn đang chạy → throw không thể tắt
        [Fact]
        public async Task UpdateStatus_ToInactive_HasCheckedInBooking_Throws()
        {
            var station = CreateApprovedStation();
            SetupApprovedStation(station);
            _bookingRepoMock.Setup(x => x.GetActiveBookingsByStationIdsAsync(
                It.IsAny<List<int>>(), It.IsAny<BookingStatus[]>()))
                .ReturnsAsync(new List<Booking>
                {
                    new Booking
                    {
                        Id = 1,
                        Status = BookingStatus.CheckedIn,
                        EndTime = DateTime.Now.AddHours(2) // future
                    }
                });

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().UpdateOperationalStatusAsync(1, 1, "Inactive"));

            Assert.Contains("CheckedIn", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        // TC07: Chuyển sang Inactive, có Paid booking, lần 1 trong tháng → hủy bookings + cảnh báo (không ban)
        [Fact]
        public async Task UpdateStatus_ToInactive_FirstEmergencyThisMonth_WarnsButNoBan()
        {
            var station = CreateApprovedStation();
            station.LastEmergencyCancelAt = DateTime.Now.AddMonths(-2); // lần trước là tháng trước
            SetupApprovedStation(station);

            var futureBooking = new Booking
            {
                Id = 1,
                Status = BookingStatus.Paid,
                EndTime = DateTime.Now.AddDays(1)
            };

            _bookingRepoMock.Setup(x => x.GetActiveBookingsByStationIdsAsync(
                It.IsAny<List<int>>(), It.IsAny<BookingStatus[]>()))
                .ReturnsAsync(new List<Booking> { futureBooking });

            // Setup IServiceProvider để resolve IBookingService
            var bookingServiceMock = new Mock<IBookingService>();
            bookingServiceMock.Setup(x => x.CancelSystemBookingAsync(It.IsAny<int>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            var scopeMock = new Mock<IServiceScope>();
            var scopeFactoryMock = new Mock<IServiceScopeFactory>();
            var scopedProviderMock = new Mock<IServiceProvider>();

            scopedProviderMock.Setup(x => x.GetService(typeof(IBookingService))).Returns(bookingServiceMock.Object);
            scopeMock.Setup(x => x.ServiceProvider).Returns(scopedProviderMock.Object);
            scopeFactoryMock.Setup(x => x.CreateScope()).Returns(scopeMock.Object);
            _serviceProviderMock.Setup(x => x.GetService(typeof(IServiceScopeFactory))).Returns(scopeFactoryMock.Object);

            await CreateService().UpdateOperationalStatusAsync(1, 1, "Inactive");

            Assert.Null(station.BannedUntil); // không bị ban
            Assert.Equal(OperationalStatus.Inactive, station.OperationalStatus);
            bookingServiceMock.Verify(x => x.CancelSystemBookingAsync(1, It.IsAny<string>()), Times.Once);
        }

        // TC08: Chuyển sang Inactive, lần 2 trong tháng → ban 30 ngày
        [Fact]
        public async Task UpdateStatus_ToInactive_SecondEmergencyThisMonth_BansStation()
        {
            var station = CreateApprovedStation();
            station.LastEmergencyCancelAt = DateTime.Now.AddDays(-5); // lần trước là 5 ngày trước (cùng tháng)
            SetupApprovedStation(station);

            _bookingRepoMock.Setup(x => x.GetActiveBookingsByStationIdsAsync(
                It.IsAny<List<int>>(), It.IsAny<BookingStatus[]>()))
                .ReturnsAsync(new List<Booking>
                {
                    new Booking { Id = 2, Status = BookingStatus.Paid, EndTime = DateTime.Now.AddDays(1) }
                });

            var bookingServiceMock = new Mock<IBookingService>();
            bookingServiceMock.Setup(x => x.CancelSystemBookingAsync(It.IsAny<int>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            var scopeMock = new Mock<IServiceScope>();
            var scopeFactoryMock = new Mock<IServiceScopeFactory>();
            var scopedProviderMock = new Mock<IServiceProvider>();

            scopedProviderMock.Setup(x => x.GetService(typeof(IBookingService))).Returns(bookingServiceMock.Object);
            scopeMock.Setup(x => x.ServiceProvider).Returns(scopedProviderMock.Object);
            scopeFactoryMock.Setup(x => x.CreateScope()).Returns(scopeMock.Object);
            _serviceProviderMock.Setup(x => x.GetService(typeof(IServiceScopeFactory))).Returns(scopeFactoryMock.Object);

            await CreateService().UpdateOperationalStatusAsync(1, 1, "Inactive");

            Assert.NotNull(station.BannedUntil);
            Assert.True(station.BannedUntil > DateTime.Now.AddDays(25)); // khoảng 30 ngày
        }

        // TC09: Chuyển sang Active → đổi status trực tiếp, không cần check booking
        [Fact]
        public async Task UpdateStatus_ToActive_UpdatesDirectly()
        {
            var station = CreateApprovedStation();
            station.OperationalStatus = OperationalStatus.Inactive;
            SetupApprovedStation(station);

            await CreateService().UpdateOperationalStatusAsync(1, 1, "Active");

            Assert.Equal(OperationalStatus.Active, station.OperationalStatus);
            _bookingRepoMock.Verify(x => x.GetActiveBookingsByStationIdsAsync(
                It.IsAny<List<int>>(), It.IsAny<BookingStatus[]>()), Times.Never);
        }
    }
}
