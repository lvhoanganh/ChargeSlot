using ChargeSlot.Api.Models;
using ChargeSlot.Api.Enums;
using Moq;

namespace ChargeSlot.Tests.Services.BookingServiceTests
{
    public class DriverCancelBookingTests : BookingServiceTestBase
    {
        // Helpers
        private static Booking MakeBooking(
            BookingStatus status,
            int driverId       = 10,
            int ownerId        = 99,
            decimal amount     = 300_000m,
            DateTime? start    = null,
            DateTime? ref100   = null,
            DateTime? ref50    = null,
            decimal pointsUsed = 0m)
        {
            var startTime = start ?? DateTime.UtcNow.AddHours(7).AddHours(5);
            return new Booking
            {
                Id                   = 1,
                DriverUserId         = driverId,
                Status               = status,
                SlotId               = 1,
                TotalAmount          = amount,
                StartTime            = startTime,
                Refund100DeadlineAt  = ref100 ?? startTime.AddHours(-2),
                Refund50DeadlineAt   = ref50  ?? startTime.AddHours(-1),
                PointsUsed           = pointsUsed,
                PlatformFeeRateSnapshot = 0.05m,
                VatRateSnapshot      = 0.08m,
                BookingExtraServices = new List<BookingExtraService>(),
                ChargingSlot = new ChargingSlot
                {
                    SlotName = "Slot A",
                    Status   = SlotStatus.Active,
                    ChargingStation = new ChargingStation { OwnerUserId = ownerId, Name = "Station X" }
                }
            };
        }

