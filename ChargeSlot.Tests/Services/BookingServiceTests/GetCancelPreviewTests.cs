using ChargeSlot.Api.Models;
using ChargeSlot.Api.Enums;
using Moq;

namespace ChargeSlot.Tests.Services.BookingServiceTests
{
    public class GetCancelPreviewTests : BookingServiceTestBase
    {
        private static Booking MakeBooking(BookingStatus status, int driverId = 10,
            decimal amount = 200_000m, DateTime? ref100 = null, DateTime? ref50 = null)
        {
            var start = DateTime.UtcNow.AddHours(7).AddHours(5);
            return new Booking
            {
                Id                  = 1,
                DriverUserId        = driverId,
                Status              = status,
                TotalAmount         = amount,
                StartTime           = start,
                Refund100DeadlineAt = ref100,
                Refund50DeadlineAt  = ref50
            };
        }

        // Helper: setup booking repo trả Booking? (explicit cast để Moq resolve đúng)
        private void SetupBooking(Booking? booking) =>
            _bookingRepo.Setup(x => x.GetByIdWithDetailsAsync(It.IsAny<int>()))
                .ReturnsAsync(booking);

        // TC01: Booking không tồn tại
        [Fact]
        public async Task TC01_BookingNotFound_ShouldThrow()
        {
            // base default đã setup GetByIdWithDetailsAsync → null
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().GetCancelPreviewAsync(10, 999));
        }

