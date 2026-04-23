using ChargeSlot.Api.Models;
using ChargeSlot.Api.Enums;
using ChargeSlot.Api.DTOs.Booking;
using Moq;

namespace ChargeSlot.Tests.Services.BookingServiceTests
{
    public class CreateBookingTests : BookingServiceTestBase
    {
        // TC01-TC05: Validate DurationHours
        [Fact]
        public async Task TC01_DurationZero_ShouldThrow()
        {
            var dto = ValidDto(); dto.DurationHours = 0;
            await Assert.ThrowsAsync<InvalidOperationException>(() => CreateService().CreateBookingAsync(1, dto));
        }

        [Fact]
        public async Task TC02_DurationNegative_ShouldThrow()
        {
            var dto = ValidDto(); dto.DurationHours = -1;
            await Assert.ThrowsAsync<InvalidOperationException>(() => CreateService().CreateBookingAsync(1, dto));
        }

        [Fact]
        public async Task TC03_DurationOver24_ShouldThrow()
        {
            var dto = ValidDto(); dto.DurationHours = 25;
            await Assert.ThrowsAsync<InvalidOperationException>(() => CreateService().CreateBookingAsync(1, dto));
        }

        [Fact]
        public async Task TC04_DurationNotHalfHourStep_ShouldThrow()
        {
            // 1.3h không phải bội số 0.5
            var dto = ValidDto(); dto.DurationHours = 1.3m;
            await Assert.ThrowsAsync<InvalidOperationException>(() => CreateService().CreateBookingAsync(1, dto));
        }

        [Fact]
        public async Task TC05_Duration_ExactBoundary24h_ShouldNotThrow()
        {
            // 24h là hợp lệ (<=24)
            var slot = MakeValidSlot();
            SetupSlot(slot);
            _bookingRepo.Setup(x => x.GetByIdWithDetailsAsync(It.IsAny<int>()))
                .ReturnsAsync(new Booking
                {
                    Id = 1, Status = BookingStatus.WaitingOwner,
                    ChargingSlot = slot, BookingExtraServices = new List<BookingExtraService>()
                });

            var dto = ValidDto(24m);
            var result = await CreateService().CreateBookingAsync(1, dto);
            Assert.NotNull(result);
        }

        // TC06-TC09: Validate StartTime block
        [Fact]
        public async Task TC06_StartTimeInvalidMinute_ShouldThrow()
        {
            // Input: dto.StartTime = VietnamNow+3h, phút=15, giây=0 (phút không phải 0 hoặc 30 → throw)
            var dto = ValidDto();
            var vnNow  = DateTime.UtcNow.AddHours(7).AddHours(3);
            dto.StartTime = new DateTime(vnNow.Year, vnNow.Month, vnNow.Day, vnNow.Hour, 15, 0);
            await Assert.ThrowsAsync<InvalidOperationException>(() => CreateService().CreateBookingAsync(1, dto));
        }

        [Fact]
        public async Task TC07_StartTimeHasSeconds_ShouldThrow()
        {
            var dto = ValidDto();
            dto.StartTime = ValidStart().AddSeconds(5);
            await Assert.ThrowsAsync<InvalidOperationException>(() => CreateService().CreateBookingAsync(1, dto));
        }

        [Fact]
        public async Task TC08_StartTimeInPast_ShouldThrow()
        {
            var dto = ValidDto();
            var pastVn = DateTime.UtcNow.AddHours(7).AddHours(-2);
            dto.StartTime = new DateTime(pastVn.Year, pastVn.Month, pastVn.Day, pastVn.Hour, 0, 0);
            await Assert.ThrowsAsync<InvalidOperationException>(() => CreateService().CreateBookingAsync(1, dto));
        }

        [Fact]
        public async Task TC09_StartTimeTooSoon_BelowLeadTime_ShouldThrow()
        {
            // Input: dto.StartTime = VietnamNow+10p, làm tròn block 30p xuống, giây=0
            // VD: nếu now=10:25 → nearVn=10:35 → phút≥30 → StartTime=10:30 (cách now chỉ 5p < min 30p)
            var dto = ValidDto();
            var nearVn = DateTime.UtcNow.AddHours(7).AddMinutes(10);
            dto.StartTime = new DateTime(nearVn.Year, nearVn.Month, nearVn.Day, nearVn.Hour, nearVn.Minute >= 30 ? 30 : 0, 0);
            await Assert.ThrowsAsync<InvalidOperationException>(() => CreateService().CreateBookingAsync(1, dto));
        }

