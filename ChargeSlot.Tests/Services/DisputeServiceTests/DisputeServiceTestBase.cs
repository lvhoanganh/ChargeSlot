using Moq;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using ChargeSlot.Api.Services.Implementation;
using ChargeSlot.Api.Models;
using ChargeSlot.Api.Models.Identity;
using ChargeSlot.Api.Enums;
using ChargeSlot.Api.DTOs.Admin;
using ChargeSlot.Api.DTOs.Dispute;
using ChargeSlot.Api.Repositories.Interfaces;
using ChargeSlot.Api.Services.Interfaces;

namespace ChargeSlot.Tests.Services.DisputeServiceTests
{
    /// <summary>
    /// Base class chứa mock + helpers chung cho DisputeService tests.
    /// </summary>
    public abstract class DisputeServiceTestBase
    {
        // ─── Constants ───
        protected const int DriverUserId = 5;
        protected const int OwnerUserId  = 10;
        protected const int AdminUserId  = 1;
        protected const int BookingId    = 1;
        protected const int DisputeId    = 1;
        protected const int StationId    = 1;
        protected const int SlotId       = 1;

        // ─── Mocks: Constructor dependencies ───
        protected readonly Mock<INotificationService>           _notifyMock          = new();
        protected readonly Mock<IUnitOfWork>                     _uowMock             = new();
        protected readonly Mock<IDisputeRepository>              _disputeRepoMock     = new();
        protected readonly Mock<IBookingRepository>              _bookingRepoMock     = new();
        protected readonly Mock<IInvoiceRepository>              _invoiceRepoMock     = new();
        protected readonly Mock<IWalletRepository>               _walletRepoMock      = new();
        protected readonly Mock<ILedgerTransactionRepository>    _ledgerRepoMock      = new();
        protected readonly Mock<IChargingStationRepository>      _stationRepoMock     = new();
        protected readonly Mock<UserManager<ApplicationUser>>    _userManagerMock;
        protected readonly Mock<IFileStorageService>             _fileStorageMock     = new();
        protected readonly Mock<IServiceProvider>                _serviceProviderMock = new();

        // ─── Mocks: Lazy-resolved via IServiceProvider ───
        protected readonly Mock<IBookingService>                 _bookingServiceMock  = new();
        protected readonly Mock<ISystemConfigService>            _configServiceMock   = new();
        protected readonly Mock<IDriverRepository>               _driverRepoMock      = new();
        protected readonly Mock<ILoyaltyTransactionRepository>   _loyaltyRepoMock     = new();

        // ─── Shared objects ───
        protected readonly Mock<IDbContextTransaction>           _transactionMock     = new();

        protected readonly Wallet EscrowWallet   = new() { Id = 100, AvailableBalance = 999_999_999m, FrozenBalance = 0, SystemCode = "ESCROW" };
        protected readonly Wallet PlatformWallet = new() { Id = 101, AvailableBalance = 999_999_999m, FrozenBalance = 0, SystemCode = "PLATFORM_REVENUE" };
        protected readonly Wallet TaxWallet      = new() { Id = 102, AvailableBalance = 999_999_999m, FrozenBalance = 0, SystemCode = "TAX_HOLD" };

