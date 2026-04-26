using ChargeSlot.Api.Enums;
using ChargeSlot.Api.Models;
using Moq;

namespace ChargeSlot.Tests.Services.PaymentServiceTests
{
    public class CreateSePayQrUrlTests : PaymentServiceTestBase
    {
        private const int DriverUserId = 5;
        private const int WrongDriver  = 99;
        private const int BookingId    = 1;

        // TC01 — Booking không tồn tại
        [Fact]
        public async Task CreateQr_BookingNotFound_ShouldThrow()
        {
            // bookingRepo default → null

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().CreateSePayQrUrlAsync(BookingId, DriverUserId));

            Assert.Contains("Booking", ex.Message);
        }

        // TC02 — Sai driver
        [Fact]
        public async Task CreateQr_WrongDriver_ShouldThrow()
        {
            var booking = CreatePendingBooking(driverUserId: DriverUserId);
            _bookingRepoMock.Setup(x => x.GetByIdWithDetailsAsync(BookingId)).ReturnsAsync(booking);

            var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                CreateService().CreateSePayQrUrlAsync(BookingId, WrongDriver));

            Assert.Contains("quyền", ex.Message);
        }

        // TC03 — Booking không ở PendingPayment
        [Fact]
        public async Task CreateQr_WrongStatus_ShouldThrow()
        {
            var booking = CreatePendingBooking(driverUserId: DriverUserId);
            booking.Status = BookingStatus.Paid;

            _bookingRepoMock.Setup(x => x.GetByIdWithDetailsAsync(BookingId)).ReturnsAsync(booking);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().CreateSePayQrUrlAsync(BookingId, DriverUserId));

            Assert.Contains("chờ thanh toán", ex.Message);
        }

        // TC04 — Hết hạn thanh toán
        [Fact]
        public async Task CreateQr_PaymentExpired_ShouldThrow()
        {
            var booking = CreatePendingBooking(driverUserId: DriverUserId);
            booking.PaymentExpiresAt = DateTime.Now.AddMinutes(-10);

            _bookingRepoMock.Setup(x => x.GetByIdWithDetailsAsync(BookingId)).ReturnsAsync(booking);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().CreateSePayQrUrlAsync(BookingId, DriverUserId));

            Assert.Contains("hết thời gian", ex.Message);
        }

        // TC05 — Happy path: booking 200k, chưa có Payment record → tạo mới Payment
        // QR URL phải chứa: bankCode=MB, accountNumber=1234567890, amount=200000, addInfo=CS1
        [Fact]
        public async Task CreateQr_Success_ShouldReturnCorrectUrl_AndCreatePayment()
        {
            var booking = CreatePendingBooking(driverUserId: DriverUserId, amount: 200_000m);
            _bookingRepoMock.Setup(x => x.GetByIdWithDetailsAsync(BookingId)).ReturnsAsync(booking);
            // paymentRepo → null (chưa có payment)

            var url = await CreateService().CreateSePayQrUrlAsync(BookingId, DriverUserId);

            // URL phải đúng format VietQR
            Assert.Contains("https://img.vietqr.io/image/MB-1234567890-compact.png", url);
            Assert.Contains("amount=200000", url);
            Assert.Contains("addInfo=CS1", url);

            // Payment record được tạo mới
            _paymentRepoMock.Verify(x => x.Add(It.Is<Payment>(p =>
                p.BookingId == BookingId &&
                p.Amount == 200_000m &&
                p.PaymentMethod == PaymentMethod.BankTransfer &&
                p.Status == PaymentStatus.Pending)), Times.Once);

            _uowMock.Verify(x => x.CompleteAsync(), Times.Once);
        }

        // TC06 — Payment đã tồn tại → không tạo lại, vẫn trả URL đúng
        [Fact]
        public async Task CreateQr_PaymentAlreadyExists_ShouldNotCreateNewPayment()
        {
            var booking = CreatePendingBooking(driverUserId: DriverUserId, amount: 150_000m);
            var existingPayment = new Payment
            {
                Id = 10, BookingId = BookingId, Amount = 150_000m,
                Status = PaymentStatus.Pending, PaymentMethod = PaymentMethod.BankTransfer
            };

            _bookingRepoMock.Setup(x => x.GetByIdWithDetailsAsync(BookingId)).ReturnsAsync(booking);
            _paymentRepoMock.Setup(x => x.GetByBookingIdAsync(BookingId)).ReturnsAsync(existingPayment);

            var url = await CreateService().CreateSePayQrUrlAsync(BookingId, DriverUserId);

            Assert.Contains("amount=150000", url);
            Assert.Contains("addInfo=CS1", url);

            // Không tạo Payment mới
            _paymentRepoMock.Verify(x => x.Add(It.IsAny<Payment>()), Times.Never);
        }
    }
}
