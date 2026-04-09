using Xunit;
using Moq;
using ChargeSlot.Api.Services.Implementation;
using ChargeSlot.Api.Repositories.Interfaces;
using ChargeSlot.Api.Services.Interfaces;
using ChargeSlot.Api.Models;
using ChargeSlot.Api.Enums;
using ChargeSlot.Api.DTOs.Booking;

namespace ChargeSlot.Tests.Services
{
    /// <summary>
    /// Unit tests cho BookingService - luồng chính booking: tạo, duyệt, từ chối.
    /// Mỗi test verify đúng 1 behaviour / business rule.
    /// </summary>
    public class BookingServiceTests
    {
        private readonly Mock<IBookingRepository> _bookingRepoMock;
        private readonly Mock<IChargingSlotRepository> _slotRepoMock;
        private readonly Mock<INotificationService> _notiMock;

        private readonly BookingService _service;

        public BookingServiceTests()
        {
            _bookingRepoMock = new Mock<IBookingRepository>();
            _slotRepoMock   = new Mock<IChargingSlotRepository>();
            _notiMock       = new Mock<INotificationService>();

            _service = new BookingService(
                _bookingRepoMock.Object,
                _slotRepoMock.Object,
                _notiMock.Object);
        }

        // ─────────────────────────────────────────────
        // CREATE BOOKING – Validation Guards
        // ─────────────────────────────────────────────

        /// <summary>
        /// Slot không tồn tại → throw InvalidOperationException,
        /// không tạo booking và không gửi thông báo.
        /// </summary>
        [Fact]
        public async Task CreateBooking_ShouldFail_WhenSlotNotFound()
        {
            _slotRepoMock
                .Setup(x => x.GetByIdAsync(It.IsAny<int>(), It.IsAny<bool>()))
                .ReturnsAsync((ChargingSlot?)null);

            var dto = new CreateBookingDto
            {
                SlotId        = 999,
                StartTime     = DateTime.UtcNow.AddHours(1),
                DurationHours = 1
            };

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _service.CreateBookingAsync(1, dto));

            _bookingRepoMock.Verify(x => x.CreateAsync(It.IsAny<Booking>()), Times.Never);
        }

        /// <summary>
        /// Slot ở trạng thái Inactive (không phải Active) → throw InvalidOperationException.
        /// Không cho phép đặt slot đang tắt.
        /// </summary>
        [Theory]
        [InlineData(SlotStatus.Inactive)]
        [InlineData(SlotStatus.Booked)]
        public async Task CreateBooking_ShouldFail_WhenSlotNotActive(SlotStatus status)
        {
            _slotRepoMock
                .Setup(x => x.GetByIdAsync(It.IsAny<int>(), It.IsAny<bool>()))
                .ReturnsAsync(new ChargingSlot { Status = status });

            var dto = new CreateBookingDto
            {
                SlotId        = 1,
                StartTime     = DateTime.UtcNow.AddHours(1),
                DurationHours = 1
            };

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _service.CreateBookingAsync(1, dto));