        // TC10: Driver pending booking >= 3
        [Fact]
        public async Task TC10_PendingBookingsFull_ShouldThrow()
        {
            _bookingRepo.Setup(x => x.GetPendingCountByDriverAsync(It.IsAny<int>())).ReturnsAsync(3);
            await Assert.ThrowsAsync<InvalidOperationException>(() => CreateService().CreateBookingAsync(1, ValidDto()));
        }

        // TC11-TC12: Slot validation
        [Fact]
        public async Task TC11_SlotNotFound_ShouldThrow()
        {
            // base default đã setup GetByIdAsync → null
            await Assert.ThrowsAsync<InvalidOperationException>(() => CreateService().CreateBookingAsync(1, ValidDto()));
        }

        [Theory]
        [InlineData(SlotStatus.Inactive)]
        [InlineData(SlotStatus.Maintenance)]
        public async Task TC12_SlotUnavailable_ShouldThrow(SlotStatus status)
        {
            var slot = MakeValidSlot(); slot.Status = status;
            SetupSlot(slot);
            await Assert.ThrowsAsync<InvalidOperationException>(() => CreateService().CreateBookingAsync(1, ValidDto()));
        }

        // TC13-TC15: Station validation
        [Fact]
        public async Task TC13_StationNotApproved_ShouldThrow()
        {
            var slot = MakeValidSlot();
            slot.ChargingStation.ApprovalStatus = ApprovalStatus.PendingApproval;
            SetupSlot(slot);
            await Assert.ThrowsAsync<InvalidOperationException>(() => CreateService().CreateBookingAsync(1, ValidDto()));
        }

        [Fact]
        public async Task TC14_StationInactive_ShouldThrow()
        {
            var slot = MakeValidSlot();
            slot.ChargingStation.OperationalStatus = OperationalStatus.Inactive;
            SetupSlot(slot);
            await Assert.ThrowsAsync<InvalidOperationException>(() => CreateService().CreateBookingAsync(1, ValidDto()));
        }

        // TC15: Unavailable date
        [Fact]
        public async Task TC15_UnavailableDate_ShouldThrow()
        {
            var slot = MakeValidSlot();
            SetupSlot(slot);
            _unavailRepo.Setup(x => x.GetDatesByStationAndDateRangeAsync(
                It.IsAny<int>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>()))
                .ReturnsAsync(new List<DateOnly> { DateOnly.FromDateTime(DateTime.Now.AddDays(1)) });
            await Assert.ThrowsAsync<InvalidOperationException>(() => CreateService().CreateBookingAsync(1, ValidDto()));
        }

        // TC16-TC17: Overlap checks
        [Fact]
        public async Task TC16_SlotOverlap_ShouldThrow()
        {
            var slot = MakeValidSlot();
            SetupSlot(slot);
            _bookingRepo.Setup(x => x.HasOverlappingBookingAsync(
                It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(),
                It.IsAny<int>(), It.IsAny<int?>())).ReturnsAsync(true);
            await Assert.ThrowsAsync<InvalidOperationException>(() => CreateService().CreateBookingAsync(1, ValidDto()));
        }

        [Fact]
        public async Task TC17_DriverOverlap_ShouldThrow()
        {
            var slot = MakeValidSlot();
            SetupSlot(slot);
            _bookingRepo.Setup(x => x.HasDriverOverlappingBookingAsync(
                It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(),
                It.IsAny<int?>())).ReturnsAsync(true);
            await Assert.ThrowsAsync<InvalidOperationException>(() => CreateService().CreateBookingAsync(1, ValidDto()));
        }

        // TC18: No pricing
        [Fact]
        public async Task TC18_NoPricingTiers_ShouldThrow()
        {
            var slot = MakeValidSlot();
            SetupSlot(slot);
            _pricingRepo.Setup(x => x.GetActiveByStationIdAsync(It.IsAny<int>()))
                .ReturnsAsync(new List<StationPricing>());
            await Assert.ThrowsAsync<InvalidOperationException>(() => CreateService().CreateBookingAsync(1, ValidDto()));
        }

        // TC19-TC22: ExtraService validation
        [Fact]
        public async Task TC19_ExtraService_NotExist_ShouldThrow()
        {
            var slot = MakeValidSlot();
            SetupSlot(slot);
            // _extraRepo default trả empty list → service không tìm thấy serviceId=1
            var dto = ValidDto();
            dto.ExtraServices = new() { new BookingExtraServiceItemDto { ServiceId = 1, Quantity = 1 } };
            await Assert.ThrowsAsync<InvalidOperationException>(() => CreateService().CreateBookingAsync(1, dto));
        }

