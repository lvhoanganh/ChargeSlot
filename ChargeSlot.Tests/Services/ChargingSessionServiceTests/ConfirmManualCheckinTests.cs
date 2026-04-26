using ChargeSlot.Api.Enums;
using ChargeSlot.Api.Models;
using Moq;

namespace ChargeSlot.Tests.Services.ChargingSessionServiceTests
{
    public class ConfirmManualCheckinTests : ChargingSessionServiceTestBase
    {
        private const int OwnerUserId = 10;
        private const int WrongOwner  = 99;
        private const int BookingId   = 1;

        private Booking CreateBookingWithManualRequest(decimal totalAmount = 150_000m)
        {
            var now     = DateTime.Now;
            var booking = CreatePaidBooking(
                bookingId:    BookingId,
                driverUserId: 5,
                start: now.AddHours(-2),
                end:   now.AddHours(-1));   // đã hết giờ
            booking.TotalAmount             = totalAmount;
            booking.ManualCheckinRequestedAt= now.AddHours(-1).AddMinutes(-30); // đã gửi request
            return booking;
        }

        // TC01
        [Fact]
        public async Task ConfirmManualCheckin_WrongOwner_ShouldThrow()
        {
            var booking = CreateBookingWithManualRequest();
            _bookingRepoMock.Setup(x => x.GetByIdWithDetailsAsync(BookingId)).ReturnsAsync(booking);

            var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                CreateService().ConfirmManualCheckinAsync(WrongOwner, BookingId));

            Assert.Contains("quyền", ex.Message);
        }

        // TC02 — Driver chưa gửi manual request
        [Fact]
        public async Task ConfirmManualCheckin_NoManualRequest_ShouldThrow()
        {
            var booking = CreateBookingWithManualRequest();
            booking.ManualCheckinRequestedAt = null; // chưa gửi

            _bookingRepoMock.Setup(x => x.GetByIdWithDetailsAsync(BookingId)).ReturnsAsync(booking);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().ConfirmManualCheckinAsync(OwnerUserId, BookingId));

            Assert.Contains("chưa gửi", ex.Message);
        }

        // TC03 — Happy path: tạo session + invoice + Completed + loyalty + settlement
        // Booking 150k → VAT 8% = 12k, Platform 5% = 7.5k → ownerNet = 130.5k
        // Loyalty 5% → 7,500 điểm
        [Fact]
        public async Task ConfirmManualCheckin_Success_ShouldCompleteBookingWithAllSideEffects()
        {
            var booking = CreateBookingWithManualRequest(totalAmount: 150_000m);

            var trackedSlot = CreateActiveSlot();
            trackedSlot.Status = SlotStatus.Booked; // đang bị chiếm

            _bookingRepoMock.Setup(x => x.GetByIdWithDetailsAsync(BookingId)).ReturnsAsync(booking);
            _slotRepoMock.Setup(x => x.GetByIdAsync(booking.SlotId, true)).ReturnsAsync(trackedSlot);

            // Owner wallet
            var ownerWallet = new Wallet { Id = 200, UserId = OwnerUserId, AvailableBalance = 0m };
            _walletRepoMock.Setup(x => x.GetByUserIdAsync(OwnerUserId)).ReturnsAsync(ownerWallet);

            var result = await CreateService().ConfirmManualCheckinAsync(OwnerUserId, BookingId);

            // ── Booking Completed ──
            Assert.Equal(BookingStatus.Completed.ToString(), result.Status);
            Assert.NotNull(booking.CheckedInAt); // CheckedInAt = ManualCheckinRequestedAt

            // ── Session được tạo ──
            _sessionRepoMock.Verify(x => x.Add(It.Is<ChargingSession>(s =>
                s.BookingId == BookingId &&
                s.ActualStartTime == booking.StartTime)), Times.Once);

            // ── Invoice Confirmed ngay (không chờ 24h) ──
            _invoiceRepoMock.Verify(x => x.Add(It.Is<Invoice>(i =>
                i.Status == InvoiceStatus.Confirmed &&
                i.BookingId == BookingId)), Times.Once);

            // ── Loyalty: 150_000 * 5% = 7_500 điểm ──
            Assert.Equal(7_500m, booking.PointsEarned);
            Assert.Equal(8_500m, booking.Driver.LoyaltyPoints); // 1000 + 7500
            _loyaltyRepoMock.Verify(x => x.Add(It.Is<LoyaltyTransaction>(t =>
                t.Points == 7_500m && t.Type == "Earn")), Times.Once);

            // ── Slot released → Active ──
            Assert.Equal(SlotStatus.Active, trackedSlot.Status);

            // ── Settlement: TransferAtomic gọi ít nhất 2 lần ──
            _walletRepoMock.Verify(x => x.TransferAtomicAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<decimal>()), Times.AtLeast(2));

            // ── Transaction commit ──
            _transactionMock.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);

            // ── Notify cả driver lẫn owner ──
            _notifyMock.Verify(x => x.SendAsync(
                booking.DriverUserId,
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<NotificationType>()), Times.Once);
            _notifyMock.Verify(x => x.SendAsync(
                OwnerUserId,
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<NotificationType>()), Times.Once);
        }
    }
}
