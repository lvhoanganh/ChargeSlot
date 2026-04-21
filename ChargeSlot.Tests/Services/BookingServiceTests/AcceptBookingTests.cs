using ChargeSlot.Api.Models;
using ChargeSlot.Api.Enums;
using ChargeSlot.Api.DTOs.Booking;
using Moq;

namespace ChargeSlot.Tests.Services.BookingServiceTests
{
    public class AcceptBookingTests : BookingServiceTestBase
    {
        // Helper 
        private static Booking MakeWaitingBooking(int ownerId = 99, int driverId = 10,
            DateTime? start = null) => new Booking
        {
            Id           = 1,
            DriverUserId = driverId,
            SlotId       = 5,
            Status       = BookingStatus.WaitingOwner,
            StartTime    = start ?? DateTime.UtcNow.AddHours(7).AddHours(3),
            BookingExtraServices = new List<BookingExtraService>(),
            ChargingSlot = new ChargingSlot
            {
                SlotName = "Slot A",
                ChargingStation = new ChargingStation { OwnerUserId = ownerId, Name = "Station X" }
            }
        };

        // TC01: Booking không tồn tại
        [Fact]
        public async Task TC01_BookingNotFound_ShouldThrow()
        {
            // _bookingRepo default GetByIdWithDetailsAsync → null
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().AcceptBookingAsync(99, 999));
        }

