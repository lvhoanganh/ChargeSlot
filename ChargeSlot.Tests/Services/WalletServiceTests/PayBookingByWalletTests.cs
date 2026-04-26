using ChargeSlot.Api.Enums;
using ChargeSlot.Api.Models;
using Moq;

namespace ChargeSlot.Tests.Services.WalletServiceTests
{
    public class PayBookingByWalletTests : WalletServiceTestBase
    {
        private const int DriverUserId = 5;
        private const int WrongUser    = 99;
        private const int BookingId    = 1;

        // TC01
        [Fact]
        public async Task PayBooking_BookingNotFound_ShouldThrow()
        {
            var wallet = CreateDriverWallet(DriverUserId);
            _walletRepoMock.Setup(x => x.GetByUserIdAsync(DriverUserId)).ReturnsAsync(wallet);
            // bookingRepo default → null

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().PayBookingByWalletAsync(DriverUserId, BookingId));

            Assert.Contains("Booking", ex.Message);
        }

        // TC02
        [Fact]
        public async Task PayBooking_WrongDriver_ShouldThrow()
        {
            var wallet  = CreateDriverWallet(WrongUser);
            var booking = CreatePendingPaymentBooking(driverUserId: DriverUserId); // owner là DriverUserId = 5

            _walletRepoMock.Setup(x => x.GetByUserIdAsync(WrongUser)).ReturnsAsync(wallet);
            _bookingRepoMock.Setup(x => x.GetByIdWithDetailsAsync(BookingId)).ReturnsAsync(booking);

            var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                CreateService().PayBookingByWalletAsync(WrongUser, BookingId)); // WrongUser trả tiền

