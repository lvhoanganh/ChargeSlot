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
    /// <summary>
    /// Unit tests cho WalletService - các luồng chính:
    ///   1. Lấy / tạo ví (GetOrCreateWalletAsync)
    ///   2. Thanh toán booking bằng ví (PayBookingByWalletAsync)
    ///   3. Rút tiền (WithdrawAsync)
    ///   4. Callback nạp tiền VNPay (ProcessTopUpCallbackAsync)
    /// </summary>
    public class WalletServiceTests
    {
        private readonly Mock<IWalletRepository>       _walletRepo  = new();
        private readonly Mock<IBookingRepository>      _bookingRepo = new();
        private readonly Mock<IPaymentRepository>      _paymentRepo = new();
        private readonly Mock<IChargingSlotRepository> _slotRepo    = new();
        private readonly Mock<IVnPayService>           _vnPay       = new();
        private readonly Mock<INotificationService>    _noti        = new();

        private readonly WalletService _service;

        public WalletServiceTests()
        {
            _service = new WalletService(
                _walletRepo.Object,
                _bookingRepo.Object,
                _paymentRepo.Object,
                _slotRepo.Object,
                _vnPay.Object,
                _noti.Object);
        }

        // ─────────────────────────────────────────────
        // GET OR CREATE WALLET
        // ─────────────────────────────────────────────

        /// <summary>
        /// Ví đã tồn tại → trả về WalletDto, không tạo mới.
        /// </summary>
        [Fact]
        public async Task GetOrCreateWallet_ShouldReturnExistingWallet()
        {
            var wallet = new Wallet
            {
                Id               = 1,
                UserId           = 10,
                WalletType       = WalletType.Driver,
                AvailableBalance = 500,
                FrozenBalance    = 0
            };

            _walletRepo.Setup(x => x.GetByUserIdAsync(10)).ReturnsAsync(wallet);

            var result = await _service.GetOrCreateWalletAsync(10);

            Assert.Equal(500, result.AvailableBalance);
            Assert.Equal(WalletType.Driver.ToString(), result.WalletType);

            _walletRepo.Verify(x => x.CreateAsync(It.IsAny<Wallet>()), Times.Never);
        }

        /// <summary>
        /// Ví chưa tồn tại → tạo mới với balance = 0.
        /// </summary>
        [Fact]
        public async Task GetOrCreateWallet_ShouldCreateWallet_WhenNotFound()
        {
            _walletRepo.Setup(x => x.GetByUserIdAsync(20)).ReturnsAsync((Wallet?)null);

            _walletRepo
                .Setup(x => x.CreateAsync(It.IsAny<Wallet>()))
                .ReturnsAsync((Wallet w) => w); // trả lại đối tượng đã tạo

            var result = await _service.GetOrCreateWalletAsync(20);

            Assert.Equal(0, result.AvailableBalance);
            Assert.Equal(0, result.FrozenBalance);

            _walletRepo.Verify(x => x.CreateAsync(It.Is<Wallet>(w =>
                w.UserId           == 20 &&
                w.AvailableBalance == 0
            )), Times.Once);
        }

        // ─────────────────────────────────────────────
        // PAY BOOKING BY WALLET
        // ─────────────────────────────────────────────

        /// <summary>
        /// ✅ Happy path: ví đủ tiền, booking hợp lệ →
        ///   - Trừ tiền ví
        ///   - Booking = Paid
        ///   - Slot = Booked
        ///   - Tạo Payment record
        ///   - Ghi ledger
        ///   - Gửi notification
        /// </summary>
        [Fact]
        public async Task PayBookingByWallet_ShouldSuccess()
        {
            var wallet = new Wallet
            {
                Id               = 1,
                UserId           = 10,
                AvailableBalance = 500
            };

            var booking = new Booking
            {
                Id           = 1,
                DriverUserId = 10,
                Status       = BookingStatus.PendingPayment,
                TotalAmount  = 200,
                SlotId       = 5
            };

            var slot = new ChargingSlot { Id = 5, Status = SlotStatus.Active };

            _walletRepo.Setup(x => x.GetByUserIdAsync(10)).ReturnsAsync(wallet);
            _bookingRepo.Setup(x => x.GetByIdWithDetailsAsync(1)).ReturnsAsync(booking);
            _paymentRepo.Setup(x => x.GetByBookingIdAsync(1)).ReturnsAsync((Payment?)null);
            _slotRepo.Setup(x => x.GetByIdAsync(5, true)).ReturnsAsync(slot);

            var result = await _service.PayBookingByWalletAsync(userId: 10, bookingId: 1);

            // Trừ tiền ví
            Assert.Equal(300, wallet.AvailableBalance); // 500 - 200
            Assert.Equal(BookingStatus.Paid, booking.Status);
            Assert.Equal(SlotStatus.Booked, slot.Status);

            // Tạo payment mới với method = Wallet
            _paymentRepo.Verify(x => x.CreateAsync(It.Is<Payment>(p =>
                p.BookingId     == 1 &&
                p.Amount        == 200 &&
                p.PaymentMethod == PaymentMethod.Wallet &&
                p.Status        == PaymentStatus.Completed
            )), Times.Once);

            // Ghi ledger 1 lần
            _walletRepo.Verify(x => x.AddLedgerTransactionAsync(It.IsAny<LedgerTransaction>()), Times.Once);

            // Update ví
            _walletRepo.Verify(x => x.UpdateAsync(wallet), Times.Once);

            // Update booking
            _bookingRepo.Verify(x => x.UpdateAsync(booking), Times.Once);

            // Slot bị lock
            _slotRepo.Verify(x => x.Update(slot), Times.Once);
            _slotRepo.Verify(x => x.SaveChangesAsync(), Times.Once);

            // Notify driver
            _noti.Verify(x => x.SendAsync(
                10,
                It.IsAny<string>(),
                It.IsAny<string>(),
                NotificationType.Payment), Times.Once);
        }

        /// <summary>
        /// Ví không đủ tiền → throw InvalidOperationException, không trừ tiền.
        /// </summary>
        [Fact]
        public async Task PayBookingByWallet_ShouldFail_WhenInsufficientBalance()
        {
            var wallet = new Wallet { Id = 1, UserId = 10, AvailableBalance = 100 };

            var booking = new Booking
            {
                DriverUserId = 10,
                Status       = BookingStatus.PendingPayment,
                TotalAmount  = 500 // cần 500, chỉ có 100
            };

            _walletRepo.Setup(x => x.GetByUserIdAsync(10)).ReturnsAsync(wallet);
            _bookingRepo.Setup(x => x.GetByIdWithDetailsAsync(1)).ReturnsAsync(booking);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.PayBookingByWalletAsync(10, 1));

            // Ví không bị thay đổi
            Assert.Equal(100, wallet.AvailableBalance);
            _walletRepo.Verify(x => x.UpdateAsync(It.IsAny<Wallet>()), Times.Never);
        }

        /// <summary>
        /// User không phải chủ booking → throw UnauthorizedAccessException.
        /// </summary>
        [Fact]
        public async Task PayBookingByWallet_ShouldFail_WhenNotBookingOwner()
        {
            var wallet  = new Wallet { UserId = 10, AvailableBalance = 1000 };
            var booking = new Booking { DriverUserId = 99, Status = BookingStatus.PendingPayment };

            _walletRepo.Setup(x => x.GetByUserIdAsync(10)).ReturnsAsync(wallet);
            _bookingRepo.Setup(x => x.GetByIdWithDetailsAsync(1)).ReturnsAsync(booking);

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                _service.PayBookingByWalletAsync(10, 1));
        }

        /// <summary>
        /// Booking không ở PendingPayment → throw InvalidOperationException.
        /// </summary>
        [Theory]
        [InlineData(BookingStatus.WaitingOwner)]
        [InlineData(BookingStatus.Paid)]
        [InlineData(BookingStatus.Completed)]
        public async Task PayBookingByWallet_ShouldFail_WhenBookingNotPendingPayment(BookingStatus status)
        {
            var wallet  = new Wallet { UserId = 10, AvailableBalance = 1000 };
            var booking = new Booking { DriverUserId = 10, Status = status };

            _walletRepo.Setup(x => x.GetByUserIdAsync(10)).ReturnsAsync(wallet);
            _bookingRepo.Setup(x => x.GetByIdWithDetailsAsync(1)).ReturnsAsync(booking);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.PayBookingByWalletAsync(10, 1));
        }

        /// <summary>
        /// Đã hết hạn thanh toán → throw InvalidOperationException.
        /// </summary>
        [Fact]
        public async Task PayBookingByWallet_ShouldFail_WhenPaymentDeadlineExpired()
        {
            var wallet = new Wallet { UserId = 10, AvailableBalance = 1000 };
            var booking = new Booking
            {
                DriverUserId     = 10,
                Status           = BookingStatus.PendingPayment,
                TotalAmount      = 100,
                PaymentExpiresAt = DateTime.UtcNow.AddMinutes(-5) // đã hết hạn
            };

            _walletRepo.Setup(x => x.GetByUserIdAsync(10)).ReturnsAsync(wallet);
            _bookingRepo.Setup(x => x.GetByIdWithDetailsAsync(1)).ReturnsAsync(booking);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.PayBookingByWalletAsync(10, 1));
        }

        /// <summary>
        /// Booking không tồn tại → throw InvalidOperationException.
        /// </summary>
        [Fact]
        public async Task PayBookingByWallet_ShouldFail_WhenBookingNotFound()
        {
            var wallet = new Wallet { UserId = 10, AvailableBalance = 1000 };

            _walletRepo.Setup(x => x.GetByUserIdAsync(10)).ReturnsAsync(wallet);
            _bookingRepo.Setup(x => x.GetByIdWithDetailsAsync(999)).ReturnsAsync((Booking?)null);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.PayBookingByWalletAsync(10, 999));
        }

        /// <summary>
        /// Đã có Payment record (Pending) → cập nhật status thay vì tạo mới.
        /// </summary>
        [Fact]
        public async Task PayBookingByWallet_ShouldUpdateExistingPayment_NotCreateNew()
        {
            var wallet  = new Wallet { Id = 1, UserId = 10, AvailableBalance = 500 };
            var booking = new Booking
            {
                Id           = 1,
                DriverUserId = 10,
                Status       = BookingStatus.PendingPayment,
                TotalAmount  = 200,
                SlotId       = 5
            };

            var existingPayment = new Payment
            {
                BookingId = 1,
                Status    = PaymentStatus.Pending
            };

            _walletRepo.Setup(x => x.GetByUserIdAsync(10)).ReturnsAsync(wallet);
            _bookingRepo.Setup(x => x.GetByIdWithDetailsAsync(1)).ReturnsAsync(booking);
            _paymentRepo.Setup(x => x.GetByBookingIdAsync(1)).ReturnsAsync(existingPayment);
            _slotRepo.Setup(x => x.GetByIdAsync(5, true)).ReturnsAsync(new ChargingSlot { Id = 5 });

            await _service.PayBookingByWalletAsync(10, 1);

            // Cập nhật payment cũ
            _paymentRepo.Verify(x => x.UpdateAsync(It.Is<Payment>(p =>
                p.Status == PaymentStatus.Completed &&
                p.PaymentMethod == PaymentMethod.Wallet
            )), Times.Once);

            // Không tạo payment mới
            _paymentRepo.Verify(x => x.CreateAsync(It.IsAny<Payment>()), Times.Never);
        }

        // ─────────────────────────────────────────────
        // WITHDRAW
        // ─────────────────────────────────────────────

        /// <summary>
        /// ✅ Happy path: ví đủ tiền → tiền bị freeze, ghi ledger, notify.
        /// </summary>
        [Fact]
        public async Task Withdraw_ShouldSuccess_AndFreezeBalance()
        {
            var wallet = new Wallet
            {
                Id               = 1,
                UserId           = 10,
                AvailableBalance = 500,
                FrozenBalance    = 0
            };

            _walletRepo.Setup(x => x.GetByUserIdAsync(10)).ReturnsAsync(wallet);

            var result = await _service.WithdrawAsync(userId: 10, amount: 200);

            Assert.Equal(300, wallet.AvailableBalance); // 500 - 200
            Assert.Equal(200, wallet.FrozenBalance);     // frozen tăng 200

            _walletRepo.Verify(x => x.UpdateAsync(wallet), Times.Once);
            _walletRepo.Verify(x => x.AddLedgerTransactionAsync(It.Is<LedgerTransaction>(tx =>
                tx.ReferenceType == "Withdraw"
            )), Times.Once);

            _noti.Verify(x => x.SendAsync(
                10,
                It.IsAny<string>(),
                It.IsAny<string>(),
                NotificationType.System), Times.Once);
        }

        /// <summary>
        /// ❌ Ví không đủ tiền để rút → throw InvalidOperationException.
        /// Ví không bị thay đổi.
        /// </summary>
        [Fact]
        public async Task Withdraw_ShouldFail_WhenInsufficientBalance()
        {
            var wallet = new Wallet
            {
                UserId           = 10,
                AvailableBalance = 100,
                FrozenBalance    = 0
            };

            _walletRepo.Setup(x => x.GetByUserIdAsync(10)).ReturnsAsync(wallet);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.WithdrawAsync(10, 200));

            // Ví không thay đổi
            Assert.Equal(100, wallet.AvailableBalance);
            Assert.Equal(0, wallet.FrozenBalance);
            _walletRepo.Verify(x => x.UpdateAsync(It.IsAny<Wallet>()), Times.Never);
        }

        /// <summary>
        /// Rút toàn bộ số dư → AvailableBalance = 0, FrozenBalance = số rút.
        /// </summary>
        [Fact]
        public async Task Withdraw_ShouldAllowWithdrawingEntireBalance()
        {
            var wallet = new Wallet
            {
                Id               = 1,
                UserId           = 10,
                AvailableBalance = 300,
                FrozenBalance    = 0
            };

            _walletRepo.Setup(x => x.GetByUserIdAsync(10)).ReturnsAsync(wallet);

            await _service.WithdrawAsync(10, 300); // rút hết

            Assert.Equal(0, wallet.AvailableBalance);
            Assert.Equal(300, wallet.FrozenBalance);
        }

        // ─────────────────────────────────────────────
        // PROCESS TOP-UP CALLBACK (VNPay)
        // ─────────────────────────────────────────────

        /// <summary>
        /// ✅ Happy path: nạp tiền thành công →
        ///   - Cộng tiền vào ví
        ///   - Ghi ledger CREDIT
        ///   - Notify user
        /// </summary>
        [Fact]
        public async Task TopUpCallback_ShouldSuccess_AndCreditWallet()
        {
            var wallet = new Wallet
            {
                Id               = 1,
                UserId           = 10,
                AvailableBalance = 100
            };

            // txnRef format: {walletId * -1}_{ticks} → "-1_123"
            _vnPay
                .Setup(x => x.ValidateCallback(It.IsAny<IQueryCollection>()))
                .Returns((true, "00", "-1_999999"));

            _walletRepo.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(wallet);

            // vnp_Amount = 50000 (× 100 theo VNPay format) → 500 VND
            var query = new QueryCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
            {
                { "vnp_Amount", "50000" }
            });

            await _service.ProcessTopUpCallbackAsync(query);

            // Nạp 500 VND → balance: 100 + 500 = 600
            Assert.Equal(600, wallet.AvailableBalance);

            _walletRepo.Verify(x => x.UpdateAsync(wallet), Times.Once);

            // Ghi ledger CREDIT
            _walletRepo.Verify(x => x.AddLedgerTransactionAsync(It.Is<LedgerTransaction>(tx =>
                tx.ReferenceType == "TopUp" &&
                tx.Entries.Any(e => e.Direction == LedgerDirection.Credit && e.Amount == 500)
            )), Times.Once);

            // Notify
            _noti.Verify(x => x.SendAsync(
                10,
                It.IsAny<string>(),
                It.IsAny<string>(),
                NotificationType.Payment), Times.Once);
        }

        /// <summary>
        /// Signature không hợp lệ → không xử lý gì (return early).
        /// </summary>
        [Fact]
        public async Task TopUpCallback_ShouldIgnore_WhenSignatureInvalid()
        {
            _vnPay
                .Setup(x => x.ValidateCallback(It.IsAny<IQueryCollection>()))
                .Returns((false, "99", "-1_123"));

            await _service.ProcessTopUpCallbackAsync(new QueryCollection());

            _walletRepo.Verify(x => x.UpdateAsync(It.IsAny<Wallet>()), Times.Never);
        }

        /// <summary>
        /// responseCode != "00" (thanh toán thất bại) → không cộng tiền.
        /// </summary>
        [Fact]
        public async Task TopUpCallback_ShouldIgnore_WhenResponseCodeFailed()
        {
            _vnPay
                .Setup(x => x.ValidateCallback(It.IsAny<IQueryCollection>()))
                .Returns((true, "24", "-1_123")); // user cancel

            await _service.ProcessTopUpCallbackAsync(new QueryCollection());

            _walletRepo.Verify(x => x.UpdateAsync(It.IsAny<Wallet>()), Times.Never);
        }

        /// <summary>
        /// txnRef không phải số âm (không phải top-up) → bỏ qua.
        /// </summary>
        [Fact]
        public async Task TopUpCallback_ShouldIgnore_WhenWalletIdIsPositive()
        {
            // txnRef "5_123" → walletId âm = -5, walletId = 5 (positive = booking id, không phải topup)
            // thực ra logic: walletId = negativeWalletId * -1. Nếu negativeWalletId = 5 thì walletId = -5 <= 0 → return
            _vnPay
                .Setup(x => x.ValidateCallback(It.IsAny<IQueryCollection>()))
                .Returns((true, "00", "5_123")); // 5 * -1 = -5 <= 0 → bỏ qua

            await _service.ProcessTopUpCallbackAsync(new QueryCollection());

            _walletRepo.Verify(x => x.GetByIdAsync(It.IsAny<int>()), Times.Never);
        }

        /// <summary>
        /// Wallet không tồn tại → bỏ qua, không cộng tiền.
        /// </summary>
        [Fact]
        public async Task TopUpCallback_ShouldIgnore_WhenWalletNotFound()
        {
            _vnPay
                .Setup(x => x.ValidateCallback(It.IsAny<IQueryCollection>()))
                .Returns((true, "00", "-3_123")); // walletId = 3

            _walletRepo.Setup(x => x.GetByIdAsync(3)).ReturnsAsync((Wallet?)null);

            var query = new QueryCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
            {
                { "vnp_Amount", "10000" }
            });

            await _service.ProcessTopUpCallbackAsync(query);

            _walletRepo.Verify(x => x.UpdateAsync(It.IsAny<Wallet>()), Times.Never);
        }

        // ─────────────────────────────────────────────
        // GET TRANSACTION HISTORY
        // ─────────────────────────────────────────────

        /// <summary>
        /// Lịch sử giao dịch trả về đúng số lượng entries đã map sang DTO.
        /// </summary>
        [Fact]
        public async Task GetTransactionHistory_ShouldReturnMappedEntries()
        {
            var wallet = new Wallet { Id = 1, UserId = 10, AvailableBalance = 500 };

            var tx = new LedgerTransaction
            {
                ReferenceType = "TopUp",
                Memo          = "Nạp tiền"
            };

            var entries = new List<LedgerEntry>
            {
                new LedgerEntry
                {
                    Id                = 1,
                    Direction         = LedgerDirection.Credit,
                    Amount            = 200,
                    CreatedAt         = DateTime.UtcNow.AddHours(-1),
                    LedgerTransaction = tx
                },
                new LedgerEntry
                {
                    Id                = 2,
                    Direction         = LedgerDirection.Debit,
                    Amount            = 50,
                    CreatedAt         = DateTime.UtcNow,
                    LedgerTransaction = tx
                }
            };

            _walletRepo.Setup(x => x.GetByUserIdAsync(10)).ReturnsAsync(wallet);
            _walletRepo.Setup(x => x.GetTransactionHistoryAsync(1, It.IsAny<int>())).ReturnsAsync(entries);

            var result = await _service.GetTransactionHistoryAsync(10);

            Assert.Equal(2, result.Count);
            Assert.Equal("Credit", result[0].Direction);
            Assert.Equal(200, result[0].Amount);
            Assert.Equal("Debit", result[1].Direction);
        }
    }
}
