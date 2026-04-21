using ChargeSlot.Api.Enums;
using ChargeSlot.Api.Models;
using Moq;

namespace ChargeSlot.Tests.Services.ChargingSessionServiceTests
{
    public class RequestManualCheckinTests : ChargingSessionServiceTestBase
    {
        private const int DriverUserId = 5;
        private const int WrongDriver  = 99;
        private const int BookingId    = 1;

        // TC01
        [Fact]
        public async Task RequestManualCheckin_WrongDriver_ShouldThrow()
        {
            var booking = CreatePaidBooking(bookingId: BookingId, driverUserId: DriverUserId);
            _bookingRepoMock.Setup(x => x.GetByIdWithDetailsAsync(BookingId)).ReturnsAsync(booking);

            var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                CreateService().RequestManualCheckinAsync(WrongDriver, BookingId));

            Assert.Contains("không thuộc", ex.Message);
        }

        // TC02
        [Fact]
        public async Task RequestManualCheckin_BookingNotPaid_ShouldThrow()
        {
            var booking = CreatePaidBooking(bookingId: BookingId, driverUserId: DriverUserId);
            booking.Status = BookingStatus.WaitingOwner; // chưa thanh toán

            _bookingRepoMock.Setup(x => x.GetByIdWithDetailsAsync(BookingId)).ReturnsAsync(booking);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().RequestManualCheckinAsync(DriverUserId, BookingId));

            Assert.Contains("thanh toán", ex.Message);
        }

        // TC03 — Happy path
        [Fact]
        public async Task RequestManualCheckin_Success_ShouldSetTimestampAndNotifyOwner()
        {
            var booking = CreatePaidBooking(bookingId: BookingId, driverUserId: DriverUserId);
            // ManualCheckinRequestedAt = null (mặc định từ CreatePaidBooking)

            _bookingRepoMock.Setup(x => x.GetByIdWithDetailsAsync(BookingId)).ReturnsAsync(booking);

            var result = await CreateService().RequestManualCheckinAsync(DriverUserId, BookingId);

            // ManualCheckinRequestedAt phải được set
            Assert.NotNull(booking.ManualCheckinRequestedAt);

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