            Assert.Contains("quyền", ex.Message);
        }

        // TC03
        [Fact]
        public async Task PayBooking_WrongStatus_ShouldThrow()
        {
            var wallet  = CreateDriverWallet(DriverUserId);
            var booking = CreatePendingPaymentBooking(driverUserId: DriverUserId);
            booking.Status = BookingStatus.Paid; // sai status

            _walletRepoMock.Setup(x => x.GetByUserIdAsync(DriverUserId)).ReturnsAsync(wallet);
            _bookingRepoMock.Setup(x => x.GetByIdWithDetailsAsync(BookingId)).ReturnsAsync(booking);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().PayBookingByWalletAsync(DriverUserId, BookingId));

            Assert.Contains("chờ thanh toán", ex.Message);
        }

        // TC04 — PaymentExpiresAt đã qua
        [Fact]
        public async Task PayBooking_PaymentExpired_ShouldThrow()
        {
            var wallet  = CreateDriverWallet(DriverUserId);
            var booking = CreatePendingPaymentBooking(driverUserId: DriverUserId);
            booking.PaymentExpiresAt = DateTime.Now.AddMinutes(-5); // đã hết hạn 5 phút trước

            _walletRepoMock.Setup(x => x.GetByUserIdAsync(DriverUserId)).ReturnsAsync(wallet);
            _bookingRepoMock.Setup(x => x.GetByIdWithDetailsAsync(BookingId)).ReturnsAsync(booking);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().PayBookingByWalletAsync(DriverUserId, BookingId));

            Assert.Contains("hết thời gian", ex.Message);
        }

        // TC05 — Số dư không đủ: booking 200k, ví chỉ có 50k
        [Fact]
        public async Task PayBooking_InsufficientBalance_ShouldThrow()
        {
            var wallet  = CreateDriverWallet(DriverUserId, balance: 50_000m);
            var booking = CreatePendingPaymentBooking(driverUserId: DriverUserId, amount: 200_000m);

            _walletRepoMock.Setup(x => x.GetByUserIdAsync(DriverUserId)).ReturnsAsync(wallet);
            _bookingRepoMock.Setup(x => x.GetByIdWithDetailsAsync(BookingId)).ReturnsAsync(booking);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().PayBookingByWalletAsync(DriverUserId, BookingId));

            Assert.Contains("Số dư ví không đủ", ex.Message);
        }

        // TC06 — Happy path: ví 500k, booking 200k
        // Verify: DeductIfSufficient, ESCROW cộng tiền, booking = Paid, slot = Booked, Payment tạo, notify
        [Fact]
        public async Task PayBooking_Success_ShouldPay200k_AndUpdateState()
        {
            var wallet  = CreateDriverWallet(DriverUserId, balance: 500_000m);
            var booking = CreatePendingPaymentBooking(driverUserId: DriverUserId, amount: 200_000m);

            var trackedSlot = new ChargingSlot
            {
                Id = booking.SlotId, SlotName = "Slot A", Status = SlotStatus.Active
            };

            _walletRepoMock.Setup(x => x.GetByUserIdAsync(DriverUserId)).ReturnsAsync(wallet);
            _bookingRepoMock.Setup(x => x.GetByIdWithDetailsAsync(BookingId)).ReturnsAsync(booking);
            _slotRepoMock.Setup(x => x.GetByIdAsync(booking.SlotId, true)).ReturnsAsync(trackedSlot);

            var result = await CreateService().PayBookingByWalletAsync(DriverUserId, BookingId);

            // Deduct 200k từ ví
            _walletRepoMock.Verify(x => x.DeductIfSufficientAsync(wallet.Id, 200_000m), Times.Once);

            // Cộng ESCROW
            _walletRepoMock.Verify(x => x.AdjustBalanceAtomicAsync(EscrowWallet.Id, 200_000m, 0), Times.Once);

            // Booking = Paid
            Assert.Equal(BookingStatus.Paid, booking.Status);
            _bookingRepoMock.Verify(x => x.Update(booking), Times.Once);

            // Slot = Booked
            Assert.Equal(SlotStatus.Booked, trackedSlot.Status);

            // Payment tạo mới (vì GetByBookingIdAsync → null)
            _paymentRepoMock.Verify(x => x.Add(It.Is<Payment>(p =>
                p.Amount == 200_000m &&
                p.Status == PaymentStatus.Completed &&
                p.PaymentMethod == PaymentMethod.Wallet)), Times.Once);

            // Ledger ghi double-entry
            _ledgerRepoMock.Verify(x => x.Add(It.IsAny<LedgerTransaction>()), Times.Once);

            // Notify driver + owner
            _notifyMock.Verify(x => x.SendAsync(DriverUserId, It.IsAny<string>(),
                It.IsAny<string>(), NotificationType.Payment), Times.Once);
            _notifyMock.Verify(x => x.SendAsync(10, It.IsAny<string>(),
                It.IsAny<string>(), NotificationType.Payment), Times.Once); // ownerUserId = 10

            // Transaction commit
            _transactionMock.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        }
        // TC07 — Race condition: DeductIfSufficient trả 0 (ví bị thay đổi bởi transaction khác)
        [Fact]
        public async Task PayBooking_RaceCondition_DeductFails_ShouldThrow()
        {
            var wallet  = CreateDriverWallet(DriverUserId, balance: 500_000m);
            var booking = CreatePendingPaymentBooking(driverUserId: DriverUserId, amount: 200_000m);

            _walletRepoMock.Setup(x => x.GetByUserIdAsync(DriverUserId)).ReturnsAsync(wallet);
            _bookingRepoMock.Setup(x => x.GetByIdWithDetailsAsync(BookingId)).ReturnsAsync(booking);
            // DeductIfSufficient trả 0 → race condition
            _walletRepoMock.Setup(x => x.DeductIfSufficientAsync(wallet.Id, 200_000m)).ReturnsAsync(0);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().PayBookingByWalletAsync(DriverUserId, BookingId));

            Assert.Contains("Kẹt ví", ex.Message);
            // Rollback phải được gọi
            _transactionMock.Verify(x => x.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        // TC08 — Payment record đã tồn tại → đi qua nhánh Update thay vì Add
        [Fact]
        public async Task PayBooking_ExistingPayment_ShouldUpdateNotCreate()
        {
            var wallet  = CreateDriverWallet(DriverUserId, balance: 500_000m);
            var booking = CreatePendingPaymentBooking(driverUserId: DriverUserId, amount: 200_000m);
            var existingPayment = new Payment
            {
                Id = 99, BookingId = BookingId, Amount = 200_000m,
                Status = PaymentStatus.Pending, PaymentMethod = PaymentMethod.BankTransfer
            };
            var trackedSlot = new ChargingSlot { Id = 1, SlotName = "Slot A", Status = SlotStatus.Active };

            _walletRepoMock.Setup(x => x.GetByUserIdAsync(DriverUserId)).ReturnsAsync(wallet);
            _bookingRepoMock.Setup(x => x.GetByIdWithDetailsAsync(BookingId)).ReturnsAsync(booking);
            _paymentRepoMock.Setup(x => x.GetByBookingIdAsync(BookingId)).ReturnsAsync(existingPayment);
            _slotRepoMock.Setup(x => x.GetByIdAsync(booking.SlotId, true)).ReturnsAsync(trackedSlot);

            var result = await CreateService().PayBookingByWalletAsync(DriverUserId, BookingId);

            // Update, không Add
            _paymentRepoMock.Verify(x => x.Update(It.Is<Payment>(p =>
                p.Status == PaymentStatus.Completed &&
                p.PaymentMethod == PaymentMethod.Wallet)), Times.Once);
            _paymentRepoMock.Verify(x => x.Add(It.IsAny<Payment>()), Times.Never);

            Assert.Equal(BookingStatus.Paid, booking.Status);
        }

        // TC09 — Slot không tìm thấy (null) → vẫn thanh toán thành công (slot null guard)
        [Fact]
        public async Task PayBooking_SlotNull_ShouldStillSucceed()
        {
            var wallet  = CreateDriverWallet(DriverUserId, balance: 500_000m);
            var booking = CreatePendingPaymentBooking(driverUserId: DriverUserId, amount: 200_000m);

            _walletRepoMock.Setup(x => x.GetByUserIdAsync(DriverUserId)).ReturnsAsync(wallet);
            _bookingRepoMock.Setup(x => x.GetByIdWithDetailsAsync(BookingId)).ReturnsAsync(booking);
            // slot returns null (default)

            var result = await CreateService().PayBookingByWalletAsync(DriverUserId, BookingId);

            Assert.Equal(BookingStatus.Paid, booking.Status);
            // Slot.Update KHÔNG được gọi
            _slotRepoMock.Verify(x => x.Update(It.IsAny<ChargingSlot>()), Times.Never);
            _transactionMock.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
