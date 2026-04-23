using Moq;
using Microsoft.EntityFrameworkCore.Storage;
using ChargeSlot.Api.Services.Implementation;
using ChargeSlot.Api.Models;
using ChargeSlot.Api.Models.Identity;
using ChargeSlot.Api.Enums;
using ChargeSlot.Api.DTOs.Admin;
using ChargeSlot.Api.Repositories.Interfaces;
using ChargeSlot.Api.Services.Interfaces;

namespace ChargeSlot.Tests.Services.ChargingSessionServiceTests
{
    public abstract class ChargingSessionServiceTestBase
    {
        protected readonly Mock<IChargingSessionRepository>    _sessionRepoMock     = new();
        protected readonly Mock<IInvoiceRepository>            _invoiceRepoMock     = new();
        protected readonly Mock<IBookingRepository>            _bookingRepoMock     = new();
        protected readonly Mock<IChargingSlotRepository>       _slotRepoMock        = new();
        protected readonly Mock<IWalletRepository>             _walletRepoMock      = new();
        protected readonly Mock<INotificationService>          _notifyMock          = new();
        protected readonly Mock<IUnitOfWork>                   _uowMock             = new();
        protected readonly Mock<IDriverRepository>             _driverRepoMock      = new();
        protected readonly Mock<ILoyaltyTransactionRepository> _loyaltyRepoMock     = new();
        protected readonly Mock<ILedgerTransactionRepository>  _ledgerRepoMock      = new();
        protected readonly Mock<ISystemConfigService>          _configServiceMock   = new();
        protected readonly Mock<IExtraServiceRepository>       _extraServiceRepoMock= new();
        protected readonly Mock<IDbContextTransaction>         _transactionMock     = new();