            _bookingRepoMock.Verify(x => x.CreateAsync(It.IsAny<Booking>()), Times.Never);
        }

        /// <summary>
        /// Khi đã có booking khác chồng lấn cùng slot + khung giờ → throw InvalidOperationException.
        /// Business rule cốt lõi: chống double-booking.
        /// </summary>
        [Fact]
        public async Task CreateBooking_ShouldFail_WhenOverlappingBookingExists()
        {
            var slot = new ChargingSlot
            {
                Id                = 1,
                Status            = SlotStatus.Active,
                BasePricePerHour  = 100,
                ChargingStation   = new ChargingStation { OwnerUserId = 99 }
            };

            _slotRepoMock
                .Setup(x => x.GetByIdAsync(1, It.IsAny<bool>()))
                .ReturnsAsync(slot);

            _bookingRepoMock
                .Setup(x => x.HasOverlappingBookingAsync(
                    It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<int?>()))
                .ReturnsAsync(true);

            var dto = new CreateBookingDto
            {
                SlotId        = 1,
                StartTime     = DateTime.UtcNow.AddHours(1),
                DurationHours = 2
            };

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _service.CreateBookingAsync(10, dto));

            _bookingRepoMock.Verify(x => x.CreateAsync(It.IsAny<Booking>()), Times.Never);
        }

        // ─────────────────────────────────────────────
        // CREATE BOOKING – Happy Path
        // ─────────────────────────────────────────────

        /// <summary>
        /// Tất cả điều kiện hợp lệ → tạo booking thành công,
        /// status = WaitingOwner, gửi notification cho Owner.
        /// </summary>
        [Fact]
        public async Task CreateBooking_ShouldSuccess_AndNotifyOwner()
        {
            var slot = new ChargingSlot
            {
                Id               = 1,
                SlotName         = "Slot A",
                Status           = SlotStatus.Active,
                BasePricePerHour = 100,
                ChargingStation  = new ChargingStation { OwnerUserId = 99 }
            };

            _slotRepoMock
                .Setup(x => x.GetByIdAsync(1, It.IsAny<bool>()))
                .ReturnsAsync(slot);

            _bookingRepoMock
                .Setup(x => x.HasOverlappingBookingAsync(
                    It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<int?>()))
                .ReturnsAsync(false);

            _bookingRepoMock
                .Setup(x => x.CreateAsync(It.IsAny<Booking>()))
                .ReturnsAsync(new Booking { Id = 1 });

            _bookingRepoMock
                .Setup(x => x.GetByIdWithDetailsAsync(It.IsAny<int>()))
                .ReturnsAsync(new Booking
                {
                    Id           = 1,
                    DriverUserId = 10,
                    SlotId       = 1,
                    ChargingSlot = slot,
                    Status       = BookingStatus.WaitingOwner,
                    TotalAmount  = 200
                });

            var dto = new CreateBookingDto
            {
                SlotId        = 1,
                StartTime     = DateTime.UtcNow.AddHours(1),
                DurationHours = 2
            };

            var result = await _service.CreateBookingAsync(10, dto);

            Assert.NotNull(result);
            Assert.Equal(BookingStatus.WaitingOwner.ToString(), result.Status);

            // Đã gọi CreateAsync đúng 1 lần
            _bookingRepoMock.Verify(x => x.CreateAsync(It.IsAny<Booking>()), Times.Once);

            // Gửi notification tới Owner (userId = 99)
            _notiMock.Verify(x => x.SendAsync(
                99,
                It.IsAny<string>(),
                It.IsAny<string>(),
                NotificationType.Booking), Times.Once);
        }

        /// <summary>
        /// TotalAmount = BasePricePerHour × DurationHours.
        /// Bắt bug tính tiền sai.
        /// </summary>
        [Fact]
        public async Task CreateBooking_ShouldCalculateTotalAmount_Correctly()
        {
            const decimal pricePerHour = 150m;
            const decimal duration     = 3m;

            var slot = new ChargingSlot
            {
                Id               = 1,
                Status           = SlotStatus.Active,
                BasePricePerHour = pricePerHour
            };

            Booking? captured = null;

            _slotRepoMock
                .Setup(x => x.GetByIdAsync(It.IsAny<int>(), It.IsAny<bool>()))
                .ReturnsAsync(slot);

            _bookingRepoMock
                .Setup(x => x.HasOverlappingBookingAsync(
                    It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<int?>()))
                .ReturnsAsync(false);

            _bookingRepoMock
                .Setup(x => x.CreateAsync(It.IsAny<Booking>()))
                .Callback<Booking>(b => captured = b)
                .ReturnsAsync(new Booking());

            _bookingRepoMock
                .Setup(x => x.GetByIdWithDetailsAsync(It.IsAny<int>()))
                .ReturnsAsync(new Booking { ChargingSlot = slot });

            var dto = new CreateBookingDto
            {
                SlotId        = 1,
                StartTime     = DateTime.UtcNow.AddHours(1),
                DurationHours = duration
            };

            await _service.CreateBookingAsync(1, dto);

            Assert.NotNull(captured);
            Assert.Equal(pricePerHour * duration, captured!.TotalAmount);
        }

        /// <summary>
        /// EndTime = StartTime + DurationHours (kiểm tra tính đúng thời gian kết thúc).
        /// </summary>
        [Fact]
        public async Task CreateBooking_ShouldSetEndTime_Correctly()
        {
            var startTime = DateTime.UtcNow.AddHours(2);
            const decimal duration = 1.5m;

            var slot = new ChargingSlot
            {
                Id               = 1,
                Status           = SlotStatus.Active,
                BasePricePerHour = 100
            };

            Booking? captured = null;

            _slotRepoMock
                .Setup(x => x.GetByIdAsync(It.IsAny<int>(), It.IsAny<bool>()))
                .ReturnsAsync(slot);

            _bookingRepoMock
                .Setup(x => x.HasOverlappingBookingAsync(
                    It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<int?>()))
                .ReturnsAsync(false);

            _bookingRepoMock
                .Setup(x => x.CreateAsync(It.IsAny<Booking>()))
                .Callback<Booking>(b => captured = b)
                .ReturnsAsync(new Booking());

            _bookingRepoMock
                .Setup(x => x.GetByIdWithDetailsAsync(It.IsAny<int>()))
                .ReturnsAsync(new Booking { ChargingSlot = slot });

            var dto = new CreateBookingDto
            {
                SlotId        = 1,
                StartTime     = startTime,
                DurationHours = duration
            };

            await _service.CreateBookingAsync(1, dto);

            Assert.NotNull(captured);
            // EndTime phải bằng StartTime + DurationHours
            var expectedEnd = startTime.AddHours((double)duration);
            Assert.Equal(expectedEnd, captured!.EndTime);
        }

        /// <summary>
        /// Slot không có ChargingStation (station = null) →
        /// không gửi notification nhưng booking vẫn tạo thành công.
        /// </summary>
        [Fact]
        public async Task CreateBooking_ShouldSuccess_EvenWhenSlotHasNoStation()
        {
            var slot = new ChargingSlot
            {
                Id               = 1,
                Status           = SlotStatus.Active,
                BasePricePerHour = 50,
                ChargingStation  = null! // không có station
            };

            _slotRepoMock
                .Setup(x => x.GetByIdAsync(It.IsAny<int>(), It.IsAny<bool>()))
                .ReturnsAsync(slot);

            _bookingRepoMock
                .Setup(x => x.HasOverlappingBookingAsync(
                    It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<int?>()))
                .ReturnsAsync(false);

            _bookingRepoMock
                .Setup(x => x.CreateAsync(It.IsAny<Booking>()))
                .ReturnsAsync(new Booking());

            _bookingRepoMock
                .Setup(x => x.GetByIdWithDetailsAsync(It.IsAny<int>()))
                .ReturnsAsync(new Booking { ChargingSlot = slot });

            var dto = new CreateBookingDto
            {
                SlotId        = 1,
                StartTime     = DateTime.UtcNow.AddHours(1),
                DurationHours = 1
            };

            var result = await _service.CreateBookingAsync(10, dto);

            Assert.NotNull(result);

            // Không gửi notification vì không có station
            _notiMock.Verify(x => x.SendAsync(
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<NotificationType>()),
                Times.Never);
        }

        // ─────────────────────────────────────────────
        // ACCEPT BOOKING
        // ─────────────────────────────────────────────

        /// <summary>
        /// Owner hợp lệ accept booking đang WaitingOwner →
        /// booking chuyển sang PendingPayment, PaymentExpiresAt được set, notify driver.
        /// </summary>
        [Fact]
        public async Task AcceptBooking_ShouldSuccess_AndSetPendingPayment()
        {
            var booking = new Booking
            {
                Id           = 1,
                DriverUserId = 10,
                Status       = BookingStatus.WaitingOwner,
                StartTime    = DateTime.UtcNow.AddHours(1), // > 15 phút → deadline = Now + 15p
                ChargingSlot = new ChargingSlot
                {
                    ChargingStation = new ChargingStation { OwnerUserId = 99 }
                }
            };

            _bookingRepoMock
                .Setup(x => x.GetByIdWithDetailsAsync(1))
                .ReturnsAsync(booking);

            var result = await _service.AcceptBookingAsync(ownerUserId: 99, bookingId: 1);

            Assert.Equal(BookingStatus.PendingPayment.ToString(), result.Status);
            Assert.NotNull(result.PaymentExpiresAt);

            _bookingRepoMock.Verify(x => x.UpdateAsync(booking), Times.Once);

            // Notify driver (driverUserId = 10)
            _notiMock.Verify(x => x.SendAsync(
                10,
                It.IsAny<string>(),
                It.IsAny<string>(),
                NotificationType.Booking), Times.Once);
        }

        /// <summary>
        /// Khi StartTime < 15 phút nữa → PaymentExpiresAt = StartTime (không phải Now + 15p).
        /// </summary>
        [Fact]
        public async Task AcceptBooking_ShouldSetPaymentDeadline_ToStartTime_WhenStartingSoon()
        {
            var startTime = DateTime.UtcNow.AddMinutes(10); // < 15 phút

            var booking = new Booking
            {
                Id           = 1,
                DriverUserId = 10,
                Status       = BookingStatus.WaitingOwner,
                StartTime    = startTime,
                ChargingSlot = new ChargingSlot
                {
                    ChargingStation = new ChargingStation { OwnerUserId = 99 }
                }
            };

            _bookingRepoMock
                .Setup(x => x.GetByIdWithDetailsAsync(1))
                .ReturnsAsync(booking);

            var result = await _service.AcceptBookingAsync(99, 1);

            // PaymentExpiresAt phải bằng StartTime (không phải UtcNow + 15p)
            Assert.Equal(startTime, result.PaymentExpiresAt);
        }

        /// <summary>
        /// Khi StartTime > 15 phút → PaymentExpiresAt ≈ UtcNow + 15 phút.
        /// </summary>
        [Fact]
        public async Task AcceptBooking_ShouldSetPaymentDeadline_ToNowPlus15_WhenStartingLater()
        {
            var booking = new Booking
            {
                Id           = 1,
                DriverUserId = 10,
                Status       = BookingStatus.WaitingOwner,
                StartTime    = DateTime.UtcNow.AddHours(2), // > 15 phút
                ChargingSlot = new ChargingSlot
                {
                    ChargingStation = new ChargingStation { OwnerUserId = 99 }
                }
            };

            _bookingRepoMock
                .Setup(x => x.GetByIdWithDetailsAsync(1))
                .ReturnsAsync(booking);

            var before = DateTime.UtcNow;
            var result = await _service.AcceptBookingAsync(99, 1);
            var after  = DateTime.UtcNow;

            // PaymentExpiresAt phải nằm trong khoảng [before + 15p, after + 15p]
            Assert.True(result.PaymentExpiresAt >= before.AddMinutes(15));
            Assert.True(result.PaymentExpiresAt <= after.AddMinutes(15));
        }

        /// <summary>
        /// User không phải Owner của slot → throw UnauthorizedAccessException.
        /// </summary>
        [Fact]
        public async Task AcceptBooking_ShouldFail_WhenNotOwner()
        {
            var booking = new Booking
            {
                Status       = BookingStatus.WaitingOwner,
                ChargingSlot = new ChargingSlot
                {
                    ChargingStation = new ChargingStation { OwnerUserId = 99 }
                }
            };

            _bookingRepoMock
                .Setup(x => x.GetByIdWithDetailsAsync(It.IsAny<int>()))
                .ReturnsAsync(booking);

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                _service.AcceptBookingAsync(ownerUserId: 1 /* sai */, bookingId: 1));
        }

        /// <summary>
        /// Booking không ở WaitingOwner (ví dụ đã Completed) → throw InvalidOperationException.
        /// Không cho phép thay đổi trạng thái ngược.
        /// </summary>
        [Theory]
        [InlineData(BookingStatus.Completed)]
        [InlineData(BookingStatus.Paid)]
        [InlineData(BookingStatus.Rejected)]
        [InlineData(BookingStatus.PendingPayment)]
        public async Task AcceptBooking_ShouldFail_WhenStatusIsNotWaitingOwner(BookingStatus status)
        {
            var booking = new Booking
            {
                Status       = status,
                ChargingSlot = new ChargingSlot
                {
                    ChargingStation = new ChargingStation { OwnerUserId = 99 }
                }
            };

            _bookingRepoMock
                .Setup(x => x.GetByIdWithDetailsAsync(It.IsAny<int>()))
                .ReturnsAsync(booking);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.AcceptBookingAsync(99, 1));
        }

        /// <summary>
        /// Booking không tồn tại → throw InvalidOperationException.
        /// </summary>
        [Fact]
        public async Task AcceptBooking_ShouldFail_WhenBookingNotFound()
        {
            _bookingRepoMock
                .Setup(x => x.GetByIdWithDetailsAsync(It.IsAny<int>()))
                .ReturnsAsync((Booking?)null);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.AcceptBookingAsync(99, 999));
        }

        // ─────────────────────────────────────────────
        // REJECT BOOKING
        // ─────────────────────────────────────────────

        /// <summary>
        /// Owner hợp lệ reject booking WaitingOwner →
        /// booking chuyển sang Rejected, lưu lý do từ chối, notify driver.
        /// </summary>
        [Fact]
        public async Task RejectBooking_ShouldSuccess_AndSetRejectedStatus()
        {
            const string reason = "Slot bảo trì";

            var booking = new Booking
            {
                Id           = 1,
                DriverUserId = 10,
                Status       = BookingStatus.WaitingOwner,
                ChargingSlot = new ChargingSlot
                {
                    ChargingStation = new ChargingStation { OwnerUserId = 99 }
                }
            };

            _bookingRepoMock
                .Setup(x => x.GetByIdWithDetailsAsync(1))
                .ReturnsAsync(booking);

            var result = await _service.RejectBookingAsync(
                ownerUserId: 99,
                bookingId:   1,
                dto:         new RejectBookingDto { RejectionReason = reason });

            Assert.Equal(BookingStatus.Rejected.ToString(), result.Status);
            Assert.Equal(reason, result.RejectionReason);

            _bookingRepoMock.Verify(x => x.UpdateAsync(booking), Times.Once);

            // Notify driver
            _notiMock.Verify(x => x.SendAsync(
                10,
                It.IsAny<string>(),
                It.Is<string>(msg => msg.Contains(reason)),
                NotificationType.Booking), Times.Once);
        }

        /// <summary>
        /// User không phải Owner → throw UnauthorizedAccessException.
        /// </summary>
        [Fact]
        public async Task RejectBooking_ShouldFail_WhenNotOwner()
        {
            var booking = new Booking
            {
                Status       = BookingStatus.WaitingOwner,
                ChargingSlot = new ChargingSlot
                {
                    ChargingStation = new ChargingStation { OwnerUserId = 99 }
                }
            };

            _bookingRepoMock
                .Setup(x => x.GetByIdWithDetailsAsync(It.IsAny<int>()))
                .ReturnsAsync(booking);

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                _service.RejectBookingAsync(
                    ownerUserId: 1, // sai
                    bookingId:   1,
                    dto:         new RejectBookingDto { RejectionReason = "x" }));
        }

        /// <summary>
        /// Booking không ở WaitingOwner → throw InvalidOperationException.
        /// Không thể reject booking đã được xử lý.
        /// </summary>
        [Theory]
        [InlineData(BookingStatus.PendingPayment)]
        [InlineData(BookingStatus.Paid)]
        [InlineData(BookingStatus.Completed)]
        public async Task RejectBooking_ShouldFail_WhenStatusIsNotWaitingOwner(BookingStatus status)
        {
            var booking = new Booking
            {
                Status       = status,
                ChargingSlot = new ChargingSlot
                {
                    ChargingStation = new ChargingStation { OwnerUserId = 99 }
                }
            };

            _bookingRepoMock
                .Setup(x => x.GetByIdWithDetailsAsync(It.IsAny<int>()))
                .ReturnsAsync(booking);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.RejectBookingAsync(99, 1, new RejectBookingDto { RejectionReason = "x" }));
        }

        /// <summary>
        /// Booking không tồn tại → throw InvalidOperationException.
        /// </summary>
        [Fact]
        public async Task RejectBooking_ShouldFail_WhenBookingNotFound()
        {
            _bookingRepoMock
                .Setup(x => x.GetByIdWithDetailsAsync(It.IsAny<int>()))
                .ReturnsAsync((Booking?)null);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.RejectBookingAsync(99, 999, new RejectBookingDto { RejectionReason = "x" }));
        }

        // ─────────────────────────────────────────────
        // GET BOOKING
        // ─────────────────────────────────────────────

        /// <summary>
        /// GetByIdAsync trả về null khi booking không tồn tại.
        /// </summary>
        [Fact]
        public async Task GetById_ShouldReturnNull_WhenNotFound()
        {
            _bookingRepoMock
                .Setup(x => x.GetByIdWithDetailsAsync(It.IsAny<int>()))
                .ReturnsAsync((Booking?)null);

            var result = await _service.GetByIdAsync(999);

            Assert.Null(result);
        }

        /// <summary>
        /// GetByIdAsync trả về BookingDto đúng khi tìm thấy.
        /// </summary>
        [Fact]
        public async Task GetById_ShouldReturnDto_WhenFound()
        {
            var slot = new ChargingSlot
            {
                Id       = 1,
                SlotName = "Slot A",
                ChargingStation = new ChargingStation { Id = 5, Name = "Station X" }
            };

            var booking = new Booking
            {
                Id           = 7,
                DriverUserId = 10,
                SlotId       = 1,
                ChargingSlot = slot,
                Status       = BookingStatus.Paid,
                TotalAmount  = 300
            };

            _bookingRepoMock
                .Setup(x => x.GetByIdWithDetailsAsync(7))
                .ReturnsAsync(booking);

            var result = await _service.GetByIdAsync(7);

            Assert.NotNull(result);
            Assert.Equal(7, result!.Id);
            Assert.Equal(BookingStatus.Paid.ToString(), result.Status);
            Assert.Equal(300, result.TotalAmount);
        }

        /// <summary>
        /// GetByDriverAsync trả về danh sách đúng theo driverUserId.
        /// </summary>
        [Fact]
        public async Task GetByDriver_ShouldReturnMappedList()
        {
            var slot = new ChargingSlot { SlotName = "S1", ChargingStation = new ChargingStation() };

            var bookings = new List<Booking>
            {
                new Booking { Id = 1, DriverUserId = 10, ChargingSlot = slot },
                new Booking { Id = 2, DriverUserId = 10, ChargingSlot = slot }
            };

            _bookingRepoMock
                .Setup(x => x.GetByDriverAsync(10))
                .ReturnsAsync(bookings);

            var result = await _service.GetByDriverAsync(10);

            Assert.Equal(2, result.Count);
            Assert.All(result, dto => Assert.NotNull(dto));
        }

        /// <summary>
        /// GetByOwnerAsync trả về danh sách đúng theo ownerUserId.
        /// </summary>
        [Fact]
        public async Task GetByOwner_ShouldReturnMappedList()
        {
            var slot = new ChargingSlot { SlotName = "S2", ChargingStation = new ChargingStation() };

            var bookings = new List<Booking>
            {
                new Booking { Id = 3, DriverUserId = 20, ChargingSlot = slot },
            };

            _bookingRepoMock
                .Setup(x => x.GetByOwnerAsync(99))
                .ReturnsAsync(bookings);

            var result = await _service.GetByOwnerAsync(99);

            Assert.Single(result);
        }
    }
}