using Moq;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ChargeSlot.Api.Services.Implementation;
using ChargeSlot.Api.Models;
using ChargeSlot.Api.Enums;
using ChargeSlot.Api.DTOs.Payment;
using ChargeSlot.Api.Repositories.Interfaces;
using ChargeSlot.Api.Services.Interfaces;

namespace ChargeSlot.Tests.Services.PaymentServiceTests
{
    public abstract class PaymentServiceTestBase
    {
        protected readonly Mock<IBookingRepository>           _bookingRepoMock    = new();
        protected readonly Mock<IPaymentRepository>           _paymentRepoMock    = new();
        protected readonly Mock<IChargingSlotRepository>      _slotRepoMock       = new();
        protected readonly Mock<INotificationService>         _notifyMock         = new();
        protected readonly Mock<IWalletRepository>            _walletRepoMock     = new();
        protected readonly Mock<IUnitOfWork>                  _uowMock            = new();
        protected readonly Mock<ILedgerTransactionRepository> _ledgerRepoMock     = new();
        protected readonly Mock<IExtraServiceRepository>      _extraServiceRepoMock= new();
        protected readonly Mock<ILogger<PaymentService>>      _loggerMock         = new();
        protected readonly Mock<IConfiguration>               _configMock         = new();
        protected readonly Mock<ISystemConfigService>         _configServiceMock  = new();
        protected readonly Mock<IDbContextTransaction>        _transactionMock    = new();

        // Ví hệ thống
        protected readonly Wallet EscrowWallet  = new() { Id = 100, SystemCode = "ESCROW",   AvailableBalance = 999_999_999m };
        protected readonly Wallet ClearingWallet= new() { Id = 101, SystemCode = "CLEARING", AvailableBalance = 999_999_999m };

        protected PaymentServiceTestBase()
        {
            // UoW
            _uowMock.Setup(x => x.CompleteAsync()).ReturnsAsync(1);
            _uowMock.Setup(x => x.BeginTransactionAsync()).ReturnsAsync(_transactionMock.Object);
            _transactionMock.Setup(x => x.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            _transactionMock.Setup(x => x.RollbackAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            // Notify 
            _notifyMock.Setup(x => x.SendAsync(
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<NotificationType>()))
                .Returns(Task.CompletedTask);

            // System wallets
            _walletRepoMock.Setup(x => x.GetBySystemCodeAsync("ESCROW"))  .ReturnsAsync(EscrowWallet);
            _walletRepoMock.Setup(x => x.GetBySystemCodeAsync("CLEARING")).ReturnsAsync(ClearingWallet);
            _walletRepoMock.Setup(x => x.AdjustBalanceAtomicAsync(
                It.IsAny<int>(), It.IsAny<decimal>(), It.IsAny<decimal>())).ReturnsAsync(1);
            _walletRepoMock.Setup(x => x.Add(It.IsAny<Wallet>()));
            _walletRepoMock.Setup(x => x.AddLedgerTransaction(It.IsAny<LedgerTransaction>()));

            // Repository defaults
            _paymentRepoMock.Setup(x => x.Add(It.IsAny<Payment>()));
            _paymentRepoMock.Setup(x => x.Update(It.IsAny<Payment>()));
            _bookingRepoMock.Setup(x => x.Update(It.IsAny<Booking>()));
            _slotRepoMock.Setup(x => x.Update(It.IsAny<ChargingSlot>()));
            _ledgerRepoMock.Setup(x => x.Add(It.IsAny<LedgerTransaction>()));

            _bookingRepoMock.Setup(x => x.GetByIdWithDetailsAsync(It.IsAny<int>()))
                            .ReturnsAsync((Booking?)null);
            _paymentRepoMock.Setup(x => x.GetByBookingIdAsync(It.IsAny<int>()))
                            .ReturnsAsync((Payment?)null);
            _walletRepoMock.Setup(x => x.GetByUserIdAsync(It.IsAny<int>()))
                           .ReturnsAsync((Wallet?)null);
            _slotRepoMock.Setup(x => x.GetByIdAsync(It.IsAny<int>(), It.IsAny<bool>()))
                         .ReturnsAsync((ChargingSlot?)null);

            // Ledger idempotency: default = chưa xử lý
            _ledgerRepoMock.Setup(x => x.HasTransactionWithMemoAsync(It.IsAny<string>()))
                           .ReturnsAsync(false);

            // Config SePay 
            _configMock.Setup(x => x["SePay:AccountNumber"]).Returns("1234567890");
            _configMock.Setup(x => x["SePay:BankCode"]).Returns("MB");

            // SystemConfig
            _configServiceMock.Setup(x => x.GetCurrentConfigsAsync())
                .ReturnsAsync(new Api.DTOs.Admin.UpdateSystemConfigsDto
                {
                    Slot_Buffer_Minutes = 15
                });
        }

        protected PaymentService CreateService() => new PaymentService(
            _bookingRepoMock.Object,
            _paymentRepoMock.Object,
            _slotRepoMock.Object,
            _notifyMock.Object,
            _walletRepoMock.Object,
            _uowMock.Object,
            _ledgerRepoMock.Object,
            _extraServiceRepoMock.Object,
            _loggerMock.Object,
            _configMock.Object,
            _configServiceMock.Object);

        //  HELPERS 

        // Tạo booking PendingPayment với hạn 30 phút.
        protected static Booking CreatePendingBooking(
            int bookingId    = 1,
            int driverUserId = 5,
            decimal amount   = 200_000m)
        {
            var now = DateTime.Now;
            return new Booking
            {
                Id               = bookingId,
                DriverUserId     = driverUserId,
                SlotId           = 1,
                Status           = BookingStatus.PendingPayment,
                TotalAmount      = amount,
                PaymentExpiresAt = now.AddMinutes(30),
                StartTime        = now.AddHours(2),
                EndTime          = now.AddHours(4),
                ChargingSlot     = new ChargingSlot
                {
                    Id       = 1,
                    SlotName = "Slot A",
                    Status   = SlotStatus.Active,
                    ChargingStation = new ChargingStation
                    {
                        Id          = 1,
                        Name        = "Trạm Sạc Q9",
                        OwnerUserId = 10
                    }
                },
                BookingExtraServices = new List<BookingExtraService>()
            };
        }

        // Tạo webhook SePay chuẩn với nội dung CS{bookingId}.
        protected static SePayWebhookRequest CreateBookingWebhook(
            int bookingId      = 1,
            decimal amount     = 200_000m,
            int sePayId        = 9001,
            string? refCode    = "FT2404210001")
        => new SePayWebhookRequest
        {
            id             = sePayId,
            content        = $"CHUYEN KHOAN CS{bookingId} THANH TOAN",
            transferAmount = amount,
            referenceCode  = refCode,
            transferType   = "in"
        };

        // Tạo webhook SePay nạp ví W{userId}.
        protected static SePayWebhookRequest CreateTopUpWebhook(
            int userId      = 5,
            decimal amount  = 300_000m,
            int sePayId     = 9002)
        => new SePayWebhookRequest
        {
            id             = sePayId,
            content        = $"NAP VI W{userId} QUA VIETQR",
            transferAmount = amount,
            referenceCode  = "FT2404210002",
            transferType   = "in"
        };
    }
}
