using Moq;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.AspNetCore.Identity;
using ChargeSlot.Api.Services.Implementation;
using ChargeSlot.Api.Models;
using ChargeSlot.Api.Models.Identity;
using ChargeSlot.Api.Enums;
using ChargeSlot.Api.DTOs.Wallet;
using ChargeSlot.Api.Repositories.Interfaces;
using ChargeSlot.Api.Services.Interfaces;
using Microsoft.Extensions.Configuration;

namespace ChargeSlot.Tests.Services.WalletServiceTests
{
    /// <summary>
    /// Base class chứa toàn bộ mock chung cho WalletService tests.
    /// </summary>
    public abstract class WalletServiceTestBase
    {
        protected readonly Mock<IWalletRepository>             _walletRepoMock      = new();
        protected readonly Mock<IBookingRepository>            _bookingRepoMock     = new();
        protected readonly Mock<IPaymentRepository>            _paymentRepoMock     = new();
        protected readonly Mock<IChargingSlotRepository>       _slotRepoMock        = new();
        protected readonly Mock<INotificationService>          _notifyMock          = new();
        protected readonly Mock<IFileStorageService>           _fileStorageMock     = new();
        protected readonly Mock<IUnitOfWork>                   _uowMock             = new();
        protected readonly Mock<IWithdrawRequestRepository>    _withdrawRepoMock    = new();
        protected readonly Mock<IOwnerRepository>              _ownerRepoMock       = new();
        protected readonly Mock<IExtraServiceRepository>       _extraServiceRepoMock= new();
        protected readonly Mock<ILedgerTransactionRepository>  _ledgerRepoMock      = new();
        protected readonly Mock<UserManager<ApplicationUser>>  _userManagerMock;
        protected readonly Mock<IConfiguration>                _configMock          = new();
        protected readonly Mock<ISystemConfigService>          _configServiceMock   = new();
        protected readonly Mock<IDbContextTransaction>         _transactionMock     = new();

        // Ví hệ thống dùng chung
        protected readonly Wallet EscrowWallet   = new() { Id = 100, AvailableBalance = 999_999_999m, SystemCode = "ESCROW" };
        protected readonly Wallet ClearingWallet = new() { Id = 101, AvailableBalance = 999_999_999m, SystemCode = "CLEARING" };