        protected ChargingSessionServiceTestBase()
        {
            // UoW defaults
            _uowMock.Setup(x => x.CompleteAsync()).ReturnsAsync(1);
            _uowMock.Setup(x => x.BeginTransactionAsync()).ReturnsAsync(_transactionMock.Object);
            _transactionMock.Setup(x => x.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            _transactionMock.Setup(x => x.RollbackAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            // Notification defaul
            _notifyMock.Setup(x => x.SendAsync(
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<NotificationType>()))
                .Returns(Task.CompletedTask);

            // Config: CheckIn window = 15 phút (giá trị thật production) 
            _configServiceMock.Setup(x => x.GetCurrentConfigsAsync())
                .ReturnsAsync(new UpdateSystemConfigsDto
                {
                    CheckIn_Window_Minutes  = 15,
                    VAT_Rate                = 0.08m,
                    Platform_Fee_Rate       = 0.05m,
                    Loyalty_Earn_Rate       = 0.05m,
                });

            // Wallet defaults (cho SettlePayment)
            var escrowWallet   = new Wallet { Id = 100, AvailableBalance = 999_999_999m };
            var platformWallet = new Wallet { Id = 101, AvailableBalance = 999_999_999m };
            var taxWallet      = new Wallet { Id = 102, AvailableBalance = 999_999_999m };
            _walletRepoMock.Setup(x => x.GetBySystemCodeAsync("ESCROW"))          .ReturnsAsync(escrowWallet);
            _walletRepoMock.Setup(x => x.GetBySystemCodeAsync("PLATFORM_REVENUE")).ReturnsAsync(platformWallet);
            _walletRepoMock.Setup(x => x.GetBySystemCodeAsync("TAX_HOLD"))        .ReturnsAsync(taxWallet);
            _walletRepoMock.Setup(x => x.TransferAtomicAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<decimal>()))
                           .Returns(Task.CompletedTask);
            _walletRepoMock.Setup(x => x.GetByUserIdAsync(It.IsAny<int>()))
                           .ReturnsAsync((Wallet?)null); // test override nếu cần
            _walletRepoMock.Setup(x => x.Add(It.IsAny<Wallet>()));
            _walletRepoMock.Setup(x => x.AddLedgerTransaction(It.IsAny<LedgerTransaction>()));

            // Repository void-return defaults
            _sessionRepoMock.Setup(x => x.Add(It.IsAny<ChargingSession>()));
            _sessionRepoMock.Setup(x => x.Update(It.IsAny<ChargingSession>()));
            _invoiceRepoMock.Setup(x => x.Add(It.IsAny<Invoice>()));
            _invoiceRepoMock.Setup(x => x.Update(It.IsAny<Invoice>()));
            _bookingRepoMock.Setup(x => x.Update(It.IsAny<Booking>()));
            _loyaltyRepoMock.Setup(x => x.Add(It.IsAny<LoyaltyTransaction>()));

            // Default: không tìm thấy (test override khi cần)
            _sessionRepoMock.Setup(x => x.HasSessionByBookingAsync(It.IsAny<int>()))   .ReturnsAsync(false);
            _sessionRepoMock.Setup(x => x.HasOngoingSessionBySlotAsync(It.IsAny<int>())).ReturnsAsync(false);
            _sessionRepoMock.Setup(x => x.GetByIdWithDetailsAsync(It.IsAny<int>()))    .ReturnsAsync((ChargingSession?)null);
            _bookingRepoMock.Setup(x => x.GetPaidBookingForDriverAndSlotAsync(It.IsAny<int>(), It.IsAny<int>()))
                            .ReturnsAsync((Booking?)null);
            _bookingRepoMock.Setup(x => x.GetByIdWithDetailsAsync(It.IsAny<int>()))
                            .ReturnsAsync((Booking?)null);
            _slotRepoMock.Setup(x => x.GetByQrCodeTokenAsync(It.IsAny<string>()))    .ReturnsAsync((ChargingSlot?)null);
            _slotRepoMock.Setup(x => x.GetByIdAsync(It.IsAny<int>(), It.IsAny<bool>())).ReturnsAsync((ChargingSlot?)null);
            _invoiceRepoMock.Setup(x => x.GetByBookingIdAsync(It.IsAny<int>()))       .ReturnsAsync((Invoice?)null);
        }

        protected ChargingSessionService CreateService() => new ChargingSessionService(
            _sessionRepoMock.Object,
            _invoiceRepoMock.Object,
            _bookingRepoMock.Object,
            _slotRepoMock.Object,
            _walletRepoMock.Object,
            _notifyMock.Object,
            _uowMock.Object,
            _driverRepoMock.Object,
            _loyaltyRepoMock.Object,
            _ledgerRepoMock.Object,
            _configServiceMock.Object,
            _extraServiceRepoMock.Object);

        // HELPERS

        // Tạo slot Active với station hợp lệ.
        // OwnerUserId = 10 (thật).
        protected static ChargingSlot CreateActiveSlot(int slotId = 1, string qr = "QR-SLOT-001") =>
            new ChargingSlot
            {
                Id       = slotId,
                SlotName = "Slot A",
                QrCodeToken = qr,
                Status   = SlotStatus.Active,
                ChargingStation = new ChargingStation
                {
                    Id              = 1,
                    Name            = "Trạm Sạc Q9",
                    OwnerUserId     = 10,
                    OperationalStatus = OperationalStatus.Active
                }
            };

        // Tạo Booking ở trạng thái Paid, thời gian hợp lệ (sắp tới).
        // StartTime = now + 5 phút, EndTime = now + 2 giờ.
        protected static Booking CreatePaidBooking(
            int bookingId      = 1,
            int driverUserId   = 5,
            int slotId         = 1,
            DateTime? start    = null,
            DateTime? end      = null)
        {
            var now       = DateTime.Now;
            var startTime = start ?? now.AddMinutes(5);
            var endTime   = end   ?? now.AddHours(2);

            return new Booking
            {
                Id              = bookingId,
                DriverUserId    = driverUserId,
                SlotId          = slotId,
                Status          = BookingStatus.Paid,
                StartTime       = startTime,
                EndTime         = endTime,
                TotalAmount     = 150_000m,         // 150k VND thực tế
                PointsDiscountAmount = 0m,
                PlatformFeeRateSnapshot = 0.05m,
                VatRateSnapshot         = 0.08m,
                LoyaltyEarnRateSnapshot = 0.05m,
                CheckedInAt     = null,
                ChargingSlot    = CreateActiveSlot(slotId),
                Driver          = new Driver
                {
                    UserId        = driverUserId,
                    LoyaltyPoints = 1_000m,
                    User          = new ApplicationUser { Id = driverUserId, FullName = "Nguyen Van A" }
                },
                BookingExtraServices = new List<BookingExtraService>()
            };
        }

        // Tạo ChargingSession gắn với booking đang CheckedIn.
        // ActualStartTime = now - 30 phút (đang sạc thực tế).
        protected static ChargingSession CreateActiveSession(Booking booking, int sessionId = 1)
        {
            var now = DateTime.Now;
            return new ChargingSession
            {
                Id              = sessionId,
                BookingId       = booking.Id,
                Booking         = booking,
                CheckinTime     = now.AddMinutes(-30),
                ActualStartTime = booking.StartTime < now ? booking.StartTime : now.AddMinutes(-30),
                ActualEndTime   = null,
                CreatedAt       = now.AddMinutes(-30)
            };
        }
    }
}
