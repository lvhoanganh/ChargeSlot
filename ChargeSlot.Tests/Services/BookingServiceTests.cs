using Xunit;
using Moq;
using ChargeSlot.Api.Services.Implementation;
using ChargeSlot.Api.Repositories.Interfaces;
using ChargeSlot.Api.Services.Interfaces;
using ChargeSlot.Api.Models.Identity;
using ChargeSlot.Api.Models;
using ChargeSlot.Api.Enums;
using ChargeSlot.Api.DTOs.Booking;

namespace ChargeSlot.Tests.Services
{
    public class BookingServiceTests
    {
        private readonly Mock<IBookingRepository> _bookingRepoMock;
        private readonly Mock<IChargingSlotRepository> _slotRepoMock;
        private readonly Mock<INotificationService> _notiMock;

        private readonly BookingService _service;

        public BookingServiceTests()
        {
            _bookingRepoMock = new Mock<IBookingRepository>();
            _slotRepoMock = new Mock<IChargingSlotRepository>();
            _notiMock = new Mock<INotificationService>();

            _service = new BookingService(
                _bookingRepoMock.Object,
                _slotRepoMock.Object,
                _notiMock.Object);
        }

        // =========================
        // CREATE BOOKING - VALIDATION
        // =========================

        // Không cho phép đặt lịch trong quá khứ
        // Bắt bug: thiếu validate StartTime < Now
        [Fact]
        public async Task CreateBooking_ShouldFail_WhenStartTimeInPast()
        {
            var dto = new CreateBookingDto
            {
                SlotId = 1,
                StartTime = DateTime.UtcNow.AddHours(-1),
                DurationHours = 1
            };

            _slotRepoMock.Setup(x => x.GetByIdAsync(It.IsAny<int>(), It.IsAny<bool>()))
                .ReturnsAsync(new ChargingSlot { Status = SlotStatus.Active });

            _bookingRepoMock.Setup(x => x.HasOverlappingBookingAsync(
                It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<int?>()))
                .ReturnsAsync(false);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.CreateBookingAsync(1, dto));

            _bookingRepoMock.Verify(x => x.CreateAsync(It.IsAny<Booking>()), Times.Never);
        }

