using ChargeSlot.Api.Models;
using ChargeSlot.Api.Enums;
using Moq;

namespace ChargeSlot.Tests.Services.BookingServiceTests
{
    public class OwnerCancelBookingTests : BookingServiceTestBase
    {
        private static Booking MakeBooking(BookingStatus status, int ownerId = 99, int driverId = 10, decimal amount = 300_000m)
            => new Booking
            {
                Id                   = 1,
                DriverUserId         = driverId,
                Status               = status,
                SlotId               = 1,
                TotalAmount          = amount,
                PointsUsed           = 0m,
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

        // TC01: Booking không tồn tại
        [Fact]
        public async Task TC01_BookingNotFound_ShouldThrow()
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().OwnerCancelBookingAsync(99, 999, null));
        }

        // TC02: Sai owner
        [Fact]
        public async Task TC02_WrongOwner_ShouldThrowUnauthorized()
        {
            _bookingRepo.Setup(x => x.GetByIdWithDetailsAsync(1))
                .ReturnsAsync(MakeBooking(BookingStatus.Paid, ownerId: 99));

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                CreateService().OwnerCancelBookingAsync(ownerUserId: 1, bookingId: 1, cancelReason: null));
        }

        // TC03: WaitingOwner → bị chặn (phải dùng Reject)
        [Fact]
        public async Task TC03_WaitingOwnerStatus_ShouldThrow_UseRejectInstead()
        {
            _bookingRepo.Setup(x => x.GetByIdWithDetailsAsync(1))
                .ReturnsAsync(MakeBooking(BookingStatus.WaitingOwner));

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().OwnerCancelBookingAsync(99, 1, null));
        }

        // TC04: Completed/Cancelled/Rejected → bị chặn
        [Theory]
        [InlineData(BookingStatus.Completed)]
        [InlineData(BookingStatus.Cancelled)]
        [InlineData(BookingStatus.Rejected)]
        public async Task TC04_TerminalStatus_ShouldThrow(BookingStatus status)
        {
            _bookingRepo.Setup(x => x.GetByIdWithDetailsAsync(1))
                .ReturnsAsync(MakeBooking(status));

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().OwnerCancelBookingAsync(99, 1, null));
        }

        // TC05: Cancel PendingPayment → Cancelled, KHÔNG refund (chưa trả)
        [Fact]
        public async Task TC05_CancelPendingPayment_ShouldNotRefund()
        {
            var booking = MakeBooking(BookingStatus.PendingPayment);
            _bookingRepo.Setup(x => x.GetByIdWithDetailsAsync(1)).ReturnsAsync(booking);
            _slotRepo.Setup(x => x.GetByIdAsync(It.IsAny<int>(), It.IsAny<bool>()))
                .ReturnsAsync(booking.ChargingSlot);

            var result = await CreateService().OwnerCancelBookingAsync(99, 1, "Lý do");

            Assert.Equal("Cancelled", result.Status);
            _walletRepo.Verify(x => x.TransferAtomicAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<decimal>()), Times.Never);
        }

        // TC06: Cancel Paid → hoàn 100% cho Driver
        [Fact]
        public async Task TC06_CancelPaid_ShouldRefund100ToDriver()
        {
            var booking = MakeBooking(BookingStatus.Paid, amount: 500_000m);
            booking.ChargingSlot.Status = SlotStatus.Booked;
            _bookingRepo.Setup(x => x.GetByIdWithDetailsAsync(1)).ReturnsAsync(booking);
            _slotRepo.Setup(x => x.GetByIdAsync(It.IsAny<int>(), It.IsAny<bool>()))
                .ReturnsAsync(booking.ChargingSlot);

            var result = await CreateService().OwnerCancelBookingAsync(99, 1, "Bảo trì");

            Assert.Equal("Cancelled", result.Status);
            // TransferAtomicAsync được gọi để hoàn 100% (ESCROW → Driver)
            _walletRepo.Verify(x => x.TransferAtomicAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.Is<decimal>(a => a > 0)), Times.AtLeastOnce);
        }

        // TC07: Hoàn stock extra service
        [Fact]
        public async Task TC07_WithExtraService_ShouldRestoreStock()
        {
            var booking = MakeBooking(BookingStatus.Paid);
            booking.BookingExtraServices = new List<BookingExtraService>
            {
                new BookingExtraService { ServiceId = 7, Quantity = 4 }
            };
            _bookingRepo.Setup(x => x.GetByIdWithDetailsAsync(1)).ReturnsAsync(booking);
            _slotRepo.Setup(x => x.GetByIdAsync(It.IsAny<int>(), It.IsAny<bool>()))
                .ReturnsAsync(booking.ChargingSlot);

            var svc = new ExtraService { Id = 7, TotalStock = 3 };
            _extraRepo.Setup(x => x.GetByIdAsync(7)).ReturnsAsync(svc);

            await CreateService().OwnerCancelBookingAsync(99, 1, null);

            Assert.Equal(7, svc.TotalStock); // 3 + 4 = 7
        }

        // TC08: Hoàn điểm loyalty
        [Fact]
        public async Task TC08_WithLoyaltyPoints_ShouldRefundPoints()
        {
            var booking = MakeBooking(BookingStatus.PendingPayment);
            booking.PointsUsed = 60m;
            _bookingRepo.Setup(x => x.GetByIdWithDetailsAsync(1)).ReturnsAsync(booking);
            _slotRepo.Setup(x => x.GetByIdAsync(It.IsAny<int>(), It.IsAny<bool>()))
                .ReturnsAsync(booking.ChargingSlot);
            _driverRepo.Setup(x => x.GetByUserIdAsync(10, true))
                .ReturnsAsync(new Driver { UserId = 10, LoyaltyPoints = 0m });

            await CreateService().OwnerCancelBookingAsync(99, 1, null);

            _loyaltyRepo.Verify(x => x.Add(It.Is<LoyaltyTransaction>(
                t => t.Type == "Refund" && t.Points == 60m)), Times.Once);
        }

        // TC09: Slot Booked → release về Active
        [Fact]
        public async Task TC09_SlotBooked_ShouldReleaseToActive()
        {
            var booking = MakeBooking(BookingStatus.Paid);
            booking.ChargingSlot.Status = SlotStatus.Booked;
            _bookingRepo.Setup(x => x.GetByIdWithDetailsAsync(1)).ReturnsAsync(booking);
            _slotRepo.Setup(x => x.GetByIdAsync(It.IsAny<int>(), It.IsAny<bool>()))
                .ReturnsAsync(booking.ChargingSlot);

            await CreateService().OwnerCancelBookingAsync(99, 1, null);

            Assert.Equal(SlotStatus.Active, booking.ChargingSlot.Status);
        }

        // TC10: Notify Driver khi cancel Paid
        [Fact]
        public async Task TC10_CancelPaid_DriverNotified_WithRefundMention()
        {
            var booking = MakeBooking(BookingStatus.Paid, driverId: 10, amount: 200_000m);
            booking.ChargingSlot.Status = SlotStatus.Booked;
            _bookingRepo.Setup(x => x.GetByIdWithDetailsAsync(1)).ReturnsAsync(booking);
            _slotRepo.Setup(x => x.GetByIdAsync(It.IsAny<int>(), It.IsAny<bool>()))
                .ReturnsAsync(booking.ChargingSlot);

            await CreateService().OwnerCancelBookingAsync(99, 1, "Lý do");

            _notiMock.Verify(x => x.SendAsync(
                10, It.IsAny<string>(),
                It.Is<string>(m => m.Contains("hoàn") || m.Contains("200")),
                NotificationType.Booking), Times.AtLeastOnce);
        }

        // TC11: Notify Driver khi cancel PendingPayment (không đề cập hoàn tiền)
        [Fact]
        public async Task TC11_CancelPendingPayment_DriverNotified()
        {
            var booking = MakeBooking(BookingStatus.PendingPayment, driverId: 10);
            _bookingRepo.Setup(x => x.GetByIdWithDetailsAsync(1)).ReturnsAsync(booking);
            _slotRepo.Setup(x => x.GetByIdAsync(It.IsAny<int>(), It.IsAny<bool>()))
                .ReturnsAsync(booking.ChargingSlot);

            await CreateService().OwnerCancelBookingAsync(99, 1, null);

            // Driver phải nhận thông báo
            _notiMock.Verify(x => x.SendAsync(
                10, It.IsAny<string>(), It.IsAny<string>(), NotificationType.Booking), Times.AtLeastOnce);
        }
    }
}