        // TC01: Booking không tồn tại
        [Fact]
        public async Task TC01_BookingNotFound_ShouldThrow()
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().DriverCancelBookingAsync(10, 999, null));
        }

        // TC02: Sai driver
        [Fact]
        public async Task TC02_WrongDriver_ShouldThrowUnauthorized()
        {
            _bookingRepo.Setup(x => x.GetByIdWithDetailsAsync(1))
                .ReturnsAsync(MakeBooking(BookingStatus.WaitingOwner, driverId: 99));

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                CreateService().DriverCancelBookingAsync(driverUserId: 10, bookingId: 1, cancelReason: null));
        }

        // TC03: Status không cho phép cancel
        [Theory]
        [InlineData(BookingStatus.Completed)]
        [InlineData(BookingStatus.Rejected)]
        [InlineData(BookingStatus.Cancelled)]
        [InlineData(BookingStatus.Expired)]
        public async Task TC03_InvalidStatus_ShouldThrow(BookingStatus status)
        {
            _bookingRepo.Setup(x => x.GetByIdWithDetailsAsync(1))
                .ReturnsAsync(MakeBooking(status));

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().DriverCancelBookingAsync(10, 1, null));
        }

        // TC04: Cancel WaitingOwner → Cancelled, không refund
        [Fact]
        public async Task TC04_CancelWaitingOwner_FreeCancel_NoRefund()
        {
            var booking = MakeBooking(BookingStatus.WaitingOwner);
            _bookingRepo.Setup(x => x.GetByIdWithDetailsAsync(1)).ReturnsAsync(booking);
            _slotRepo.Setup(x => x.GetByIdAsync(It.IsAny<int>(), It.IsAny<bool>()))
                .ReturnsAsync(booking.ChargingSlot);

            var result = await CreateService().DriverCancelBookingAsync(10, 1, null);

            Assert.Equal("Cancelled", result.Status);
            // Không gọi TransferAtomicAsync (chưa trả tiền → không hoàn)
            _walletRepo.Verify(x => x.TransferAtomicAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<decimal>()), Times.Never);
        }

        // TC05: Cancel PendingPayment → Cancelled, không refund
        [Fact]
        public async Task TC05_CancelPendingPayment_FreeCancel_NoRefund()
        {
            var booking = MakeBooking(BookingStatus.PendingPayment);
            _bookingRepo.Setup(x => x.GetByIdWithDetailsAsync(1)).ReturnsAsync(booking);
            _slotRepo.Setup(x => x.GetByIdAsync(It.IsAny<int>(), It.IsAny<bool>()))
                .ReturnsAsync(booking.ChargingSlot);

            var result = await CreateService().DriverCancelBookingAsync(10, 1, null);

            Assert.Equal("Cancelled", result.Status);
            _walletRepo.Verify(x => x.TransferAtomicAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<decimal>()), Times.Never);
        }

        // TC06: Cancel Paid, trước Refund100Deadline → hoàn 100%
        [Fact]
        public async Task TC06_CancelPaid_Before100Deadline_ShouldRefund100()
        {
            // Refund100Deadline = 3h nữa → chưa qua → hoàn 100%
            var vnNow    = DateTime.UtcNow.AddHours(7);
            var ref100   = vnNow.AddHours(3); // chưa qua
            var ref50    = vnNow.AddHours(4);
            var booking  = MakeBooking(BookingStatus.Paid, ref100: ref100, ref50: ref50);
            booking.ChargingSlot.Status = SlotStatus.Booked;

            _bookingRepo.Setup(x => x.GetByIdWithDetailsAsync(1)).ReturnsAsync(booking);
            _slotRepo.Setup(x => x.GetByIdAsync(It.IsAny<int>(), It.IsAny<bool>()))
                .ReturnsAsync(booking.ChargingSlot);

            await CreateService().DriverCancelBookingAsync(10, 1, "test");

            // ProcessRefundAsync(1.0m) → TransferAtomicAsync được gọi (driver ← ESCROW)
            _walletRepo.Verify(x => x.TransferAtomicAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.Is<decimal>(a => a > 0)), Times.AtLeastOnce);
        }

        // TC07: Cancel Paid, sau 100% nhưng trước 50% deadline → hoàn 50%
        [Fact]
        public async Task TC07_CancelPaid_Between100And50Deadline_ShouldRefund50()
        {
            var vnNow   = DateTime.UtcNow.AddHours(7);
            var ref100  = vnNow.AddMinutes(-30); // đã qua
            var ref50   = vnNow.AddMinutes(30);  // chưa qua
            var booking = MakeBooking(BookingStatus.Paid, amount: 200_000m, ref100: ref100, ref50: ref50);
            booking.ChargingSlot.Status = SlotStatus.Booked;

            _bookingRepo.Setup(x => x.GetByIdWithDetailsAsync(1)).ReturnsAsync(booking);
            _slotRepo.Setup(x => x.GetByIdAsync(It.IsAny<int>(), It.IsAny<bool>()))
                .ReturnsAsync(booking.ChargingSlot);

            await CreateService().DriverCancelBookingAsync(10, 1, null);

            // refundAmount = 200_000 * 0.5 = 100_000 → TransferAtomicAsync nhận 100_000
            _walletRepo.Verify(x => x.TransferAtomicAsync(
                It.IsAny<int>(), It.IsAny<int>(),
                It.Is<decimal>(a => a == 100_000m)), Times.AtLeastOnce);
        }

        // TC08: Cancel Paid, quá 50% deadline → refund 0% (mất tiền)
        [Fact]
        public async Task TC08_CancelPaid_After50Deadline_ShouldRefund0_SettleToOwner()
        {
            var vnNow   = DateTime.UtcNow.AddHours(7);
            var ref100  = vnNow.AddMinutes(-60); // đã qua
            var ref50   = vnNow.AddMinutes(-10); // đã qua
            var booking = MakeBooking(BookingStatus.Paid, ref100: ref100, ref50: ref50);
            booking.ChargingSlot.Status = SlotStatus.Booked;

            _bookingRepo.Setup(x => x.GetByIdWithDetailsAsync(1)).ReturnsAsync(booking);
            _slotRepo.Setup(x => x.GetByIdAsync(It.IsAny<int>(), It.IsAny<bool>()))
                .ReturnsAsync(booking.ChargingSlot);

            await CreateService().DriverCancelBookingAsync(10, 1, null);

            // 0% refund → SettleCompensationToOwner → TransferAtomicAsync (ESCROW → Owner)
            _walletRepo.Verify(x => x.TransferAtomicAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.Is<decimal>(a => a > 0)), Times.AtLeastOnce);
        }

        // TC09: Slot Booked → phải release về Active
        [Fact]
        public async Task TC09_SlotBooked_ShouldReleaseToActive()
        {
            var booking = MakeBooking(BookingStatus.WaitingOwner);
            var slot    = booking.ChargingSlot;
            slot.Status = SlotStatus.Booked;

            _bookingRepo.Setup(x => x.GetByIdWithDetailsAsync(1)).ReturnsAsync(booking);
            _slotRepo.Setup(x => x.GetByIdAsync(It.IsAny<int>(), It.IsAny<bool>())).ReturnsAsync(slot);

            await CreateService().DriverCancelBookingAsync(10, 1, null);

            Assert.Equal(SlotStatus.Active, slot.Status);
        }

        // TC10: Hoàn điểm loyalty sau khi cancel
        [Fact]
        public async Task TC10_WithLoyaltyPoints_ShouldRefundPoints()
        {
            var booking = MakeBooking(BookingStatus.WaitingOwner, pointsUsed: 80m);
            _bookingRepo.Setup(x => x.GetByIdWithDetailsAsync(1)).ReturnsAsync(booking);
            _slotRepo.Setup(x => x.GetByIdAsync(It.IsAny<int>(), It.IsAny<bool>()))
                .ReturnsAsync(booking.ChargingSlot);
            _driverRepo.Setup(x => x.GetByUserIdAsync(10, true))
                .ReturnsAsync(new Driver { UserId = 10, LoyaltyPoints = 20m });

            await CreateService().DriverCancelBookingAsync(10, 1, null);

            _loyaltyRepo.Verify(x => x.Add(It.Is<LoyaltyTransaction>(
                t => t.Type == "Refund" && t.Points == 80m)), Times.Once);
        }

        // TC11: Hoàn stock extra service
        [Fact]
        public async Task TC11_WithExtraService_ShouldRestoreStock()
        {
            var booking = MakeBooking(BookingStatus.WaitingOwner);
            booking.BookingExtraServices = new List<BookingExtraService>
            {
                new BookingExtraService { ServiceId = 7, Quantity = 2 }
            };
            _bookingRepo.Setup(x => x.GetByIdWithDetailsAsync(1)).ReturnsAsync(booking);
            _slotRepo.Setup(x => x.GetByIdAsync(It.IsAny<int>(), It.IsAny<bool>()))
                .ReturnsAsync(booking.ChargingSlot);

            var svc = new ExtraService { Id = 7, TotalStock = 5 };
            _extraRepo.Setup(x => x.GetByIdAsync(7)).ReturnsAsync(svc);

            await CreateService().DriverCancelBookingAsync(10, 1, null);

            Assert.Equal(7, svc.TotalStock); // 5 + 2 = 7
        }

        // TC12:Notify Driver đúng khi Paid và có refund
        [Fact]
        public async Task TC12_PaidWithRefund_DriverNotified()
        {
            var vnNow   = DateTime.UtcNow.AddHours(7);
            var ref100  = vnNow.AddHours(3); // chưa qua → 100%
            var ref50   = vnNow.AddHours(4);
            var booking = MakeBooking(BookingStatus.Paid, driverId: 10, ref100: ref100, ref50: ref50);
            booking.ChargingSlot.Status = SlotStatus.Booked;

            _bookingRepo.Setup(x => x.GetByIdWithDetailsAsync(1)).ReturnsAsync(booking);
            _slotRepo.Setup(x => x.GetByIdAsync(It.IsAny<int>(), It.IsAny<bool>()))
                .ReturnsAsync(booking.ChargingSlot);

            await CreateService().DriverCancelBookingAsync(10, 1, "Test");

            _notiMock.Verify(x => x.SendAsync(
                10, It.IsAny<string>(),
                It.Is<string>(m => m.Contains("hoàn")),
                NotificationType.Booking), Times.AtLeastOnce);
        }

        // TC13: Notify Driver + Owner khi WaitingOwner (không paid)
        [Fact]
        public async Task TC13_WaitingOwner_NotifyBothDriverAndOwner()
        {
            var booking = MakeBooking(BookingStatus.WaitingOwner, driverId: 10, ownerId: 77);
            _bookingRepo.Setup(x => x.GetByIdWithDetailsAsync(1)).ReturnsAsync(booking);
            _slotRepo.Setup(x => x.GetByIdAsync(It.IsAny<int>(), It.IsAny<bool>()))
                .ReturnsAsync(booking.ChargingSlot);

            await CreateService().DriverCancelBookingAsync(10, 1, null);

            _notiMock.Verify(x => x.SendAsync(10, It.IsAny<string>(), It.IsAny<string>(),
                NotificationType.Booking), Times.Once);
            _notiMock.Verify(x => x.SendAsync(77, It.IsAny<string>(), It.IsAny<string>(),
                NotificationType.Booking), Times.Once);
        }

        // TC14: Booking status set Cancelled TRƯỚC khi refund (double-refund guard)
        [Fact]
        public async Task TC14_StatusSetCancelledBeforeRefund()
        {
            var vnNow   = DateTime.UtcNow.AddHours(7);
            var ref100  = vnNow.AddHours(3);
            var ref50   = vnNow.AddHours(4);
            var booking = MakeBooking(BookingStatus.Paid, ref100: ref100, ref50: ref50);
            booking.ChargingSlot.Status = SlotStatus.Booked;

            BookingStatus? statusAtRefund = null;
            _walletRepo.Setup(x => x.TransferAtomicAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<decimal>()))
                .Callback(() => statusAtRefund = booking.Status)
                .Returns(Task.CompletedTask);

            _bookingRepo.Setup(x => x.GetByIdWithDetailsAsync(1)).ReturnsAsync(booking);
            _slotRepo.Setup(x => x.GetByIdAsync(It.IsAny<int>(), It.IsAny<bool>()))
                .ReturnsAsync(booking.ChargingSlot);

            await CreateService().DriverCancelBookingAsync(10, 1, null);

            // Khi TransferAtomicAsync được gọi, booking đã là Cancelled
            Assert.Equal(BookingStatus.Cancelled, statusAtRefund);
        }
    }
}