        // Rule: Duration phải > 0
        // Bắt bug: dev quên validate duration
        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public async Task CreateBooking_ShouldFail_WhenDurationInvalid(decimal duration)
        {
            var dto = new CreateBookingDto
            {
                SlotId = 1,
                StartTime = DateTime.UtcNow.AddHours(1),
                DurationHours = duration
            };

            _slotRepoMock.Setup(x => x.GetByIdAsync(It.IsAny<int>(), It.IsAny<bool>()))
                .ReturnsAsync(new ChargingSlot { Status = SlotStatus.Active });

            _bookingRepoMock.Setup(x => x.HasOverlappingBookingAsync(
                It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<int?>()))
                .ReturnsAsync(false);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.CreateBookingAsync(1, dto));
        }

        // Rule: Slot phải tồn tại
        // Tránh null reference + data rác
        [Fact]
        public async Task CreateBooking_ShouldFail_WhenSlotNotFound()
        {
            _slotRepoMock.Setup(x => x.GetByIdAsync(It.IsAny<int>(), It.IsAny<bool>()))
                .ReturnsAsync((ChargingSlot?)null);

            var dto = new CreateBookingDto
            {
                SlotId = 1,
                StartTime = DateTime.UtcNow.AddHours(1),
                DurationHours = 1
            };

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.CreateBookingAsync(1, dto));
        }

        // Rule: Slot phải Active mới được đặt
        // Tránh booking vào slot bị disable
        [Fact]
        public async Task CreateBooking_ShouldFail_WhenSlotInactive()
        {
            _slotRepoMock.Setup(x => x.GetByIdAsync(It.IsAny<int>(), It.IsAny<bool>()))
                .ReturnsAsync(new ChargingSlot { Status = SlotStatus.Inactive });

            var dto = new CreateBookingDto
            {
                SlotId = 1,
                StartTime = DateTime.UtcNow.AddHours(1),
                DurationHours = 1
            };

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.CreateBookingAsync(1, dto));
        }

        // Rule: Không được đặt lịch trùng thời gian
        // Core business: chống double booking
        [Fact]
        public async Task CreateBooking_ShouldFail_WhenOverlap()
        {
            _slotRepoMock.Setup(x => x.GetByIdAsync(It.IsAny<int>(), It.IsAny<bool>()))
                .ReturnsAsync(new ChargingSlot { Status = SlotStatus.Active });

            _bookingRepoMock.Setup(x => x.HasOverlappingBookingAsync(
                It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<int?>()))
                .ReturnsAsync(true);

            var dto = new CreateBookingDto
            {
                SlotId = 1,
                StartTime = DateTime.UtcNow.AddHours(1),
                DurationHours = 1
            };

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.CreateBookingAsync(1, dto));

            _bookingRepoMock.Verify(x => x.CreateAsync(It.IsAny<Booking>()), Times.Never);
        }

        // =========================
        // CREATE BOOKING - SUCCESS
        // =========================

        // Flow chuẩn:
        // - Slot hợp lệ
        // - Không overlap
        // - Duration hợp lệ
        // Expect: tạo booking + gửi notification cho owner
        [Fact]
        public async Task CreateBooking_ShouldSuccess_WhenValid()
        {
            var slot = new ChargingSlot
            {
                Id = 1,
                Status = SlotStatus.Active,
                BasePricePerHour = 100,
                ChargingStation = new ChargingStation { OwnerUserId = 99 }
            };

            _slotRepoMock.Setup(x => x.GetByIdAsync(It.IsAny<int>(), It.IsAny<bool>()))
                .ReturnsAsync(slot);

            _bookingRepoMock.Setup(x => x.HasOverlappingBookingAsync(
                It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<int?>()))
                .ReturnsAsync(false);

            _bookingRepoMock.Setup(x => x.CreateAsync(It.IsAny<Booking>()))
                .ReturnsAsync(new Booking());

            _bookingRepoMock.Setup(x => x.GetByIdWithDetailsAsync(It.IsAny<int>()))
                .ReturnsAsync(new Booking { Id = 1, ChargingSlot = slot });

            var dto = new CreateBookingDto
            {
                SlotId = 1,
                StartTime = DateTime.UtcNow.AddHours(1),
                DurationHours = 2
            };

            var result = await _service.CreateBookingAsync(10, dto);

            Assert.NotNull(result);
            _bookingRepoMock.Verify(x => x.CreateAsync(It.IsAny<Booking>()), Times.Once);
            _notiMock.Verify(x => x.SendAsync(
                99, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<NotificationType>()), Times.Once);
        }

        // Rule: TotalAmount = BasePrice * Duration
        // Bắt bug: tính tiền sai
        [Fact]
        public async Task CreateBooking_ShouldCalculateTotalCorrect()
        {
            var slot = new ChargingSlot
            {
                Status = SlotStatus.Active,
                BasePricePerHour = 200
            };

            Booking captured = null;

            _slotRepoMock.Setup(x => x.GetByIdAsync(It.IsAny<int>(), It.IsAny<bool>()))
                .ReturnsAsync(slot);

            _bookingRepoMock.Setup(x => x.HasOverlappingBookingAsync(
                It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<int?>()))
                .ReturnsAsync(false);

            _bookingRepoMock.Setup(x => x.CreateAsync(It.IsAny<Booking>()))
                .Callback<Booking>(b => captured = b)
                .ReturnsAsync(new Booking());

            _bookingRepoMock.Setup(x => x.GetByIdWithDetailsAsync(It.IsAny<int>()))
                .ReturnsAsync(new Booking { Id = 1, ChargingSlot = slot });

            var dto = new CreateBookingDto
            {
                SlotId = 1,
                StartTime = DateTime.UtcNow.AddHours(1),
                DurationHours = 3
            };

            await _service.CreateBookingAsync(1, dto);

            Assert.Equal(600, captured.TotalAmount);
        }

        // =========================
        // ACCEPT BOOKING
        // =========================

        // Rule: TotalAmount = BasePrice * Duration
        // Bắt bug: tính tiền sai
        [Fact]
        public async Task AcceptBooking_ShouldSuccess()
        {
            var booking = new Booking
            {
                Status = BookingStatus.WaitingOwner,
                StartTime = DateTime.UtcNow.AddHours(1),
                DriverUserId = 10,
                ChargingSlot = new ChargingSlot
                {
                    ChargingStation = new ChargingStation { OwnerUserId = 99 }
                }
            };

            _bookingRepoMock.Setup(x => x.GetByIdWithDetailsAsync(It.IsAny<int>()))
                .ReturnsAsync(booking);

            var result = await _service.AcceptBookingAsync(99, 1);

            Assert.Equal(BookingStatus.PendingPayment.ToString(), result.Status);
            _bookingRepoMock.Verify(x => x.UpdateAsync(It.IsAny<Booking>()), Times.Once);
        }

        // Rule: chỉ Owner của station mới được accept
        // Security check
        [Fact]
        public async Task AcceptBooking_ShouldFail_WhenNotOwner()
        {
            var booking = new Booking
            {
                Status = BookingStatus.WaitingOwner,
                ChargingSlot = new ChargingSlot
                {
                    ChargingStation = new ChargingStation { OwnerUserId = 99 }
                }
            };

            _bookingRepoMock.Setup(x => x.GetByIdWithDetailsAsync(It.IsAny<int>()))
                .ReturnsAsync(booking);

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                _service.AcceptBookingAsync(1, 1));
        }

        // Rule: chỉ accept khi status = WaitingOwner
        // Bắt bug: accept sai state
        [Fact]
        public async Task AcceptBooking_ShouldFail_WhenWrongStatus()
        {
            var booking = new Booking
            {
                Status = BookingStatus.Completed,
                ChargingSlot = new ChargingSlot
                {
                    ChargingStation = new ChargingStation { OwnerUserId = 99 }
                }
            };

            _bookingRepoMock.Setup(x => x.GetByIdWithDetailsAsync(It.IsAny<int>()))
                .ReturnsAsync(booking);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.AcceptBookingAsync(99, 1));
        }

        // =========================
        // REJECT BOOKING
        // =========================

        // Flow Owner reject booking
        // Booking → Rejected + notify driver
        [Fact]
        public async Task RejectBooking_ShouldSuccess()
        {
            var booking = new Booking
            {
                Status = BookingStatus.WaitingOwner,
                DriverUserId = 10,
                ChargingSlot = new ChargingSlot
                {
                    ChargingStation = new ChargingStation { OwnerUserId = 99 }
                }
            };

            _bookingRepoMock.Setup(x => x.GetByIdWithDetailsAsync(It.IsAny<int>()))
                .ReturnsAsync(booking);

            var result = await _service.RejectBookingAsync(99, 1,
                new RejectBookingDto { RejectionReason = "Full" });

            Assert.Equal(BookingStatus.Rejected.ToString(), result.Status);
            _bookingRepoMock.Verify(x => x.UpdateAsync(It.IsAny<Booking>()), Times.Once);
        }

        // Rule: chỉ Owner được reject
        // Security check
        [Fact]
        public async Task RejectBooking_ShouldFail_WhenNotOwner()
        {
            var booking = new Booking
            {
                Status = BookingStatus.WaitingOwner,
                ChargingSlot = new ChargingSlot
                {
                    ChargingStation = new ChargingStation { OwnerUserId = 99 }
                }
            };

            _bookingRepoMock.Setup(x => x.GetByIdWithDetailsAsync(It.IsAny<int>()))
                .ReturnsAsync(booking);

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                _service.RejectBookingAsync(1, 1,
                    new RejectBookingDto { RejectionReason = "" }));
        }


        // =========================
        // EDGE CASE / BUSINESS HARD RULES
        // =========================

        // Rule : phải đặt trước X phút (ví dụ 15p)
        // Tránh user spam đặt sát giờ
        [Fact]
        public async Task CreateBooking_ShouldFail_WhenStartTooSoon()
        {
            var dto = new CreateBookingDto
            {
                SlotId = 1,
                StartTime = DateTime.UtcNow.AddMinutes(5), // quá gần
                DurationHours = 2
            };

            var slot = new ChargingSlot
            {
                Status = SlotStatus.Active,
                BasePricePerHour = 100
            };

            _slotRepoMock.Setup(x => x.GetByIdAsync(
                It.IsAny<int>(), It.IsAny<bool>()))
                .ReturnsAsync(slot);

            _bookingRepoMock.Setup(x => x.HasOverlappingBookingAsync(
                It.IsAny<int>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<int?>()))
                .ReturnsAsync(false);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.CreateBookingAsync(1, dto));
        }

        // Rule : phải đặt trước X phút (ví dụ 15p)
        // Tránh user spam đặt sát giờ
        [Fact]
        public async Task CreateBooking_ShouldFail_WhenDurationTooLarge()
        {
            var dto = new CreateBookingDto
            {
                SlotId = 1,
                StartTime = DateTime.UtcNow.AddHours(1),
                DurationHours = 100 // quá lớn
            };

            var slot = new ChargingSlot
            {
                Status = SlotStatus.Active,
                BasePricePerHour = 100
            };

            _slotRepoMock.Setup(x => x.GetByIdAsync(
                It.IsAny<int>(), It.IsAny<bool>()))
                .ReturnsAsync(slot);

            _bookingRepoMock.Setup(x => x.HasOverlappingBookingAsync(
                It.IsAny<int>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<int?>()))
                .ReturnsAsync(false);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.CreateBookingAsync(1, dto));
        }

        // Rule: 1 driver chỉ được có 1 booking Pending/Draft
        // Anti-spam user
        //[Fact]
        //public async Task CreateBooking_ShouldFail_WhenDriverHasPendingBooking()
        //{
        //    var dto = new CreateBookingDto
        //    {
        //        SlotId = 1,
        //        StartTime = DateTime.UtcNow.AddHours(1),
        //        DurationHours = 2
        //    };

        //    var slot = new ChargingSlot
        //    {
        //        Status = SlotStatus.Active,
        //        BasePricePerHour = 100
        //    };

        //    _slotRepoMock.Setup(x => x.GetByIdAsync(
        //        It.IsAny<int>(), It.IsAny<bool>()))
        //        .ReturnsAsync(slot);

        //    _bookingRepoMock.Setup(x => x.HasOverlappingBookingAsync(
        //        It.IsAny<int>(),
        //        It.IsAny<DateTime>(),
        //        It.IsAny<DateTime>(),
        //        It.IsAny<int?>()))
        //        .ReturnsAsync(false);

        //    // ❗ giả lập user đã có booking pending
        //    _bookingRepoMock.Setup(x => x.HasPendingBookingByDriverAsync(It.IsAny<int>()))
        //        .ReturnsAsync(true);

        //    await Assert.ThrowsAsync<InvalidOperationException>(() =>
        //        _service.CreateBookingAsync(1, dto));
        //}

        // Rule QUAN TRỌNG:
        // 1 slot + 1 time chỉ được tồn tại 1 PendingPayment
        // Nếu đã có booking pending → không cho accept thêm
        // Đây là rule cực quan trọng chống double charge
        [Fact]
        public async Task AcceptBooking_ShouldFail_WhenAnotherPendingExists()
        {
            var booking = new Booking
            {
                Id = 1,
                Status = BookingStatus.WaitingOwner,
                SlotId = 1,
                StartTime = DateTime.UtcNow.AddHours(1),
                ChargingSlot = new ChargingSlot
                {
                    ChargingStation = new ChargingStation { OwnerUserId = 99 }
                }
            };

            _bookingRepoMock.Setup(x => x.GetByIdWithDetailsAsync(1))
                .ReturnsAsync(booking);

            // ❗ đã có booking pending khác
            _bookingRepoMock.Setup(x => x.HasOverlappingBookingAsync(
                It.IsAny<int>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<int>()))
                .ReturnsAsync(true);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.AcceptBookingAsync(99, 1));
        }

        // Rule: khi Paid → reject tất cả booking overlap
        // Core business flow 
        //[Fact]
        //public async Task Payment_ShouldRejectOtherBookings_WhenPaid()
        //{
        //    var booking = new Booking
        //    {
        //        Id = 1,
        //        SlotId = 1,
        //        StartTime = DateTime.UtcNow.AddHours(1),
        //        EndTime = DateTime.UtcNow.AddHours(3),
        //        Status = BookingStatus.PendingPayment
        //    };

        //    _bookingRepoMock.Setup(x => x.GetByIdWithDetailsAsync(1))
        //        .ReturnsAsync(booking);

        //    // giả lập các booking overlap
        //    _bookingRepoMock.Setup(x => x.GetOverlappingBookingsAsync(
        //        It.IsAny<int>(),
        //        It.IsAny<DateTime>(),
        //        It.IsAny<DateTime>()))
        //        .ReturnsAsync(new List<Booking>
        //        {
        //    new Booking { Id = 2, Status = BookingStatus.Draft },
        //    new Booking { Id = 3, Status = BookingStatus.PendingPayment }
        //        });

        //    await _service.MarkAsPaidAsync(1); // giả sử có method này

        //    _bookingRepoMock.Verify(x => x.UpdateAsync(
        //        It.Is<Booking>(b => b.Status == BookingStatus.Rejected)),
        //        Times.AtLeastOnce);
        //}
    }
}