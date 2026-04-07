using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ChargeSlot.Api.Data;
using ChargeSlot.Api.DTOs.Booking;
using ChargeSlot.Api.DTOs.Admin;
using ChargeSlot.Api.Enums;
using ChargeSlot.Api.Models;
using ChargeSlot.Api.Models.Identity;
using ChargeSlot.Api.Repositories.Interfaces;
using ChargeSlot.Api.Services.Implementation;
using ChargeSlot.Api.Services.Interfaces;
using ChargeSlot.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ChargeSlot.Tests.Services
{
    public class BookingServiceTests
    {
        private readonly Mock<IBookingRepository> _bookingRepo;
        private readonly Mock<IChargingSlotRepository> _slotRepo;
        private readonly Mock<INotificationService> _notificationService;
        private readonly Mock<IWalletRepository> _walletRepo;
        private readonly Mock<ILogger<BookingService>> _logger;
        private readonly Mock<ISystemConfigService> _configService;
        private readonly ChargeSlotDbContext _db;
        private readonly BookingService _service;

        public BookingServiceTests()
        {
            _bookingRepo = new Mock<IBookingRepository>();
            _slotRepo = new Mock<IChargingSlotRepository>();
            _notificationService = new Mock<INotificationService>();
            _walletRepo = new Mock<IWalletRepository>();
            _logger = new Mock<ILogger<BookingService>>();
            _configService = new Mock<ISystemConfigService>();
            _db = TestDbHelper.CreateInMemoryDb();

            _configService.Setup(c => c.GetCurrentConfigsAsync()).ReturnsAsync(TestDbHelper.GetDefaultConfigs());

            _service = new BookingService(
                _bookingRepo.Object, _slotRepo.Object, _notificationService.Object,
                _walletRepo.Object, _db, _logger.Object, _configService.Object);
        }

        [Fact]
        public async Task CreateBooking_Success()
        {
            await TestDbHelper.SeedSystemWalletsAsync(_db);
            await TestDbHelper.SeedDriverAsync(_db, 1);
            var (station, slots) = await TestDbHelper.SeedStationWithSlotsAsync(_db, 2, 1);

            // Seed pricing so total can be calculated
            _db.Set<Api.Models.StationPricing>().Add(new Api.Models.StationPricing
            {
                StationId = station.Id, PricePerHour = 50000,
                StartTime = new TimeOnly(0, 0), EndTime = new TimeOnly(23, 59),
                Priority = 1, IsActive = true, EffectiveFrom = DateTime.Now.AddYears(-1),
                CreatedAt = DateTime.Now
            });
            await _db.SaveChangesAsync();

            // Use aligned 30-min block time
            var startTime = TestDbHelper.NextAlignedStartTime(2);
            var dto = new CreateBookingDto
            {
                SlotId = slots[0].Id,
                StartTime = startTime,
                DurationHours = 2,
                Note = "Sạc nhanh"
            };

            _slotRepo.Setup(r => r.GetByIdAsync(dto.SlotId, false)).ReturnsAsync(slots[0]);
            _bookingRepo.Setup(r => r.HasOverlappingBookingAsync(dto.SlotId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), null)).ReturnsAsync(false);
            _bookingRepo.Setup(r => r.HasDriverOverlappingBookingAsync(1, It.IsAny<DateTime>(), It.IsAny<DateTime>(), null)).ReturnsAsync(false);
            _bookingRepo.Setup(r => r.GetByIdWithDetailsAsync(It.IsAny<int>())).ReturnsAsync((int id) =>
            {
                // Return a booking-like object with required navigation props
                return new Booking
                {
                    Id = id, DriverUserId = 1, SlotId = slots[0].Id,
                    ChargingSlot = slots[0], Status = BookingStatus.WaitingOwner,
                    StartTime = startTime, EndTime = startTime.AddHours(2),
                    DurationHours = 2, TotalAmount = 100_000, CreatedAt = DateTime.Now
                };
            });

            var result = await _service.CreateBookingAsync(1, dto);

            Assert.NotNull(result);
            Assert.Equal(BookingStatus.WaitingOwner.ToString(), result.Status);
            _bookingRepo.Verify(r => r.CreateAsync(It.IsAny<Booking>()), Times.Once);
        }

        [Fact]
        public async Task CreateBooking_SlotUnavailable_Throws()
        {
            await TestDbHelper.SeedDriverAsync(_db, 1);
            var (station, slots) = await TestDbHelper.SeedStationWithSlotsAsync(_db, 2, 1);
            var dto = new CreateBookingDto { SlotId = slots[0].Id, StartTime = DateTime.Now.AddHours(2), DurationHours = 2 };
            
            _slotRepo.Setup(r => r.GetByIdAsync(slots[0].Id, false)).ReturnsAsync(slots[0]);
            _bookingRepo.Setup(r => r.HasOverlappingBookingAsync(slots[0].Id, It.IsAny<DateTime>(), It.IsAny<DateTime>(), null)).ReturnsAsync(true);

            await Assert.ThrowsAsync<InvalidOperationException>(() => _service.CreateBookingAsync(1, dto));
        }

        [Fact]
        public async Task AcceptBooking_Success()
        {
            await TestDbHelper.SeedDriverAsync(_db, 1);
            var booking = await TestDbHelper.SeedBookingAsync(_db, 1, 2, 1, BookingStatus.WaitingOwner);
            _bookingRepo.Setup(r => r.GetByIdWithDetailsAsync(1)).ReturnsAsync(booking);
            _bookingRepo.Setup(r => r.GetOverlappingWaitingBookingsAsync(
                It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<int>()
            )).ReturnsAsync(new List<Booking>());

            var result = await _service.AcceptBookingAsync(2, 1);

            Assert.Equal(BookingStatus.PendingPayment.ToString(), result.Status);
            _notificationService.Verify(n => n.SendAsync(booking.DriverUserId, It.IsAny<string>(), It.IsAny<string>(), NotificationType.Booking), Times.Once);
        }

        [Fact]
        public async Task AcceptBooking_AlreadyAccepted_Throws()
        {
            var booking = await TestDbHelper.SeedBookingAsync(_db, 1, 2, 1, BookingStatus.PendingPayment);
            _bookingRepo.Setup(r => r.GetByIdWithDetailsAsync(1)).ReturnsAsync(booking);

            await Assert.ThrowsAsync<InvalidOperationException>(() => _service.AcceptBookingAsync(2, 1));
        }

        [Fact]
        public async Task RejectBooking_Success()
        {
            var booking = await TestDbHelper.SeedBookingAsync(_db, 1, 2, 1, BookingStatus.WaitingOwner);
            _bookingRepo.Setup(r => r.GetByIdWithDetailsAsync(1)).ReturnsAsync(booking);

            var result = await _service.RejectBookingAsync(2, 1, new RejectBookingDto { RejectionReason = "Bận" });

            Assert.Equal(BookingStatus.Rejected.ToString(), result.Status);
        }

        [Fact]
        public async Task DriverCancel_PendingPayment_Success()
        {
            var booking = await TestDbHelper.SeedBookingAsync(_db, 1, 2, 1, BookingStatus.PendingPayment);
            _bookingRepo.Setup(r => r.GetByIdWithDetailsAsync(1)).ReturnsAsync(booking);

            var result = await _service.DriverCancelBookingAsync(1, 1, "Đổi lịch");

            Assert.Equal(BookingStatus.Cancelled.ToString(), result.Status);
        }

        [Fact]
        public async Task OwnerCancel_Paid_Success()
        {
            await TestDbHelper.SeedSystemWalletsAsync(_db);
            await TestDbHelper.SeedDriverAsync(_db, 1);
            var booking = await TestDbHelper.SeedBookingAsync(_db, 1, 2, 1, BookingStatus.Paid);
            _bookingRepo.Setup(r => r.GetByIdWithDetailsAsync(1)).ReturnsAsync(booking);

            var result = await _service.OwnerCancelBookingAsync(2, 1, "Trạm bảo trì");

            Assert.Equal(BookingStatus.Cancelled.ToString(), result.Status);
        }

        [Fact]
        public async Task GetBookingById_NotFound_ReturnsNull()
        {
            _bookingRepo.Setup(r => r.GetByIdWithDetailsAsync(999)).ReturnsAsync((Booking?)null);
            var result = await _service.GetByIdAsync(999);
            Assert.Null(result);
        }
    }
}