        // TC02: Sai driver
        [Fact]
        public async Task TC02_WrongDriver_ShouldThrowUnauthorized()
        {
            SetupBooking(MakeBooking(BookingStatus.Paid, driverId: 99));

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                CreateService().GetCancelPreviewAsync(driverUserId: 10, bookingId: 1));
        }

        // TC03: Status = WaitingOwner → miễn phí (chưa thanh toán)
        [Fact]
        public async Task TC03_WaitingOwner_FreeCancel_RefundAmount0()
        {
            SetupBooking(MakeBooking(BookingStatus.WaitingOwner));

            var result = await CreateService().GetCancelPreviewAsync(10, 1);

            Assert.Equal(100, result.RefundPercent);
            Assert.Equal(0, result.RefundAmount); // chưa trả tiền
            Assert.Equal(0, result.PenaltyAmount);
        }

        // TC04: Status = PendingPayment → miễn phí
        [Fact]
        public async Task TC04_PendingPayment_FreeCancel_RefundAmount0()
        {
            SetupBooking(MakeBooking(BookingStatus.PendingPayment));

            var result = await CreateService().GetCancelPreviewAsync(10, 1);

            Assert.Equal(100, result.RefundPercent);
            Assert.Equal(0, result.RefundAmount);
        }

        // TC05: Paid, trước Refund100Deadline → hoàn 100%
        [Fact]
        public async Task TC05_Paid_Before100Deadline_ShouldReturn100Percent()
        {
            var vnNow  = DateTime.UtcNow.AddHours(7);
            var ref100 = vnNow.AddHours(3); // chưa qua
            var ref50  = vnNow.AddHours(4);
            SetupBooking(MakeBooking(BookingStatus.Paid, amount: 200_000m, ref100: ref100, ref50: ref50));

            var result = await CreateService().GetCancelPreviewAsync(10, 1);

            Assert.Equal(100, result.RefundPercent);
            Assert.Equal(200_000m, result.RefundAmount);
            Assert.Equal(0m, result.PenaltyAmount);
        }

        // TC06: Paid, giữa 2 deadline → hoàn 50%
        [Fact]
        public async Task TC06_Paid_Between100And50Deadline_ShouldReturn50Percent()
        {
            var vnNow  = DateTime.UtcNow.AddHours(7);
            var ref100 = vnNow.AddMinutes(-30); // đã qua
            var ref50  = vnNow.AddMinutes(30);  // chưa qua
            SetupBooking(MakeBooking(BookingStatus.Paid, amount: 200_000m, ref100: ref100, ref50: ref50));

            var result = await CreateService().GetCancelPreviewAsync(10, 1);

            Assert.Equal(50, result.RefundPercent);
            Assert.Equal(100_000m, result.RefundAmount);
            Assert.Equal(100_000m, result.PenaltyAmount);
        }

        // TC07: Paid, quá cả 2 deadline → refund 0%
        [Fact]
        public async Task TC07_Paid_AfterBothDeadlines_ShouldReturn0Percent()
        {
            var vnNow  = DateTime.UtcNow.AddHours(7);
            var ref100 = vnNow.AddMinutes(-60); // đã qua
            var ref50  = vnNow.AddMinutes(-10); // đã qua
            SetupBooking(MakeBooking(BookingStatus.Paid, amount: 200_000m, ref100: ref100, ref50: ref50));

            var result = await CreateService().GetCancelPreviewAsync(10, 1);

            Assert.Equal(0, result.RefundPercent);
            Assert.Equal(0m, result.RefundAmount);
            Assert.Equal(200_000m, result.PenaltyAmount);
        }

        // TC08: Status Completed/Cancelled/Rejected → throw
        [Theory]
        [InlineData(BookingStatus.Completed)]
        [InlineData(BookingStatus.Cancelled)]
        [InlineData(BookingStatus.Rejected)]
        public async Task TC08_TerminalStatus_ShouldThrow(BookingStatus status)
        {
            SetupBooking(MakeBooking(status));

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().GetCancelPreviewAsync(10, 1));
        }

        // TC09: Snapshot null → dùng fallback (-2h/-1h)
        [Fact]
        public async Task TC09_NullSnapshot_ShouldUseFallback()
        {
            // StartTime = 5h nữa → fallback ref100 = start-2h = 3h nữa (chưa qua) → 100%
            var start = DateTime.UtcNow.AddHours(7).AddHours(5);
            var booking = new Booking
            {
                Id                  = 1,
                DriverUserId        = 10,
                Status              = BookingStatus.Paid,
                TotalAmount         = 100_000m,
                StartTime           = start,
                Refund100DeadlineAt = null,  // fallback: StartTime - 2h
                Refund50DeadlineAt  = null   // fallback: StartTime - 1h
            };
            SetupBooking(booking);

            var result = await CreateService().GetCancelPreviewAsync(10, 1);

            // fallback ref100 = start - 2h = now + 3h > now → vẫn 100%
            Assert.Equal(100, result.RefundPercent);
            Assert.Equal(100_000m, result.RefundAmount);
        }

        // TC10: Boundary: now == Refund100DeadlineAt → phải trả 100%
        [Fact]
        public async Task TC10_ExactBoundary_EqualRef100Deadline_ShouldReturn100()
        {
            var vnNow  = DateTime.UtcNow.AddHours(7);
            var ref100 = vnNow.AddSeconds(5); // buffer nhỏ → now <= ref100
            var ref50  = vnNow.AddHours(1);
            SetupBooking(MakeBooking(BookingStatus.Paid, amount: 200_000m, ref100: ref100, ref50: ref50));

            var result = await CreateService().GetCancelPreviewAsync(10, 1);

            Assert.Equal(100, result.RefundPercent);
        }

        // TC11: Boundary: now == Refund50DeadlineAt → phải trả 50%
        [Fact]
        public async Task TC11_ExactBoundary_EqualRef50Deadline_ShouldReturn50()
        {
            var vnNow  = DateTime.UtcNow.AddHours(7);
            var ref100 = vnNow.AddMinutes(-30); // đã qua
            var ref50  = vnNow.AddSeconds(5);   // buffer nhỏ → now <= ref50
            SetupBooking(MakeBooking(BookingStatus.Paid, amount: 200_000m, ref100: ref100, ref50: ref50));

            var result = await CreateService().GetCancelPreviewAsync(10, 1);

            Assert.Equal(50, result.RefundPercent);
        }
    }
}
