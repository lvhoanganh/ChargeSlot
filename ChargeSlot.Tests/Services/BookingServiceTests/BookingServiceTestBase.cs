using ChargeSlot.Api.Services.Implementation;
using ChargeSlot.Api.Repositories.Interfaces;
using ChargeSlot.Api.Services.Interfaces;
using ChargeSlot.Api.Models;
using ChargeSlot.Api.Enums;
using ChargeSlot.Api.DTOs.Booking;
using Microsoft.Extensions.Logging;
using Moq;

namespace ChargeSlot.Tests.Services.BookingServiceTests
{
    /// Base class: khởi tạo tất cả mock + default setups cho BookingService.
    /// Mỗi test class kế thừa và chỉ override setup cần thiết.
    public abstract class BookingServiceTestBase
    {
        protected readonly Mock<IBookingRepository>                _bookingRepo     = new();
        protected readonly Mock<IChargingSlotRepository>           _slotRepo        = new();
        protected readonly Mock<INotificationService>              _notiMock        = new();
        protected readonly Mock<IWalletRepository>                 _walletRepo      = new();
        protected readonly Mock<IUnitOfWork>                       _uow             = new();
        protected readonly Mock<IStationUnavailableDateRepository> _unavailRepo     = new();
        protected readonly Mock<IStationPricingRepository>         _pricingRepo     = new();
        protected readonly Mock<IExtraServiceRepository>           _extraRepo       = new();
        protected readonly Mock<IDriverRepository>                 _driverRepo      = new();
        protected readonly Mock<ILoyaltyTransactionRepository>     _loyaltyRepo     = new();
        protected readonly Mock<ILedgerTransactionRepository>      _ledgerRepo      = new();
        protected readonly Mock<ILogger<BookingService>>           _logger          = new();
        protected readonly Mock<ISystemConfigService>              _configSvc       = new();

        protected BookingServiceTestBase()
        {
            // Transaction
            var tx = new Mock<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction>();
            tx.Setup(x => x.CommitAsync(default)).Returns(Task.CompletedTask);
            tx.Setup(x => x.RollbackAsync(default)).Returns(Task.CompletedTask);
            _uow.Setup(x => x.BeginTransactionAsync()).ReturnsAsync(tx.Object);
            _uow.Setup(x => x.CompleteAsync()).ReturnsAsync(1);

            // Pricing: 1 tier 24/7 = 100đ/h
            _pricingRepo.Setup(x => x.GetActiveByStationIdAsync(It.IsAny<int>()))
                .ReturnsAsync(new List<StationPricing>
                {
                    new StationPricing
                    {
                        StartTime    = new TimeOnly(0, 0),
                        EndTime      = new TimeOnly(23, 59),
                        PricePerHour = 100m,
                        IsActive     = true,
                        EffectiveFrom = DateTime.UtcNow.AddDays(-1)
                    }
                });

            // Unavailable dates: không có ngày block
            _unavailRepo.Setup(x => x.GetDatesByStationAndDateRangeAsync(
                It.IsAny<int>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>()))
                .ReturnsAsync(new List<DateOnly>());

            // System config defaults
            _configSvc.Setup(x => x.GetCurrentConfigsAsync())
                .ReturnsAsync(new ChargeSlot.Api.DTOs.Admin.UpdateSystemConfigsDto
                {
                    CheckIn_Window_Minutes  = 15,
                    Payment_Expiry_Minutes  = 15,
                    Min_Booking_Lead_Minutes = 30,
                    Slot_Buffer_Minutes     = 5,
                    RefundPolicy100_Hrs     = 2,
                    RefundPolicy50_Hrs      = 1,
                    Platform_Fee_Rate       = 0.05m,
                    VAT_Rate                = 0.08m,
                    Loyalty_Earn_Rate       = 0.01m
                });

            // Booking repo defaults (happy path)
            _bookingRepo.Setup(x => x.AcquireSlotLockAsync(It.IsAny<int>())).Returns(Task.CompletedTask);
            _bookingRepo.Setup(x => x.GetPendingCountByDriverAsync(It.IsAny<int>())).ReturnsAsync(0);
            _bookingRepo.Setup(x => x.HasOverlappingBookingAsync(
                It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(),
                It.IsAny<int>(), It.IsAny<int?>())).ReturnsAsync(false);
            _bookingRepo.Setup(x => x.HasDriverOverlappingBookingAsync(
                It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(),
                It.IsAny<int?>())).ReturnsAsync(false);
            _bookingRepo.Setup(x => x.GetOverlappingWaitingBookingsAsync(
                It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(),
                It.IsAny<int>(), It.IsAny<int>())).ReturnsAsync(new List<Booking>());
            _bookingRepo.Setup(x => x.GetByIdWithDetailsAsync(It.IsAny<int>()))
                .ReturnsAsync((Booking?)null);

            // Extra services: mặc định empty
            _extraRepo.Setup(x => x.GetByIdsAsync(It.IsAny<List<int>>()))
                .ReturnsAsync(new List<ExtraService>());

            // Wallet defaults (cho cancel/refund paths)
            _walletRepo.Setup(x => x.GetBySystemCodeAsync(It.IsAny<string>()))
                .ReturnsAsync(new Wallet { Id = 99, AvailableBalance = 9_999_999 });
            _walletRepo.Setup(x => x.GetByUserIdAsync(It.IsAny<int>()))
                .ReturnsAsync(new Wallet { Id = 100, AvailableBalance = 0 });
            _walletRepo.Setup(x => x.TransferAtomicAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<decimal>()))
                .Returns(Task.CompletedTask);