        protected WalletServiceTestBase()
        {
            // ── UserManager ──
            var userStore = new Mock<IUserStore<ApplicationUser>>();
            _userManagerMock = new Mock<UserManager<ApplicationUser>>(
                userStore.Object, null, null, null, null, null, null, null, null);

            // ── UoW ──
            _uowMock.Setup(x => x.CompleteAsync()).ReturnsAsync(1);
            _uowMock.Setup(x => x.BeginTransactionAsync()).ReturnsAsync(_transactionMock.Object);
            _transactionMock.Setup(x => x.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            _transactionMock.Setup(x => x.RollbackAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            // ── Notification ──
            _notifyMock.Setup(x => x.SendAsync(
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<NotificationType>()))
                .Returns(Task.CompletedTask);

            // ── System wallets ──
            _walletRepoMock.Setup(x => x.GetBySystemCodeAsync("ESCROW"))  .ReturnsAsync(EscrowWallet);
            _walletRepoMock.Setup(x => x.GetBySystemCodeAsync("CLEARING")).ReturnsAsync(ClearingWallet);

            // ── Wallet atomic ops: default thành công ──
            _walletRepoMock.Setup(x => x.DeductIfSufficientAsync(It.IsAny<int>(), It.IsAny<decimal>()))
                           .ReturnsAsync(1);
            _walletRepoMock.Setup(x => x.FreezeIfSufficientAsync(It.IsAny<int>(), It.IsAny<decimal>()))
                           .ReturnsAsync(1);
            _walletRepoMock.Setup(x => x.AdjustBalanceAtomicAsync(It.IsAny<int>(), It.IsAny<decimal>(), It.IsAny<decimal>()))
                           .ReturnsAsync(1);
            _walletRepoMock.Setup(x => x.TransferAtomicAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<decimal>()))
                           .Returns(Task.CompletedTask);
            _walletRepoMock.Setup(x => x.Add(It.IsAny<Wallet>()));
            _walletRepoMock.Setup(x => x.AddLedgerTransaction(It.IsAny<LedgerTransaction>()));

            // ── Repository void-return defaults ──
            _paymentRepoMock.Setup(x => x.Add(It.IsAny<Payment>()));
            _paymentRepoMock.Setup(x => x.Update(It.IsAny<Payment>()));
            _bookingRepoMock.Setup(x => x.Update(It.IsAny<Booking>()));
            _withdrawRepoMock.Setup(x => x.Add(It.IsAny<WithdrawRequest>()));
            _withdrawRepoMock.Setup(x => x.Update(It.IsAny<WithdrawRequest>()));
            _ledgerRepoMock.Setup(x => x.Add(It.IsAny<LedgerTransaction>()));
            _slotRepoMock.Setup(x => x.Update(It.IsAny<ChargingSlot>()));

            // ── Defaults: không tìm thấy (tests override khi cần) ──
            _bookingRepoMock.Setup(x => x.GetByIdWithDetailsAsync(It.IsAny<int>()))
                            .ReturnsAsync((Booking?)null);
            _paymentRepoMock.Setup(x => x.GetByBookingIdAsync(It.IsAny<int>()))
                            .ReturnsAsync((Payment?)null);
            _walletRepoMock.Setup(x => x.GetByUserIdAsync(It.IsAny<int>()))
                           .ReturnsAsync((Wallet?)null);
            _ownerRepoMock.Setup(x => x.GetByUserIdAsync(It.IsAny<int>(), It.IsAny<bool>()))
                          .ReturnsAsync((Owner?)null);
            _withdrawRepoMock.Setup(x => x.GetByIdWithUserAndWalletAsync(It.IsAny<int>()))
                             .ReturnsAsync((WithdrawRequest?)null);
            _withdrawRepoMock.Setup(x => x.GetByIdWithUserAsync(It.IsAny<int>()))
                             .ReturnsAsync((WithdrawRequest?)null);

            // ── UserManager defaults ──
            _userManagerMock.Setup(x => x.FindByIdAsync(It.IsAny<string>()))
                            .ReturnsAsync((ApplicationUser?)null);
            _userManagerMock.Setup(x => x.GetUsersInRoleAsync(It.IsAny<string>()))
                            .ReturnsAsync(new List<ApplicationUser>());
            _userManagerMock.Setup(x => x.GetRolesAsync(It.IsAny<ApplicationUser>()))
                            .ReturnsAsync(new List<string>());
        }

        protected WalletService CreateService() => new WalletService(
            _walletRepoMock.Object,
            _bookingRepoMock.Object,
            _paymentRepoMock.Object,
            _slotRepoMock.Object,
            _notifyMock.Object,
            _fileStorageMock.Object,
            _uowMock.Object,
            _withdrawRepoMock.Object,
            _ownerRepoMock.Object,
            _extraServiceRepoMock.Object,
            _ledgerRepoMock.Object,
            _userManagerMock.Object,
            _configMock.Object,
            _configServiceMock.Object);

        // ─── HELPERS ───

        /// <summary>Tạo ví Driver có sẵn số dư.</summary>
        protected static Wallet CreateDriverWallet(int userId = 5, decimal balance = 500_000m) =>
            new Wallet
            {
                Id               = 10,
                UserId           = userId,
                WalletType       = WalletType.Driver,
                AvailableBalance = balance,
                FrozenBalance    = 0m
            };

        /// <summary>Tạo Booking ở PendingPayment, hạn thanh toán còn 30 phút.</summary>
        protected static Booking CreatePendingPaymentBooking(
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
                PaymentExpiresAt = now.AddMinutes(30),  // còn 30 phút
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
                Driver          = new Driver { UserId = driverUserId },
                BookingExtraServices = new List<BookingExtraService>()
            };
        }

        /// <summary>Tạo WithdrawRequest ở trạng thái TransferCompleted.</summary>
        protected static WithdrawRequest CreateTransferCompletedRequest(
            int requestId = 1,
            int userId    = 5,
            decimal amount= 500_000m)
        {
            var wallet = new Wallet
            {
                Id            = 10,
                UserId        = userId,
                WalletType    = WalletType.Owner,
                FrozenBalance = amount   // đang freeze chờ xác nhận
            };
            return new WithdrawRequest
            {
                Id                 = requestId,
                UserId             = userId,
                WalletId           = wallet.Id,
                Wallet             = wallet,
                Amount             = amount,
                BankName           = "Vietcombank",
                BankAccountNumber  = "1234567890",
                BankAccountHolder  = "NGUYEN VAN A",
                Status             = WithdrawStatus.TransferCompleted,
                RequestedAt        = DateTime.Now.AddDays(-1),
                TransferredAt      = DateTime.Now.AddHours(-2),
                User               = new ApplicationUser { Id = userId, FullName = "Nguyen Van A" }
            };
        }

        /// <summary>Tạo WithdrawDto chuẩn.</summary>
        protected static WithdrawDto CreateValidWithdrawDto(decimal amount = 500_000m) =>
            new WithdrawDto
            {
                Amount            = amount,
                BankName          = "Vietcombank",
                BankAccountNumber = "1234567890",
                BankAccountHolder = "NGUYEN VAN A",
                UserNote          = "Rút tiền tháng 4"
            };
    }
}
