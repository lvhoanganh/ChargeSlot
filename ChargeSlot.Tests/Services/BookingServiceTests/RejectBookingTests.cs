using ChargeSlot.Api.Models;
using ChargeSlot.Api.Enums;
using ChargeSlot.Api.DTOs.Booking;
using Moq;

namespace ChargeSlot.Tests.Services.BookingServiceTests
{
    public class RejectBookingTests : BookingServiceTestBase
    {
        private static Booking MakeWaitingBooking(int ownerId = 99, int driverId = 10, decimal points = 0)
            => new Booking
            {
                Id           = 1,
                DriverUserId = driverId,
                Status       = BookingStatus.WaitingOwner,
                PointsUsed   = points,
                BookingExtraServices = new List<BookingExtraService>(),
                ChargingSlot = new ChargingSlot
                {
                    SlotName = "Slot A",
                    ChargingStation = new ChargingStation { OwnerUserId = ownerId, Name = "Station X" }
                }
            };

        private static RejectBookingDto Dto(string reason = "Bảo trì")
            => new RejectBookingDto { RejectionReason = reason };

        // TC01: Booking không tồn tại
        [Fact]
        public async Task TC01_BookingNotFound_ShouldThrow()
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().RejectBookingAsync(99, 999, Dto()));
        }

        // TC02: Sai owner
        [Fact]
        public async Task TC02_WrongOwner_ShouldThrowUnauthorized()
        {
            _bookingRepo.Setup(x => x.GetByIdWithDetailsAsync(1))
                .ReturnsAsync(MakeWaitingBooking(ownerId: 99));

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                CreateService().RejectBookingAsync(ownerUserId: 1, bookingId: 1, dto: Dto()));
        }

        // TC03: Booking không ở WaitingOwner
        [Theory]
        [InlineData(BookingStatus.PendingPayment)]
        [InlineData(BookingStatus.Paid)]
        [InlineData(BookingStatus.Completed)]
        public async Task TC03_WrongStatus_ShouldThrow(BookingStatus status)
        {
            var booking = MakeWaitingBooking();
            booking.Status = status;
            _bookingRepo.Setup(x => x.GetByIdWithDetailsAsync(1)).ReturnsAsync(booking);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().RejectBookingAsync(99, 1, Dto()));
        }

        // TC04: Booking đã dùng điểm → phải hoàn điểm sau khi reject
        [Fact]
        public async Task TC04_WithLoyaltyPoints_ShouldRefundPoints()
        {
            var booking = MakeWaitingBooking(points: 50m);
            _bookingRepo.Setup(x => x.GetByIdWithDetailsAsync(1)).ReturnsAsync(booking);
            _driverRepo.Setup(x => x.GetByUserIdAsync(booking.DriverUserId, true))
                .ReturnsAsync(new Driver { UserId = booking.DriverUserId, LoyaltyPoints = 10m });

            await CreateService().RejectBookingAsync(99, 1, Dto());

            // LoyaltyTransaction Refund phải được thêm
            _loyaltyRepo.Verify(x => x.Add(It.Is<LoyaltyTransaction>(
                t => t.Type == "Refund" && t.Points == 50m)), Times.Once);
        }

        // TC05: Booking có extra → phải hoàn stock
        [Fact]
        public async Task TC05_WithExtraService_ShouldRestoreStock()
        {
            var booking = MakeWaitingBooking();
            booking.BookingExtraServices = new List<BookingExtraService>
            {
                new BookingExtraService { ServiceId = 7, Quantity = 3 }
            };
            _bookingRepo.Setup(x => x.GetByIdWithDetailsAsync(1)).ReturnsAsync(booking);

            var svc = new ExtraService { Id = 7, TotalStock = 5 };
            _extraRepo.Setup(x => x.GetByIdAsync(7)).ReturnsAsync(svc);

            await CreateService().RejectBookingAsync(99, 1, Dto());

            // Stock phải được hoàn: 5 + 3 = 8
            Assert.Equal(8, svc.TotalStock);
        }

        // TC06: Happy path
        [Fact]
        public async Task TC06_HappyPath_ShouldSetRejectedAndNotify()
        {
            const string reason = "Slot bảo trì khẩn cấp";
            var booking = MakeWaitingBooking(driverId: 25);
            _bookingRepo.Setup(x => x.GetByIdWithDetailsAsync(1)).ReturnsAsync(booking);

            var result = await CreateService().RejectBookingAsync(99, 1, Dto(reason));

            Assert.Equal("Rejected", result.Status);
            Assert.Equal(reason, result.RejectionReason);
            _bookingRepo.Verify(x => x.Update(booking), Times.Once);
            _notiMock.Verify(x => x.SendAsync(
                25, It.IsAny<string>(), It.Is<string>(m => m.Contains(reason)),
                NotificationType.Booking), Times.Once);
        }
    }
}
