using ChargeSlot.Api.Enums;
using ChargeSlot.Api.Models;
using Moq;

namespace ChargeSlot.Tests.Services.ChargingSessionServiceTests
{
    public class RequestEarlyEndTests : ChargingSessionServiceTestBase
    {
        private const int DriverUserId = 5;
        private const int WrongDriver  = 99;
        private const int SessionId    = 1;

        // TC01
        [Fact]
        public async Task RequestEarlyEnd_WrongDriver_ShouldThrow()
        {
            var now     = DateTime.Now;
            var booking = CreatePaidBooking(
                driverUserId: DriverUserId,
                start: now.AddMinutes(-30),
                end:   now.AddHours(1));
            booking.Status = BookingStatus.CheckedIn;
            var session = CreateActiveSession(booking);

            _sessionRepoMock.Setup(x => x.GetByIdWithDetailsAsync(SessionId)).ReturnsAsync(session);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().RequestEarlyEndAsync(WrongDriver, SessionId));

            Assert.Contains("không thuộc", ex.Message);
        }

        // TC02
        [Fact]
        public async Task RequestEarlyEnd_BookingNotCheckedIn_ShouldThrow()
        {
            var booking = CreatePaidBooking(driverUserId: DriverUserId);
            booking.Status = BookingStatus.Paid; // chưa CheckedIn
            var session    = CreateActiveSession(booking);

            _sessionRepoMock.Setup(x => x.GetByIdWithDetailsAsync(SessionId)).ReturnsAsync(session);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().RequestEarlyEndAsync(DriverUserId, SessionId));

            Assert.Contains("đang sạc", ex.Message);
        }

        // TC03
        [Fact]
        public async Task RequestEarlyEnd_AlreadyRequested_ShouldThrow()
        {
            var now     = DateTime.Now;
            var booking = CreatePaidBooking(
                driverUserId: DriverUserId,
                start: now.AddMinutes(-30),
                end:   now.AddHours(1));
            booking.Status              = BookingStatus.CheckedIn;
            booking.EarlyEndRequestedAt = now.AddMinutes(-5); // đã request rồi

            var session = CreateActiveSession(booking);
            _sessionRepoMock.Setup(x => x.GetByIdWithDetailsAsync(SessionId)).ReturnsAsync(session);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().RequestEarlyEndAsync(DriverUserId, SessionId));

            Assert.Contains("đã yêu cầu", ex.Message);
        }

        // TC04 — Happy path
        [Fact]
        public async Task RequestEarlyEnd_Success_ShouldSetTimestampAndNotifyOwner()
        {
            var now     = DateTime.Now;
            var booking = CreatePaidBooking(
                driverUserId: DriverUserId,
                start: now.AddMinutes(-30),
                end:   now.AddHours(1));
            booking.Status              = BookingStatus.CheckedIn;
            booking.EarlyEndRequestedAt = null;

            var session = CreateActiveSession(booking);
            _sessionRepoMock.Setup(x => x.GetByIdWithDetailsAsync(SessionId)).ReturnsAsync(session);

            var result = await CreateService().RequestEarlyEndAsync(DriverUserId, SessionId);

            // EarlyEndRequestedAt phải được set
            Assert.NotNull(booking.EarlyEndRequestedAt);

            // Booking được update
            _bookingRepoMock.Verify(x => x.Update(booking), Times.Once);
            _uowMock.Verify(x => x.CompleteAsync(), Times.Once);

            // Notify owner
            _notifyMock.Verify(x => x.SendAsync(
                booking.ChargingSlot.ChargingStation.OwnerUserId,
                It.IsAny<string>(), It.IsAny<string>(), NotificationType.Booking), Times.Once);
        }
    }
}