        // TC02: Sai owner
        [Fact]
        public async Task TC02_WrongOwner_ShouldThrowUnauthorized()
        {
            var booking = MakeWaitingBooking(ownerId: 99);
            _bookingRepo.Setup(x => x.GetByIdWithDetailsAsync(1)).ReturnsAsync(booking);

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                CreateService().AcceptBookingAsync(ownerUserId: 1, bookingId: 1));
        }

        // TC03: Booking không ở WaitingOwner
        [Theory]
        [InlineData(BookingStatus.PendingPayment)]
        [InlineData(BookingStatus.Paid)]
        [InlineData(BookingStatus.Rejected)]
        [InlineData(BookingStatus.Completed)]
        public async Task TC03_WrongStatus_ShouldThrow(BookingStatus status)
        {
            var booking = MakeWaitingBooking();
            booking.Status = status;
            _bookingRepo.Setup(x => x.GetByIdWithDetailsAsync(1)).ReturnsAsync(booking);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().AcceptBookingAsync(99, 1));
        }

        // TC04: Race condition — Slot đã có booking accept trùng giờ
        [Fact]
        public async Task TC04_SlotConflict_RaceCondition_ShouldThrow()
        {
            var booking = MakeWaitingBooking();
            _bookingRepo.Setup(x => x.GetByIdWithDetailsAsync(1)).ReturnsAsync(booking);
            // Có booking khác đã accept trùng giờ trên slot này
            _bookingRepo.Setup(x => x.HasOverlappingBookingAsync(
                It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(),
                It.IsAny<int>(), It.IsAny<int?>())).ReturnsAsync(true);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().AcceptBookingAsync(99, 1));
        }

        // TC05: PaymentExpiry khi StartTime gần (< paymentExpiryMinutes)
        [Fact]
        public async Task TC05_PaymentExpiry_StartsVerySOon_ShouldSetToStartTime()
        {
            // StartTime chỉ còn 5 phút nữa → < 15p (payment expiry) → ExpiresAt = StartTime
            var startTime = DateTime.UtcNow.AddHours(7).AddMinutes(5);
            var booking   = MakeWaitingBooking(start: startTime);
            _bookingRepo.Setup(x => x.GetByIdWithDetailsAsync(1)).ReturnsAsync(booking);
            _bookingRepo.Setup(x => x.HasOverlappingBookingAsync(
                It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(),
                It.IsAny<int>(), It.IsAny<int?>())).ReturnsAsync(false);

            var result = await CreateService().AcceptBookingAsync(99, 1);

            Assert.Equal(startTime, result.PaymentExpiresAt);
        }

        // TC06: PaymentExpiry bình thường (StartTime xa)
        [Fact]
        public async Task TC06_PaymentExpiry_NormalCase_ShouldSetToNowPlusExpiry()
        {
            // StartTime còn 3 giờ → > 15p → ExpiresAt ≈ Now + 15p
            var booking = MakeWaitingBooking(start: DateTime.UtcNow.AddHours(7).AddHours(3));
            _bookingRepo.Setup(x => x.GetByIdWithDetailsAsync(1)).ReturnsAsync(booking);
            _bookingRepo.Setup(x => x.HasOverlappingBookingAsync(
                It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(),
                It.IsAny<int>(), It.IsAny<int?>())).ReturnsAsync(false);

            var before = DateTime.UtcNow.AddHours(7);
            var result = await CreateService().AcceptBookingAsync(99, 1);
            var after  = DateTime.UtcNow.AddHours(7);

            Assert.True(result.PaymentExpiresAt >= before.AddMinutes(14));
            Assert.True(result.PaymentExpiresAt <= after.AddMinutes(16));
        }

        // TC07: Auto-reject overlapping WaitingOwner bookings + restore
        [Fact]
        public async Task TC07_AutoReject_OverlappingBookings_ShouldRejectAndNotify()
        {
            var booking = MakeWaitingBooking(driverId: 10);
            _bookingRepo.Setup(x => x.GetByIdWithDetailsAsync(1)).ReturnsAsync(booking);
            _bookingRepo.Setup(x => x.HasOverlappingBookingAsync(
                It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(),
                It.IsAny<int>(), It.IsAny<int?>())).ReturnsAsync(false);

            // 2 booking trùng giờ của driver khác
            var overlapping1 = new Booking
            {
                Id = 2, DriverUserId = 20, Status = BookingStatus.WaitingOwner,
                BookingExtraServices = new List<BookingExtraService>(),
                ChargingSlot = booking.ChargingSlot
            };
            var overlapping2 = new Booking
            {
                Id = 3, DriverUserId = 30, Status = BookingStatus.WaitingOwner,
                BookingExtraServices = new List<BookingExtraService>(),
                ChargingSlot = booking.ChargingSlot
            };
            _bookingRepo.Setup(x => x.GetOverlappingWaitingBookingsAsync(
                It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(),
                It.IsAny<int>(), It.IsAny<int>()))
                .ReturnsAsync(new List<Booking> { overlapping1, overlapping2 });

            // Driver repo để RefundLoyaltyPoints không throw
            _driverRepo.Setup(x => x.GetByUserIdAsync(It.IsAny<int>(), It.IsAny<bool>()))
                .ReturnsAsync((Driver?)null);

            await CreateService().AcceptBookingAsync(99, 1);

            // Cả 2 booking bị auto-reject
            Assert.Equal(BookingStatus.Rejected, overlapping1.Status);
            Assert.Equal(BookingStatus.Rejected, overlapping2.Status);

            // Notify 2 driver bị reject
            _notiMock.Verify(x => x.SendAsync(
                20, It.IsAny<string>(), It.IsAny<string>(), NotificationType.Booking), Times.Once);
            _notiMock.Verify(x => x.SendAsync(
                30, It.IsAny<string>(), It.IsAny<string>(), NotificationType.Booking), Times.Once);
        }

        // TC08: Happy path — status → PendingPayment
        [Fact]
        public async Task TC08_HappyPath_StatusSetToPendingPayment()
        {
            var booking = MakeWaitingBooking();
            _bookingRepo.Setup(x => x.GetByIdWithDetailsAsync(1)).ReturnsAsync(booking);
            _bookingRepo.Setup(x => x.HasOverlappingBookingAsync(
                It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(),
                It.IsAny<int>(), It.IsAny<int?>())).ReturnsAsync(false);

            var result = await CreateService().AcceptBookingAsync(99, 1);

            Assert.Equal("PendingPayment", result.Status);
            Assert.NotNull(result.PaymentExpiresAt);
            _bookingRepo.Verify(x => x.Update(booking), Times.AtLeastOnce);
        }

        // TC09: Notify Driver được gọi đúng
        [Fact]
        public async Task TC09_HappyPath_DriverNotified()
        {
            var booking = MakeWaitingBooking(driverId: 42);
            _bookingRepo.Setup(x => x.GetByIdWithDetailsAsync(1)).ReturnsAsync(booking);
            _bookingRepo.Setup(x => x.HasOverlappingBookingAsync(
                It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(),
                It.IsAny<int>(), It.IsAny<int?>())).ReturnsAsync(false);

            await CreateService().AcceptBookingAsync(99, 1);

            _notiMock.Verify(x => x.SendAsync(
                42, It.IsAny<string>(), It.IsAny<string>(), NotificationType.Booking), Times.Once);
        }

        // TC10: Booking bị auto-reject phải có RejectionReason
        [Fact]
        public async Task TC10_AutoRejected_ShouldHaveRejectionReason()
        {
            var booking = MakeWaitingBooking();
            _bookingRepo.Setup(x => x.GetByIdWithDetailsAsync(1)).ReturnsAsync(booking);
            _bookingRepo.Setup(x => x.HasOverlappingBookingAsync(
                It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(),
                It.IsAny<int>(), It.IsAny<int?>())).ReturnsAsync(false);

            var conflicting = new Booking
            {
                Id = 2, DriverUserId = 55, Status = BookingStatus.WaitingOwner,
                BookingExtraServices = new List<BookingExtraService>(),
                ChargingSlot = booking.ChargingSlot
            };
            _bookingRepo.Setup(x => x.GetOverlappingWaitingBookingsAsync(
                It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(),
                It.IsAny<int>(), It.IsAny<int>()))
                .ReturnsAsync(new List<Booking> { conflicting });
            _driverRepo.Setup(x => x.GetByUserIdAsync(It.IsAny<int>(), It.IsAny<bool>()))
                .ReturnsAsync((Driver?)null);

            await CreateService().AcceptBookingAsync(99, 1);

            Assert.Equal(BookingStatus.Rejected, conflicting.Status);
            Assert.NotNull(conflicting.RejectionReason);
            Assert.NotEmpty(conflicting.RejectionReason!);
        }
    }
}
