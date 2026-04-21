using ChargeSlot.Api.Enums;
using ChargeSlot.Api.Models;
using Moq;

namespace ChargeSlot.Tests.Services.ChargingSessionServiceTests
{
    public class CheckInTests : ChargingSessionServiceTestBase
    {
        private const int DriverUserId = 5;
        private const string ValidQr   = "QR-SLOT-001";

        // TC01
        [Fact]
        public async Task CheckIn_InvalidQr_ShouldThrow()
        {
            // QR không tìm thấy slot → null
            _slotRepoMock.Setup(x => x.GetByQrCodeTokenAsync(ValidQr))
                         .ReturnsAsync((ChargingSlot?)null);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().CheckInAsync(DriverUserId, ValidQr));

            Assert.Contains("QR", ex.Message);
        }

        // TC02
        [Fact]
        public async Task CheckIn_SlotMaintenance_ShouldThrow()
        {
            var slot = CreateActiveSlot();
            slot.Status = SlotStatus.Maintenance;

            _slotRepoMock.Setup(x => x.GetByQrCodeTokenAsync(ValidQr)).ReturnsAsync(slot);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().CheckInAsync(DriverUserId, ValidQr));

            Assert.Contains("bảo trì", ex.Message);
        }

        // TC03
        [Fact]
        public async Task CheckIn_NoPaidBooking_ShouldThrow()
        {
            var slot = CreateActiveSlot();
            _slotRepoMock.Setup(x => x.GetByQrCodeTokenAsync(ValidQr)).ReturnsAsync(slot);
            // không có booking paid → default null

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().CheckInAsync(DriverUserId, ValidQr));

            Assert.Contains("booking", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        // TC04 — check-in quá sớm: StartTime = now + 60 phút, window = 15 phút
        // → earliestCheckin = now + 45 phút, nhưng now < earliestCheckin → throw
        [Fact]
        public async Task CheckIn_TooEarly_ShouldThrow()
        {
            var slot    = CreateActiveSlot();
            var now     = DateTime.Now;
            var booking = CreatePaidBooking(
                driverUserId: DriverUserId,
                start: now.AddMinutes(60),   // StartTime xa quá → window chưa mở
                end:   now.AddHours(3));

            _slotRepoMock.Setup(x => x.GetByQrCodeTokenAsync(ValidQr)).ReturnsAsync(slot);
            _bookingRepoMock.Setup(x => x.GetPaidBookingForDriverAndSlotAsync(DriverUserId, slot.Id))
                            .ReturnsAsync(booking);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().CheckInAsync(DriverUserId, ValidQr));

            Assert.Contains("Chưa đến giờ check-in", ex.Message);
        }

        // TC05 — check-in quá muộn: EndTime đã qua
        [Fact]
        public async Task CheckIn_TooLate_ShouldThrow()
        {
            var slot    = CreateActiveSlot();
            var now     = DateTime.Now;
            var booking = CreatePaidBooking(
                driverUserId: DriverUserId,
                start: now.AddHours(-3),   // đã bắt đầu từ lâu
                end:   now.AddMinutes(-5)); // đã kết thúc 5 phút trước

            _slotRepoMock.Setup(x => x.GetByQrCodeTokenAsync(ValidQr)).ReturnsAsync(slot);
            _bookingRepoMock.Setup(x => x.GetPaidBookingForDriverAndSlotAsync(DriverUserId, slot.Id))
                            .ReturnsAsync(booking);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().CheckInAsync(DriverUserId, ValidQr));

            Assert.Contains("quá thời gian", ex.Message);
        }

        // TC06 — Happy path: check-in trong window (StartTime = now - 10 phút, window = 15 → mở rồi)
        [Fact]
        public async Task CheckIn_Success_ShouldCreateSession()
        {
            var slot    = CreateActiveSlot();
            var now     = DateTime.Now;
            var booking = CreatePaidBooking(
                driverUserId: DriverUserId,
                slotId: slot.Id,
                start: now.AddMinutes(-10),   // đang trong thời gian sạc
                end:   now.AddHours(1));

            var trackedSlot = CreateActiveSlot(); // slot for GetByIdAsync (tracking)

            _slotRepoMock.Setup(x => x.GetByQrCodeTokenAsync(ValidQr)).ReturnsAsync(slot);
            _bookingRepoMock.Setup(x => x.GetPaidBookingForDriverAndSlotAsync(DriverUserId, slot.Id))
                            .ReturnsAsync(booking);
            _slotRepoMock.Setup(x => x.GetByIdAsync(slot.Id, true)).ReturnsAsync(trackedSlot);

            var result = await CreateService().CheckInAsync(DriverUserId, ValidQr);

            // Booking status được cập nhật
            Assert.Equal(BookingStatus.CheckedIn.ToString(), result.BookingStatus);

            // Session được Add
            _sessionRepoMock.Verify(x => x.Add(It.IsAny<ChargingSession>()), Times.Once);

            // Notify owner
            _notifyMock.Verify(x => x.SendAsync(
                slot.ChargingStation.OwnerUserId,
                It.IsAny<string>(), It.IsAny<string>(), NotificationType.Booking), Times.Once);
        }
    }
}