        [Fact]
        public async Task TC20_ExtraService_WrongStation_ShouldThrow()
        {
            var slot = MakeValidSlot(); // StationId = 10
            SetupSlot(slot);
            _extraRepo.Setup(x => x.GetByIdsAsync(It.IsAny<List<int>>()))
                .ReturnsAsync(new List<ExtraService>
                {
                    new ExtraService { Id = 5, StationId = 999, ServiceName = "Other", Price = 10m, IsActive = true }
                });
            var dto = ValidDto();
            dto.ExtraServices = new() { new BookingExtraServiceItemDto { ServiceId = 5, Quantity = 1 } };
            await Assert.ThrowsAsync<InvalidOperationException>(() => CreateService().CreateBookingAsync(1, dto));
        }

        [Fact]
        public async Task TC21_ExtraService_Inactive_ShouldThrow()
        {
            var slot = MakeValidSlot();
            SetupSlot(slot);
            _extraRepo.Setup(x => x.GetByIdsAsync(It.IsAny<List<int>>()))
                .ReturnsAsync(new List<ExtraService>
                {
                    new ExtraService { Id = 5, StationId = 10, ServiceName = "Svc", Price = 10m, IsActive = false }
                });
            var dto = ValidDto();
            dto.ExtraServices = new() { new BookingExtraServiceItemDto { ServiceId = 5, Quantity = 1 } };
            await Assert.ThrowsAsync<InvalidOperationException>(() => CreateService().CreateBookingAsync(1, dto));
        }

        [Fact]
        public async Task TC22_ExtraService_OutOfStock_ShouldThrow()
        {
            var slot = MakeValidSlot();
            SetupSlot(slot);
            _extraRepo.Setup(x => x.GetByIdsAsync(It.IsAny<List<int>>()))
                .ReturnsAsync(new List<ExtraService>
                {
                    new ExtraService { Id = 5, StationId = 10, ServiceName = "Cable", Price = 20m, IsActive = true, TotalStock = 1 }
                });
            var dto = ValidDto();
            dto.ExtraServices = new() { new BookingExtraServiceItemDto { ServiceId = 5, Quantity = 3 } }; // 3 > 1
            await Assert.ThrowsAsync<InvalidOperationException>(() => CreateService().CreateBookingAsync(1, dto));
        }

        // TC23-TC24: Loyalty points validation
        [Fact]
        public async Task TC23_Points_ExceedDriverBalance_ShouldThrow()
        {
            var slot = MakeValidSlot();
            SetupSlot(slot);
            _driverRepo.Setup(x => x.GetByUserIdAsync(1, true))
                .ReturnsAsync(new Driver { UserId = 1, LoyaltyPoints = 10m });
            var dto = ValidDto(); dto.PointsToUse = 50; // 50 > 10 → throw
            await Assert.ThrowsAsync<InvalidOperationException>(() => CreateService().CreateBookingAsync(1, dto));
        }

        [Fact]
        public async Task TC24_Points_ExceedTotalAmount_ShouldThrow()
        {
            // TotalAmount = 1h × 100đ = 100đ; nếu dùng 200 điểm → vượt totalAmount → throw
            var slot = MakeValidSlot();
            SetupSlot(slot);
            _driverRepo.Setup(x => x.GetByUserIdAsync(1, true))
                .ReturnsAsync(new Driver { UserId = 1, LoyaltyPoints = 500m });
            var dto = ValidDto(); dto.PointsToUse = 200; // 200 > 100đ → throw
            await Assert.ThrowsAsync<InvalidOperationException>(() => CreateService().CreateBookingAsync(1, dto));
        }

        // TC25-TC28: Happy path
        [Fact]
        public async Task TC25_HappyPath_NoExtraNoPoints_ShouldSucceed()
        {
            var slot = MakeValidSlot();
            SetupSlot(slot);
            _bookingRepo.Setup(x => x.GetByIdWithDetailsAsync(It.IsAny<int>()))
                .ReturnsAsync(new Booking
                {
                    Id = 1, Status = BookingStatus.WaitingOwner,
                    ChargingSlot = slot, BookingExtraServices = new List<BookingExtraService>()
                });

            var result = await CreateService().CreateBookingAsync(1, ValidDto());

            Assert.NotNull(result);
            Assert.Equal("WaitingOwner", result.Status);
            _bookingRepo.Verify(x => x.Add(It.IsAny<Booking>()), Times.Once);
            _notiMock.Verify(x => x.SendAsync(
                slot.ChargingStation.OwnerUserId, It.IsAny<string>(), It.IsAny<string>(),
                NotificationType.Booking), Times.Once);
        }

