using Xunit;
using Moq;
using ChargeSlot.Api.Services.Implementation;
using ChargeSlot.Api.Repositories.Interfaces;
using ChargeSlot.Api.Services.Interfaces;
using ChargeSlot.Api.Models;
using ChargeSlot.Api.Enums;
using Microsoft.AspNetCore.Http;

namespace ChargeSlot.Tests.Services
{
    public class PaymentServiceTests
    {
        private readonly Mock<IBookingRepository>     _bookingRepoMock = new();
        private readonly Mock<IPaymentRepository>     _paymentRepoMock = new();
        private readonly Mock<IChargingSlotRepository>_slotRepoMock    = new();
        private readonly Mock<IVnPayService>          _vnPayMock       = new();
        private readonly Mock<INotificationService>   _notiMock        = new();

        private readonly PaymentService _service;

        public PaymentServiceTests()
        {
            _service = new PaymentService(
                _bookingRepoMock.Object,
                _paymentRepoMock.Object,
                _slotRepoMock.Object,
                _vnPayMock.Object,
                _notiMock.Object);
        }

        /// Happy path: booking hợp lệ, chưa có Payment record →
        /// tạo Payment record mới + trả về URL từ VNPay.
        [Fact]
        public async Task CreatePaymentUrl_ShouldSuccess_AndCreatePaymentRecord()
        {
            var booking = new Booking
            {
                Id               = 1,
                DriverUserId     = 10,
                Status           = BookingStatus.PendingPayment,
                TotalAmount      = 300,
                PaymentExpiresAt = DateTime.UtcNow.AddMinutes(10)
            };

            _bookingRepoMock
                .Setup(x => x.GetByIdWithDetailsAsync(1))
                .ReturnsAsync(booking);

            _paymentRepoMock
                .Setup(x => x.GetByBookingIdAsync(1))
                .ReturnsAsync((Payment?)null); // chưa có Payment

            _vnPayMock
                .Setup(x => x.CreatePaymentUrl(
                    It.IsAny<int>(),
                    It.IsAny<decimal>(),
                    It.IsAny<string>(),
                    It.IsAny<HttpContext>()))
                .Returns("https://sandbox.vnpayment.vn/pay");

            var mockHttpContext = new DefaultHttpContext();

            var result = await _service.CreatePaymentUrlAsync(
                bookingId:     1,
                driverUserId:  10,
                context:       mockHttpContext);

            Assert.Equal("https://sandbox.vnpayment.vn/pay", result);

            // Phải tạo Payment record mới
            _paymentRepoMock.Verify(x => x.CreateAsync(It.Is<Payment>(p =>
                p.BookingId     == 1 &&
                p.Amount        == 300 &&
                p.Status        == PaymentStatus.Pending
            )), Times.Once);
        }

        /// Đã có Payment record → không tạo record mới, vẫn trả về URL.
        /// Idempotent: driver có thể request lại URL nếu chưa thanh toán.
        [Fact]
        public async Task CreatePaymentUrl_ShouldNotCreateDuplicate_WhenPaymentExists()
        {
            var booking = new Booking
            {
                Id               = 1,
                DriverUserId     = 10,
                Status           = BookingStatus.PendingPayment,
                TotalAmount      = 200,
                PaymentExpiresAt = DateTime.UtcNow.AddMinutes(5)
            };

            _bookingRepoMock
                .Setup(x => x.GetByIdWithDetailsAsync(1))
                .ReturnsAsync(booking);

            _paymentRepoMock
                .Setup(x => x.GetByBookingIdAsync(1))
                .ReturnsAsync(new Payment { BookingId = 1, Status = PaymentStatus.Pending });

            _vnPayMock
                .Setup(x => x.CreatePaymentUrl(
                    It.IsAny<int>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<HttpContext>()))
                .Returns("https://sandbox.vnpayment.vn/pay");

            var result = await _service.CreatePaymentUrlAsync(1, 10, new DefaultHttpContext());

            Assert.NotNull(result);

            // Không tạo record mới
            _paymentRepoMock.Verify(x => x.CreateAsync(It.IsAny<Payment>()), Times.Never);
        }