        protected DisputeServiceTestBase()
        {
            // ── UserManager ──
            var userStore = new Mock<IUserStore<ApplicationUser>>();
            _userManagerMock = new Mock<UserManager<ApplicationUser>>(
                userStore.Object, null, null, null, null, null, null, null, null);

            // ── UoW + Transaction ──
            _uowMock.Setup(x => x.CompleteAsync()).ReturnsAsync(1);
            _uowMock.Setup(x => x.BeginTransactionAsync()).ReturnsAsync(_transactionMock.Object);
            _transactionMock.Setup(x => x.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            _transactionMock.Setup(x => x.RollbackAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            // ── Notification ──
            _notifyMock.Setup(x => x.SendAsync(
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<NotificationType>()))
                .Returns(Task.CompletedTask);

            // ── System wallets ──
            _walletRepoMock.Setup(x => x.GetBySystemCodeAsync("ESCROW")).ReturnsAsync(EscrowWallet);
            _walletRepoMock.Setup(x => x.GetBySystemCodeAsync("PLATFORM_REVENUE")).ReturnsAsync(PlatformWallet);
            _walletRepoMock.Setup(x => x.GetBySystemCodeAsync("TAX_HOLD")).ReturnsAsync(TaxWallet);

            // ── Wallet atomic ops ──
            _walletRepoMock.Setup(x => x.AdjustBalanceAtomicAsync(It.IsAny<int>(), It.IsAny<decimal>(), It.IsAny<decimal>())).ReturnsAsync(1);
            _walletRepoMock.Setup(x => x.TransferAtomicAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<decimal>())).Returns(Task.CompletedTask);
            _walletRepoMock.Setup(x => x.UnfreezeAtomicAsync(It.IsAny<int>(), It.IsAny<decimal>())).Returns(Task.CompletedTask);
            _walletRepoMock.Setup(x => x.Add(It.IsAny<Wallet>()));

            // ── Repository void defaults ──
            _disputeRepoMock.Setup(x => x.Add(It.IsAny<Dispute>()));
            _disputeRepoMock.Setup(x => x.Update(It.IsAny<Dispute>()));
            _bookingRepoMock.Setup(x => x.Update(It.IsAny<Booking>()));
            _invoiceRepoMock.Setup(x => x.Update(It.IsAny<Invoice>()));
            _ledgerRepoMock.Setup(x => x.Add(It.IsAny<LedgerTransaction>()));
            _stationRepoMock.Setup(x => x.Update(It.IsAny<ChargingStation>()));
            _loyaltyRepoMock.Setup(x => x.Add(It.IsAny<LoyaltyTransaction>()));

            // ── Repository null-defaults (tests override khi cần) ──
            _bookingRepoMock.Setup(x => x.GetByIdWithDetailsAsync(It.IsAny<int>())).ReturnsAsync((Booking?)null);
            _disputeRepoMock.Setup(x => x.GetByIdWithDetailsAsync(It.IsAny<int>())).ReturnsAsync((Dispute?)null);
            _disputeRepoMock.Setup(x => x.HasDisputeForBookingAsync(It.IsAny<int>())).ReturnsAsync(false);
            _disputeRepoMock.Setup(x => x.GetDisputeCountByDriverInMonthAsync(It.IsAny<int>(), It.IsAny<DateTime>())).ReturnsAsync(0);
            _disputeRepoMock.Setup(x => x.GetDriverLoseCountInMonthAsync(It.IsAny<int>(), It.IsAny<DateTime>())).ReturnsAsync(0);
            _disputeRepoMock.Setup(x => x.GetStationLoseCountInMonthAsync(It.IsAny<int>(), It.IsAny<DateTime>())).ReturnsAsync(0);
            _invoiceRepoMock.Setup(x => x.GetByBookingIdAsync(It.IsAny<int>())).ReturnsAsync((Invoice?)null);
            _walletRepoMock.Setup(x => x.GetByUserIdAsync(It.IsAny<int>())).ReturnsAsync((Wallet?)null);

            // ── UserManager defaults ──
            _userManagerMock.Setup(x => x.GetUsersInRoleAsync(It.IsAny<string>())).ReturnsAsync(new List<ApplicationUser>());
            _userManagerMock.Setup(x => x.UpdateAsync(It.IsAny<ApplicationUser>())).ReturnsAsync(IdentityResult.Success);

            // ── Booking cancel (for banning flow) ──
            _bookingRepoMock.Setup(x => x.GetActiveBookingsByDriverAsync(It.IsAny<int>(), It.IsAny<BookingStatus[]>())).ReturnsAsync(new List<Booking>());
            _bookingRepoMock.Setup(x => x.GetActiveBookingsByStationIdsAsync(It.IsAny<List<int>>(), It.IsAny<BookingStatus[]>())).ReturnsAsync(new List<Booking>());
            _bookingServiceMock.Setup(x => x.CancelSystemBookingAsync(It.IsAny<int>(), It.IsAny<string>())).Returns(Task.CompletedTask);

            // ── SystemConfig defaults ──
            _configServiceMock.Setup(x => x.GetCurrentConfigsAsync()).ReturnsAsync(CreateDefaultConfigs());

            // ── Driver repo ──
            _driverRepoMock.Setup(x => x.GetByUserIdAsync(It.IsAny<int>(), It.IsAny<bool>())).ReturnsAsync((Driver?)null);

            // ── IServiceProvider → Lazy<T> resolution ──
            _serviceProviderMock.Setup(x => x.GetService(typeof(IBookingService))).Returns(_bookingServiceMock.Object);
            _serviceProviderMock.Setup(x => x.GetService(typeof(ISystemConfigService))).Returns(_configServiceMock.Object);
            _serviceProviderMock.Setup(x => x.GetService(typeof(IDriverRepository))).Returns(_driverRepoMock.Object);
            _serviceProviderMock.Setup(x => x.GetService(typeof(ILoyaltyTransactionRepository))).Returns(_loyaltyRepoMock.Object);

            // ── FileStorage ──
            _fileStorageMock.Setup(x => x.UploadAsync(It.IsAny<IFormFile>(), It.IsAny<string>())).ReturnsAsync("https://storage.example.com/evidence.jpg");
        }

        protected DisputeService CreateService() => new DisputeService(
            _notifyMock.Object,
            _uowMock.Object,
            _disputeRepoMock.Object,
            _bookingRepoMock.Object,
            _invoiceRepoMock.Object,
            _walletRepoMock.Object,
            _ledgerRepoMock.Object,
            _stationRepoMock.Object,
            _userManagerMock.Object,
            _fileStorageMock.Object,
            _serviceProviderMock.Object);

        // ─────────────── HELPERS ───────────────

        /// <summary>Booking ở CompletedPendingInvoice, kèm đầy đủ navigation chain.</summary>
        protected static Booking CreateCompletedBooking(
            int bookingId      = BookingId,
            int driverUserId   = DriverUserId,
            decimal totalAmount = 200_000m,
            decimal pointsUsed  = 0m,
            decimal pointsDiscount = 0m)
        {
            return new Booking
            {
                Id                    = bookingId,
                DriverUserId          = driverUserId,
                SlotId                = SlotId,
                Status                = BookingStatus.CompletedPendingInvoice,
                TotalAmount           = totalAmount,
                PointsUsed            = pointsUsed,
                PointsDiscountAmount  = pointsDiscount,
                PointsEarned          = 0,
                StartTime             = DateTime.Now.AddHours(-4),
                EndTime               = DateTime.Now.AddHours(-2),
                ChargingSlot = new ChargingSlot
                {
                    Id        = SlotId,
                    StationId = StationId,
                    SlotName  = "Slot A",
                    Status    = SlotStatus.Active,
                    ChargingStation = new ChargingStation
                    {
                        Id                = StationId,
                        Name              = "Trạm Sạc Q9",
                        OwnerUserId       = OwnerUserId,
                        OperationalStatus = OperationalStatus.Active,
                        BanCount          = 0,
                        BannedUntil       = null
                    }
                },
                Driver = new Driver
                {
                    UserId        = driverUserId,
                    LoyaltyPoints = 100,
                    User = new ApplicationUser
                    {
                        Id         = driverUserId,
                        FullName   = "Nguyen Van A",
                        Status     = "ACTIVE",
                        BanCount   = 0,
                        BannedUntil = null
                    }
                },
                BookingExtraServices = new List<BookingExtraService>()
            };
        }

        /// <summary>Invoice chuẩn gắn với booking.</summary>
        protected static Invoice CreateInvoice(
            int bookingId       = BookingId,
            decimal charging    = 160_000m,
            decimal platformFee = 32_000m,
            decimal vat         = 8_000m)
        {
            return new Invoice
            {
                Id              = 1,
                BookingId       = bookingId,
                ChargingAmount  = charging,
                ServiceAmount   = 0,
                VatAmount       = vat,
                PlatformFee     = platformFee,
                TotalAmount     = charging + platformFee + vat,
                Status          = InvoiceStatus.PendingConfirm
            };
        }

        /// <summary>Dispute ở PendingReview, đầy đủ nav chain cho ResolveDispute tests.</summary>
        protected static Dispute CreatePendingReviewDispute(
            Booking booking,
            Invoice? invoice   = null,
            int disputeId      = DisputeId)
        {
            return new Dispute
            {
                Id              = disputeId,
                BookingId       = booking.Id,
                Booking         = booking,
                InvoiceId       = invoice?.Id,
                Invoice         = invoice,
                CreatedByUserId = booking.DriverUserId,
                CreatedByUser   = booking.Driver!.User,
                Reason          = "Sạc không đầy",
                Description     = "Pin chỉ sạc 50%",
                Status          = DisputeStatus.PendingReview,
                StatusChangedAt = DateTime.Now.AddHours(-12),
                CreatedAt       = DateTime.Now.AddHours(-24),
                Evidences       = new List<DisputeEvidence>()
            };
        }

        /// <summary>Config system defaults cho dispute.</summary>
        protected static UpdateSystemConfigsDto CreateDefaultConfigs() => new()
        {
            SecondaryPassword          = "dummy",
            Dispute_Limit_Per_Month    = 3,
            Dispute_OwnerEvidence_Hours = 48,
            Dispute_AdminReview_Hours  = 72,
            RefundPolicy100_Hrs        = 48,
            RefundPolicy50_Hrs         = 24,
            Payment_Expiry_Minutes     = 30,
            CheckIn_Window_Minutes     = 15,
            NoShow_Grace_Minutes       = 30,
            Slot_Buffer_Minutes        = 15,
            VAT_Rate                   = 0.08m,
            Platform_Fee_Rate          = 0.20m,
            Loyalty_Earn_Rate          = 0.05m,
            OTP_Expiry_Minutes         = 5,
            OTP_Cooldown_Seconds       = 60,
            Withdraw_AutoConfirm_Hours = 24,
            Invoice_AutoConfirm_Hours  = 24,
            Reminder_Window_Hours      = 4,
            Min_Booking_Lead_Minutes   = 30
        };

        /// <summary>DTO tạo dispute hợp lệ (không có files).</summary>
        protected static CreateDisputeDto CreateValidSubmitDto() => new()
        {
            BookingId   = BookingId,
            Reason      = "Sạc không đầy",
            Description = "Pin chỉ sạc 50%, yêu cầu hoàn tiền",
            Files       = null
        };

        /// <summary>DTO resolve dispute.</summary>
        protected static ResolveDisputeDto CreateResolveDto(bool driverWins, string note = "Đã xử lý") => new()
        {
            IsDriverWin = driverWins,
            AdminNote   = note
        };

        /// <summary>Setup mock để GetByIdWithDetailsAsync trả Dispute hoàn chỉnh (cho MapToDto cuối flow).</summary>
        protected void SetupDisputeReturnForMapping(Dispute dispute)
        {
            _disputeRepoMock.Setup(x => x.GetByIdWithDetailsAsync(dispute.Id)).ReturnsAsync(dispute);
        }
    }
}
