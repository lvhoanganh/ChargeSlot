using ChargeSlot.Api.Enums;
using ChargeSlot.Api.Models;
using Moq;

namespace ChargeSlot.Tests.Services.ChargingSessionServiceTests
{
    public class StopChargingTests : ChargingSessionServiceTestBase
    {
        private const int OwnerUserId  = 10;
        private const int WrongOwner   = 99;
        private const int SessionId    = 1;

        // TC01
        [Fact]
        public async Task StopCharging_SessionNotFound_ShouldThrow()
        {
            // default: GetByIdWithDetailsAsync → null

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().StopChargingAsync(OwnerUserId, SessionId));

            Assert.Contains("Session", ex.Message);
        }

        // TC02
        [Fact]
        public async Task StopCharging_WrongOwner_ShouldThrow()
        {
            var now     = DateTime.Now;
            var booking = CreatePaidBooking(start: now.AddHours(-2), end: now.AddMinutes(-1));
            booking.Status = BookingStatus.CheckedIn;

            var session = CreateActiveSession(booking);

            _sessionRepoMock.Setup(x => x.GetByIdWithDetailsAsync(SessionId))
                            .ReturnsAsync(session);

            // WrongOwner (99) ≠ OwnerUserId (10)
            var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                CreateService().StopChargingAsync(WrongOwner, SessionId));

            Assert.Contains("quyền", ex.Message);
        }

        // TC03
        [Fact]
        public async Task StopCharging_BookingNotCheckedIn_ShouldThrow()
        {
            var booking = CreatePaidBooking(); // Status = Paid (not CheckedIn)
            var session = CreateActiveSession(booking);

            _sessionRepoMock.Setup(x => x.GetByIdWithDetailsAsync(SessionId))
                            .ReturnsAsync(session);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().StopChargingAsync(OwnerUserId, SessionId));

            Assert.Contains("CheckedIn", ex.Message);
        }

        // TC04 — chưa hết giờ VÀ driver chưa request early end
        [Fact]
        public async Task StopCharging_TimeNotUpAndNoEarlyRequest_ShouldThrow()
        {
            var now      = DateTime.Now;
            var booking  = CreatePaidBooking(
                start: now.AddMinutes(-30),
                end:   now.AddMinutes(30));   // còn 30 phút nữa mới hết
            booking.Status = BookingStatus.CheckedIn;
            booking.EarlyEndRequestedAt = null; // chưa request

            var session = CreateActiveSession(booking);
            _sessionRepoMock.Setup(x => x.GetByIdWithDetailsAsync(SessionId))
                            .ReturnsAsync(session);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().StopChargingAsync(OwnerUserId, SessionId));

            Assert.Contains("kết thúc sớm", ex.Message);
        }

        // TC05 — Happy path: hết giờ sạc (now >= EndTime)
        [Fact]
        public async Task StopCharging_TimeUp_Success()
        {
            var now     = DateTime.Now;
            var booking = CreatePaidBooking(
                start: now.AddHours(-2),
                end:   now.AddMinutes(-1)); // đã hết giờ
            booking.Status             = BookingStatus.CheckedIn;
            booking.EarlyEndRequestedAt= null;
            booking.TotalAmount        = 200_000m; // 200k VND

            var session    = CreateActiveSession(booking);
            var trackedSlot = CreateActiveSlot();

            _sessionRepoMock.Setup(x => x.GetByIdWithDetailsAsync(SessionId)).ReturnsAsync(session);
            _slotRepoMock.Setup(x => x.GetByIdAsync(booking.SlotId, true)).ReturnsAsync(trackedSlot);

            var result = await CreateService().StopChargingAsync(OwnerUserId, SessionId);

            // Booking chuyển sang CompletedPendingInvoice
            Assert.Equal(BookingStatus.CompletedPendingInvoice.ToString(), result.BookingStatus);

            // Session được cập nhật ActualEndTime
            _sessionRepoMock.Verify(x => x.Update(It.Is<ChargingSession>(s => s.ActualEndTime != null)), Times.Once);

            // Invoice được tạo
            _invoiceRepoMock.Verify(x => x.Add(It.IsAny<Invoice>()), Times.Once);

            // Slot về Active
            Assert.Equal(SlotStatus.Active, trackedSlot.Status);

            // Notify driver
            _notifyMock.Verify(x => x.SendAsync(
                booking.DriverUserId,
                It.IsAny<string>(), It.IsAny<string>(), NotificationType.Booking), Times.Once);

            // Transaction commit
            _transactionMock.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