        [Fact]
        public async Task TC26_HappyPath_WithExtraService_StockDeducted()
        {
            var slot = MakeValidSlot();
            SetupSlot(slot);

            var svc = new ExtraService { Id = 7, StationId = 10, ServiceName = "Cable", Price = 50_000m, IsActive = true, TotalStock = 10 };
            _extraRepo.Setup(x => x.GetByIdsAsync(It.IsAny<List<int>>()))
                .ReturnsAsync(new List<ExtraService> { svc });

            Booking? captured = null;
            _bookingRepo.Setup(x => x.Add(It.IsAny<Booking>())).Callback<Booking>(b => captured = b);
            _bookingRepo.Setup(x => x.GetByIdWithDetailsAsync(It.IsAny<int>()))
                .ReturnsAsync(new Booking
                {
                    Id = 1, Status = BookingStatus.WaitingOwner,
                    ChargingSlot = slot,
                    BookingExtraServices = new List<BookingExtraService>
                    {
                        new BookingExtraService { ServiceId = 7, Quantity = 2, UnitPrice = 50_000m, TotalPrice = 100_000m }
                    }
                });

            var dto = ValidDto();
            dto.ExtraServices = new() { new BookingExtraServiceItemDto { ServiceId = 7, Quantity = 2 } };

            await CreateService().CreateBookingAsync(1, dto);

            Assert.NotNull(captured);
            // TotalAmount = 1h×100đ + 2×50_000đ = 100_100đ
            Assert.Equal(100_100m, captured!.TotalAmount);
            // Stock bị trừ: 10 - 2 = 8
            Assert.Equal(8, svc.TotalStock);
            _extraRepo.Verify(x => x.Update(svc), Times.Once);
        }

        [Fact]
        public async Task TC27_HappyPath_WithLoyaltyPoints_TotalAmountReduced()
        {
            // TotalAmount gốc = 1h × 100đ = 100đ; dùng 50 điểm → 100 - 50 = 50đ
            var slot = MakeValidSlot();
            SetupSlot(slot);
            _driverRepo.Setup(x => x.GetByUserIdAsync(1, true))
                .ReturnsAsync(new Driver { UserId = 1, LoyaltyPoints = 200m });

            Booking? captured = null;
            _bookingRepo.Setup(x => x.Add(It.IsAny<Booking>())).Callback<Booking>(b => captured = b);
            _bookingRepo.Setup(x => x.GetByIdWithDetailsAsync(It.IsAny<int>()))
                .ReturnsAsync(new Booking { Id = 1, Status = BookingStatus.WaitingOwner, ChargingSlot = slot, BookingExtraServices = new List<BookingExtraService>() });

            var dto = ValidDto(); dto.PointsToUse = 50;
            await CreateService().CreateBookingAsync(1, dto);

            Assert.NotNull(captured);
            Assert.Equal(50m, captured!.TotalAmount);       // 100 - 50 điểm
            Assert.Equal(50m, captured.PointsUsed);
            Assert.Equal(50m, captured.PointsDiscountAmount);
            _loyaltyRepo.Verify(x => x.Add(It.Is<LoyaltyTransaction>(t => t.Type == "Redeem" && t.Points == 50m)), Times.Once);
        }

        [Fact]
        public async Task TC28_HappyPath_OwnerNotified()
        {
            var slot = MakeValidSlot(ownerId: 77);
            SetupSlot(slot);
            _bookingRepo.Setup(x => x.GetByIdWithDetailsAsync(It.IsAny<int>()))
                .ReturnsAsync(new Booking { Id = 1, Status = BookingStatus.WaitingOwner, ChargingSlot = slot, BookingExtraServices = new List<BookingExtraService>() });

            await CreateService().CreateBookingAsync(1, ValidDto());

            // Notify phải gửi đến owner với đúng ownerId
            _notiMock.Verify(x => x.SendAsync(
                77, It.IsAny<string>(), It.IsAny<string>(), NotificationType.Booking), Times.Once);
        }
    }
}