            // Notification
            _notiMock.Setup(x => x.SendAsync(
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<NotificationType>()))
                .Returns(Task.CompletedTask);

            // Driver repo default
            _driverRepo.Setup(x => x.GetByUserIdAsync(It.IsAny<int>(), It.IsAny<bool>()))
                .ReturnsAsync((Driver?)null);

            // Loyalty repo
            _loyaltyRepo.Setup(x => x.Add(It.IsAny<LoyaltyTransaction>()));
            _ledgerRepo.Setup(x => x.Add(It.IsAny<LedgerTransaction>()));

            // Slot repo: default trả null (không có slot)
            // Service gọi GetByIdAsync(id) tức GetByIdAsync(id, false=default)
            _slotRepo.Setup(x => x.GetByIdAsync(It.IsAny<int>(), false)).ReturnsAsync((ChargingSlot?)null);
            _slotRepo.Setup(x => x.GetByIdAsync(It.IsAny<int>(), true)).ReturnsAsync((ChargingSlot?)null);
        }

        /// Tạo BookingService với toàn bộ mock đã cấu hình
        protected BookingService CreateService() => new BookingService(
            _bookingRepo.Object, _slotRepo.Object, _notiMock.Object, _walletRepo.Object,
            _uow.Object, _unavailRepo.Object, _pricingRepo.Object, _extraRepo.Object,
            _driverRepo.Object, _loyaltyRepo.Object, _ledgerRepo.Object,
            _logger.Object, _configSvc.Object);

        // Shared Helpers
        /// Trả về DateTime VN (Local) hợp lệ: phút = 00 hoặc 30, cách VietnamNow ≥ 3h.
        /// BookingService dùng DateTimeHelper.VietnamNow() nên phải truyền VN time.
        protected static DateTime ValidStart()
        {
            var vnNow    = DateTime.UtcNow.AddHours(7);
            var target   = vnNow.AddHours(3);
            // Làm tròn lên block 30 phút tiếp theo
            int minute   = target.Minute < 30 ? 30 : 0;
            int hour     = target.Minute < 30 ? target.Hour : target.Hour + 1;
            return new DateTime(target.Year, target.Month, target.Day, hour, minute, 0);
        }

        /// Slot Active + Station Approved.
        protected static ChargingSlot MakeValidSlot(int ownerId = 99) => new ChargingSlot
        {
            Id        = 1,
            StationId = 10,
            SlotName  = "Slot A",
            Status    = SlotStatus.Active,
            ChargingStation = new ChargingStation
            {
                Id                = 10,
                OwnerUserId       = ownerId,
                Name              = "Station X",
                ApprovalStatus    = ApprovalStatus.Approved,
                OperationalStatus = OperationalStatus.Active
            }
        };

        /// Booking đã Paid với snapshot refund deadline hợp lệ.
        protected static Booking MakePaidBooking(
            int driverId     = 10,
            int ownerId      = 99,
            decimal amount   = 300_000m,
            DateTime? start  = null) 
        {
            var startTime = start ?? DateTime.UtcNow.AddHours(7).AddHours(5);
            return new Booking
            {
                Id                   = 1,
                DriverUserId         = driverId,
                Status               = BookingStatus.Paid,
                SlotId               = 1,
                TotalAmount          = amount,
                StartTime            = startTime,
                Refund100DeadlineAt  = startTime.AddHours(-2),
                Refund50DeadlineAt   = startTime.AddHours(-1),
                PlatformFeeRateSnapshot = 0.05m,
                VatRateSnapshot      = 0.08m,
                BookingExtraServices = new List<BookingExtraService>(),
                ChargingSlot = new ChargingSlot
                {
                    SlotName = "Slot A",
                    Status   = SlotStatus.Booked,
                    ChargingStation = new ChargingStation
                    {
                        OwnerUserId = ownerId,
                        Name        = "Station X"
                    }
                }
            };
        }

        protected static CreateBookingDto ValidDto(decimal durationHours = 1m) => new CreateBookingDto
        {
            SlotId        = 1,
            StartTime     = ValidStart(),
            DurationHours = durationHours
        };

        /// Helper mock slotRepo.GetByIdAsync với đúng signature (cả tracking=false và true).
        /// Tránh CS0854 optional argument trong expression tree.
        protected void SetupSlot(ChargingSlot? slot)
        {
            _slotRepo.Setup(x => x.GetByIdAsync(It.IsAny<int>(), false)).ReturnsAsync(slot);
            _slotRepo.Setup(x => x.GetByIdAsync(It.IsAny<int>(), true)).ReturnsAsync(slot);
        }
    }
}
