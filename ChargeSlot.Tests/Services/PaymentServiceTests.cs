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
        private readonly Mock<IBookingRepository> _bookingRepoMock = new();
        private readonly Mock<IPaymentRepository> _paymentRepoMock = new();
        private readonly Mock<IChargingSlotRepository> _slotRepoMock = new();
        private readonly Mock<ISePayService> _sePayMock = new();
        private readonly Mock<INotificationService> _notiMock = new();

        private readonly PaymentService _service;

        public PaymentServiceTests()
        {
            _service = new PaymentService(
                _bookingRepoMock.Object,
                _paymentRepoMock.Object,
                _slotRepoMock.Object,
                _sePayMock.Object,
                _notiMock.Object);
        }

        // =========================
        // CREATE PAYMENT LINK
        // =========================

        /// <summary>
        /// ✅ Happy case: tạo payment link thành công
        /// </summary>
        [Fact]
        public async Task CreatePaymentLink_ShouldSuccess()
        {
            var booking = new Booking
            {
                Id = 1,
                DriverUserId = 10,
                Status = BookingStatus.PendingPayment,
                TotalAmount = 200,
                PaymentExpiresAt = DateTime.UtcNow.AddMinutes(10)
            };

            _bookingRepoMock.Setup(x => x.GetByIdWithDetailsAsync(1))
                .ReturnsAsync(booking);

            _paymentRepoMock.Setup(x => x.GetByBookingIdAsync(1))
                .ReturnsAsync((Payment?)null);

            _sePayMock.Setup(x => x.CreatePaymentLink(
                It.IsAny<int>(),
                It.IsAny<decimal>()))
                .Returns("https://sepay.test");

            var result = await _service.CreatePaymentLinkAsync(1, 10);

            Assert.Equal("https://sepay.test", result);

            _paymentRepoMock.Verify(x => x.CreateAsync(It.IsAny<Payment>()), Times.Once);
        }

        /// <summary>
        /// ❌ Fail: booking hết hạn thanh toán
        /// </summary>
        [Fact]
        public async Task CreatePaymentLink_ShouldFail_WhenExpired()
        {
            var booking = new Booking
            {
                Id = 1,
                DriverUserId = 10,
                Status = BookingStatus.PendingPayment,
                PaymentExpiresAt = DateTime.UtcNow.AddMinutes(-1)
            };

            _bookingRepoMock.Setup(x => x.GetByIdWithDetailsAsync(1))
                .ReturnsAsync(booking);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.CreatePaymentLinkAsync(1, 10));
        }

        /// <summary>
        /// ❌ Fail: user không phải owner của booking
        /// </summary>
        [Fact]
        public async Task CreatePaymentLink_ShouldFail_WhenWrongUser()
        {
            var booking = new Booking
            {
                Id = 1,
                DriverUserId = 99,
                Status = BookingStatus.PendingPayment
            };

            _bookingRepoMock.Setup(x => x.GetByIdWithDetailsAsync(1))
                .ReturnsAsync(booking);

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                _service.CreatePaymentLinkAsync(1, 10));
        }

        /// <summary>
        /// ❌ BUG TEST: tạo link 2 lần → không được tạo payment trùng
        /// </summary>
        [Fact]
        public async Task CreatePaymentLink_ShouldFail_WhenPaymentAlreadyExists()
        {
            var booking = new Booking
            {
                Id = 1,
                DriverUserId = 10,
                Status = BookingStatus.PendingPayment
            };

            _bookingRepoMock.Setup(x => x.GetByIdWithDetailsAsync(1))
                .ReturnsAsync(booking);

            _paymentRepoMock.Setup(x => x.GetByBookingIdAsync(1))
                .ReturnsAsync(new Payment()); // đã tồn tại

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.CreatePaymentLinkAsync(1, 10));
        }

        // =========================
        // CALLBACK / WEBHOOK
        // =========================

        /// <summary>
        /// ❌ Fail: callback signature sai (bị fake)
        /// </summary>
        [Fact]
        public async Task ProcessCallback_ShouldFail_WhenInvalidSignature()
        {
            _sePayMock.Setup(x => x.ValidateCallback(It.IsAny<IQueryCollection>()))
                .Returns((false, 0, ""));

            var result = await _service.ProcessSePayCallbackAsync(new QueryCollection());

            Assert.False(result);
        }

        /// <summary>
        /// ❌ Fail: payment không tồn tại
        /// </summary>
        [Fact]
        public async Task ProcessCallback_ShouldFail_WhenPaymentNotFound()
        {
            _sePayMock.Setup(x => x.ValidateCallback(It.IsAny<IQueryCollection>()))
                .Returns((true, 100, "1"));

            _paymentRepoMock.Setup(x => x.GetByBookingIdAsync(1))
                .ReturnsAsync((Payment?)null);

            var result = await _service.ProcessSePayCallbackAsync(new QueryCollection());

            Assert.False(result);
        }

        /// <summary>
        /// ❌ BUG TEST: callback bị gọi 2 lần (idempotent)
        /// </summary>
        [Fact]
        public async Task ProcessCallback_ShouldIgnore_WhenAlreadyPaid()
        {
            var booking = new Booking { Id = 1, Status = BookingStatus.Paid };

            var payment = new Payment
            {
                BookingId = 1,
                Status = PaymentStatus.Completed
            };

            _sePayMock.Setup(x => x.ValidateCallback(It.IsAny<IQueryCollection>()))
                .Returns((true, 100, "1"));

            _bookingRepoMock.Setup(x => x.GetByIdWithDetailsAsync(1))
                .ReturnsAsync(booking);

            _paymentRepoMock.Setup(x => x.GetByBookingIdAsync(1))
                .ReturnsAsync(payment);

            var result = await _service.ProcessSePayCallbackAsync(new QueryCollection());

            Assert.True(result);

            // ❗ không update lại
            _paymentRepoMock.Verify(x => x.UpdateAsync(It.IsAny<Payment>()), Times.Never);
        }

        /// <summary>
        /// ✅ Happy case: thanh toán thành công
        /// </summary>
        [Fact]
        public async Task ProcessCallback_ShouldSuccess_WhenPaid()
        {
            var booking = new Booking
            {
                Id = 1,
                SlotId = 5,
                DriverUserId = 10,
                Status = BookingStatus.PendingPayment
            };

            var payment = new Payment
            {
                BookingId = 1,
                Status = PaymentStatus.Pending
            };

            var slot = new ChargingSlot { Id = 5 };

            _sePayMock.Setup(x => x.ValidateCallback(It.IsAny<IQueryCollection>()))
                .Returns((true, 200, "1"));

            _bookingRepoMock.Setup(x => x.GetByIdWithDetailsAsync(1))
                .ReturnsAsync(booking);

            _paymentRepoMock.Setup(x => x.GetByBookingIdAsync(1))
                .ReturnsAsync(payment);

            _slotRepoMock.Setup(x => x.GetByIdAsync(5, true))
                .ReturnsAsync(slot);

            // giả lập overlap booking
            _bookingRepoMock.Setup(x => x.GetOverlappingBookingsAsync(
                It.IsAny<int>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>()))
                .ReturnsAsync(new List<Booking>
                {
                    new Booking { Id = 2, Status = BookingStatus.Draft },
                    new Booking { Id = 3, Status = BookingStatus.PendingPayment }
                });

            var result = await _service.ProcessSePayCallbackAsync(new QueryCollection());

            Assert.True(result);

            Assert.Equal(PaymentStatus.Completed, payment.Status);
            Assert.Equal(BookingStatus.Paid, booking.Status);

            // ❗ đảm bảo reject booking khác
            _bookingRepoMock.Verify(x => x.UpdateAsync(
                It.Is<Booking>(b => b.Status == BookingStatus.Rejected)),
                Times.AtLeastOnce);

            // notify driver
            _notiMock.Verify(x => x.SendAsync(
                10,
                It.IsAny<string>(),
                It.IsAny<string>(),
                NotificationType.Payment), Times.Once);
        }

        /// <summary>
        /// ❌ Fail: thanh toán thất bại
        /// </summary>
        [Fact]
        public async Task ProcessCallback_ShouldFail_WhenPaymentFailed()
        {
            var payment = new Payment
            {
                BookingId = 1,
                Status = PaymentStatus.Pending
            };

            _sePayMock.Setup(x => x.ValidateCallback(It.IsAny<IQueryCollection>()))
                .Returns((true, 500, "1"));

            _paymentRepoMock.Setup(x => x.GetByBookingIdAsync(1))
                .ReturnsAsync(payment);

            var result = await _service.ProcessSePayCallbackAsync(new QueryCollection());

            Assert.False(result);

            Assert.Equal(PaymentStatus.Failed, payment.Status);
        }
    }
}