        /// Booking không tồn tại → throw InvalidOperationException.
        [Fact]
        public async Task CreatePaymentUrl_ShouldFail_WhenBookingNotFound()
        {
            _bookingRepoMock
                .Setup(x => x.GetByIdWithDetailsAsync(It.IsAny<int>()))
                .ReturnsAsync((Booking?)null);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.CreatePaymentUrlAsync(999, 10, new DefaultHttpContext()));
        }

        /// Driver không phải chủ booking → throw UnauthorizedAccessException.
        /// Chống trường hợp driver A tạo link thanh toán cho booking của driver B.
        [Fact]
        public async Task CreatePaymentUrl_ShouldFail_WhenDriverNotOwnerOfBooking()
        {
            var booking = new Booking
            {
                Id           = 1,
                DriverUserId = 99, // booking của driver 99
                Status       = BookingStatus.PendingPayment
            };

            _bookingRepoMock
                .Setup(x => x.GetByIdWithDetailsAsync(1))
                .ReturnsAsync(booking);

            // Driver 10 cố tạo URL thanh toán → unauthorized
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                _service.CreatePaymentUrlAsync(1, 10, new DefaultHttpContext()));
        }

        ///Booking không ở trạng thái PendingPayment →
        /// throw InvalidOperationException.
        [Theory]
        [InlineData(BookingStatus.WaitingOwner)]
        [InlineData(BookingStatus.Paid)]
        [InlineData(BookingStatus.Completed)]
        [InlineData(BookingStatus.Rejected)]
        public async Task CreatePaymentUrl_ShouldFail_WhenStatusIsNotPendingPayment(BookingStatus status)
        {
            var booking = new Booking
            {
                Id           = 1,
                DriverUserId = 10,
                Status       = status
            };

            _bookingRepoMock
                .Setup(x => x.GetByIdWithDetailsAsync(1))
                .ReturnsAsync(booking);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.CreatePaymentUrlAsync(1, 10, new DefaultHttpContext()));
        }

        /// Đã hết hạn thanh toán (PaymentExpiresAt < Now) →
        /// throw InvalidOperationException.
        [Fact]
        public async Task CreatePaymentUrl_ShouldFail_WhenPaymentExpired()
        {
            var booking = new Booking
            {
                Id               = 1,
                DriverUserId     = 10,
                Status           = BookingStatus.PendingPayment,
                PaymentExpiresAt = DateTime.UtcNow.AddMinutes(-5) // đã hết hạn
            };

            _bookingRepoMock
                .Setup(x => x.GetByIdWithDetailsAsync(1))
                .ReturnsAsync(booking);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.CreatePaymentUrlAsync(1, 10, new DefaultHttpContext()));
        }

        /// VNPay callback với chữ ký không hợp lệ → trả về false, không xử lý gì.
        /// Bảo vệ chống tấn công giả mạo callback.
        [Fact]
        public async Task ProcessCallback_ShouldReturnFalse_WhenSignatureInvalid()
        {
            _vnPayMock
                .Setup(x => x.ValidateCallback(It.IsAny<IQueryCollection>()))
                .Returns((false, "97", "1_123456")); // invalid signature

            var result = await _service.ProcessVnPayCallbackAsync(new QueryCollection());

            Assert.False(result);

            // Không thay đổi bất kỳ dữ liệu nào
            _bookingRepoMock.Verify(x => x.UpdateAsync(It.IsAny<Booking>()), Times.Never);
            _paymentRepoMock.Verify(x => x.UpdateAsync(It.IsAny<Payment>()), Times.Never);
        }

        /// txnRef không parse được thành bookingId → trả về false.
        [Fact]
        public async Task ProcessCallback_ShouldReturnFalse_WhenTxnRefInvalid()
        {
            _vnPayMock
                .Setup(x => x.ValidateCallback(It.IsAny<IQueryCollection>()))
                .Returns((true, "00", "ABC_xyz")); // không phải số

            var result = await _service.ProcessVnPayCallbackAsync(new QueryCollection());

            Assert.False(result);
        }

        /// Booking không tồn tại trong DB → trả về false.
        [Fact]
        public async Task ProcessCallback_ShouldReturnFalse_WhenBookingNotFound()
        {
            _vnPayMock
                .Setup(x => x.ValidateCallback(It.IsAny<IQueryCollection>()))
                .Returns((true, "00", "1_999999"));

            _bookingRepoMock
                .Setup(x => x.GetByIdWithDetailsAsync(1))
                .ReturnsAsync((Booking?)null);

            var result = await _service.ProcessVnPayCallbackAsync(new QueryCollection());

            Assert.False(result);
        }

        /// Payment record không tồn tại → trả về false.
        [Fact]
        public async Task ProcessCallback_ShouldReturnFalse_WhenPaymentNotFound()
        {
            _vnPayMock
                .Setup(x => x.ValidateCallback(It.IsAny<IQueryCollection>()))
                .Returns((true, "00", "1_999"));

            _bookingRepoMock
                .Setup(x => x.GetByIdWithDetailsAsync(1))
                .ReturnsAsync(new Booking { Id = 1, Status = BookingStatus.PendingPayment });

            _paymentRepoMock
                .Setup(x => x.GetByBookingIdAsync(1))
                .ReturnsAsync((Payment?)null);

            var result = await _service.ProcessVnPayCallbackAsync(new QueryCollection());

            Assert.False(result);
        }

        /// Idempotent: callback gọi 2 lần cho cùng 1 payment đã Completed →
        /// trả về true nhưng KHÔNG update lại.
        [Fact]
        public async Task ProcessCallback_ShouldIgnore_WhenPaymentAlreadyProcessed()
        {
            _vnPayMock
                .Setup(x => x.ValidateCallback(It.IsAny<IQueryCollection>()))
                .Returns((true, "00", "1_999"));

            _bookingRepoMock
                .Setup(x => x.GetByIdWithDetailsAsync(1))
                .ReturnsAsync(new Booking { Id = 1 });

            _paymentRepoMock
                .Setup(x => x.GetByBookingIdAsync(1))
                .ReturnsAsync(new Payment
                {
                    BookingId = 1,
                    Status    = PaymentStatus.Completed // đã xử lý
                });

            var result = await _service.ProcessVnPayCallbackAsync(new QueryCollection());

            Assert.True(result); // trả về true (đã xử lý)

            // KHÔNG update lại
            _paymentRepoMock.Verify(x => x.UpdateAsync(It.IsAny<Payment>()), Times.Never);
            _bookingRepoMock.Verify(x => x.UpdateAsync(It.IsAny<Booking>()), Times.Never);
        }

        ///  Thanh toán thành công (responseCode = "00") →
        ///   - Payment.Status = Completed
        ///   - Booking.Status = Paid
        ///   - Slot.Status    = Booked
        ///   - Gửi notification cho Driver
        [Fact]
        public async Task ProcessCallback_ShouldSuccess_WhenPaymentSucceeded()
        {
            var booking = new Booking
            {
                Id           = 1,
                SlotId       = 5,
                DriverUserId = 10,
                Status       = BookingStatus.PendingPayment
            };

            var payment = new Payment
            {
                BookingId = 1,
                Status    = PaymentStatus.Pending
            };

            var slot = new ChargingSlot { Id = 5, Status = SlotStatus.Active };

            _vnPayMock
                .Setup(x => x.ValidateCallback(It.IsAny<IQueryCollection>()))
                .Returns((true, "00", "1_123456789")); // thành công

            _bookingRepoMock
                .Setup(x => x.GetByIdWithDetailsAsync(1))
                .ReturnsAsync(booking);

            _paymentRepoMock
                .Setup(x => x.GetByBookingIdAsync(1))
                .ReturnsAsync(payment);

            _slotRepoMock
                .Setup(x => x.GetByIdAsync(5, true))
                .ReturnsAsync(slot);

            var result = await _service.ProcessVnPayCallbackAsync(new QueryCollection());

            Assert.True(result);

            // Payment cập nhật thành Completed
            Assert.Equal(PaymentStatus.Completed, payment.Status);
            Assert.NotNull(payment.PaidAt);
            Assert.Equal("1_123456789", payment.GatewayTxnRef);

            // Booking chuyển sang Paid
            Assert.Equal(BookingStatus.Paid, booking.Status);

            // Slot bị lock (Booked)
            Assert.Equal(SlotStatus.Booked, slot.Status);

            // Gọi update đúng
            _paymentRepoMock.Verify(x => x.UpdateAsync(payment), Times.Once);
            _bookingRepoMock.Verify(x => x.UpdateAsync(booking), Times.Once);
            _slotRepoMock.Verify(x => x.Update(slot), Times.Once);
            _slotRepoMock.Verify(x => x.SaveChangesAsync(), Times.Once);

            // Notify driver
            _notiMock.Verify(x => x.SendAsync(
                10,
                It.IsAny<string>(),
                It.IsAny<string>(),
                NotificationType.Payment), Times.Once);
        }

        /// Thanh toán thất bại (responseCode != "00") →
        ///   - Payment.Status = Failed
        ///   - Booking KHÔNG thay đổi status
        ///   - Trả về false
        [Fact]
        public async Task ProcessCallback_ShouldReturnFalse_AndMarkPaymentFailed_WhenPaymentFailed()
        {
            var booking = new Booking
            {
                Id     = 1,
                SlotId = 5,
                Status = BookingStatus.PendingPayment
            };

            var payment = new Payment
            {
                BookingId = 1,
                Status    = PaymentStatus.Pending
            };

            _vnPayMock
                .Setup(x => x.ValidateCallback(It.IsAny<IQueryCollection>()))
                .Returns((true, "24", "1_999")); // mã lỗi VNPay (user cancel)

            _bookingRepoMock
                .Setup(x => x.GetByIdWithDetailsAsync(1))
                .ReturnsAsync(booking);

            _paymentRepoMock
                .Setup(x => x.GetByBookingIdAsync(1))
                .ReturnsAsync(payment);

            var result = await _service.ProcessVnPayCallbackAsync(new QueryCollection());

            Assert.False(result);

            // Payment mark là Failed
            Assert.Equal(PaymentStatus.Failed, payment.Status);
            _paymentRepoMock.Verify(x => x.UpdateAsync(payment), Times.Once);

            // Booking KHÔNG thay đổi (chờ PaymentExpiryJob xử lý expire)
            _bookingRepoMock.Verify(x => x.UpdateAsync(It.IsAny<Booking>()), Times.Never);

            // Không notify
            _notiMock.Verify(x => x.SendAsync(
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<NotificationType>()),
                Times.Never);
        }

        /// Thanh toán thành công nhưng Slot không tồn tại →
        /// vẫn trả về true (slot có thể đã bị xóa).
        [Fact]
        public async Task ProcessCallback_ShouldStillSucceed_WhenSlotNotFound()
        {
            var booking = new Booking
            {
                Id           = 1,
                SlotId       = 99,
                DriverUserId = 10,
                Status       = BookingStatus.PendingPayment
            };

            var payment = new Payment
            {
                BookingId = 1,
                Status    = PaymentStatus.Pending
            };

            _vnPayMock
                .Setup(x => x.ValidateCallback(It.IsAny<IQueryCollection>()))
                .Returns((true, "00", "1_111"));

            _bookingRepoMock
                .Setup(x => x.GetByIdWithDetailsAsync(1))
                .ReturnsAsync(booking);

            _paymentRepoMock
                .Setup(x => x.GetByBookingIdAsync(1))
                .ReturnsAsync(payment);

            _slotRepoMock
                .Setup(x => x.GetByIdAsync(99, true))
                .ReturnsAsync((ChargingSlot?)null); // slot không tồn tại

            var result = await _service.ProcessVnPayCallbackAsync(new QueryCollection());

            Assert.True(result);
            Assert.Equal(PaymentStatus.Completed, payment.Status);
            Assert.Equal(BookingStatus.Paid, booking.Status);

            // Không cố update slot
            _slotRepoMock.Verify(x => x.Update(It.IsAny<ChargingSlot>()), Times.Never);
        }
    }
}