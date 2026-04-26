using ChargeSlot.Api.Enums;
using ChargeSlot.Api.Models;
using ChargeSlot.Api.DTOs.Payment;
using Moq;

namespace ChargeSlot.Tests.Services.PaymentServiceTests
{
    public class ProcessSePayWebhookTests : PaymentServiceTestBase
    {
        private const int DriverUserId = 5;
        private const int BookingId    = 1;
        private const int SePayId      = 9001;

        // TC01 — Webhook trùng lặp (đã xử lý trước đó) → trả true, không làm gì
        [Fact]
        public async Task Webhook_DuplicateTransaction_ShouldReturnTrueAndSkip()
        {
            _ledgerRepoMock.Setup(x => x.HasTransactionWithMemoAsync($"SePay#{SePayId}"))
                           .ReturnsAsync(true); // đã xử lý rồi

            var request = CreateBookingWebhook(sePayId: SePayId);
            var result  = await CreateService().ProcessSePayWebhookAsync(request);

            Assert.True(result);
            // Không có giao dịch nào được ghi
            _ledgerRepoMock.Verify(x => x.Add(It.IsAny<LedgerTransaction>()), Times.Never);
            _paymentRepoMock.Verify(x => x.Update(It.IsAny<Payment>()), Times.Never);
        }

        // TC02 — transferAmount <= 0 → bỏ qua ngay
        [Fact]
        public async Task Webhook_ZeroAmount_ShouldReturnTrueAndSkip()
        {
            var request = new SePayWebhookRequest
            {
                id             = SePayId,
                content        = "CS1 CHUYEN KHOAN",
                transferAmount = 0m,
                transferType   = "in"
            };

            var result = await CreateService().ProcessSePayWebhookAsync(request);

            Assert.True(result);
            _bookingRepoMock.Verify(x => x.GetByIdWithDetailsAsync(It.IsAny<int>()), Times.Never);
        }

        // TC03 — Nội dung không chứa CS/W → ghi CLEARING + log cảnh báo
        [Fact]
        public async Task Webhook_UnrecognizedContent_ShouldCreditClearingAndReturnTrue()
        {
            var request = new SePayWebhookRequest
            {
                id             = SePayId,
                content        = "CHUC MUNG NAM MOI",  // không có CSxxx hoặc Wxxx
                transferAmount = 100_000m,
                referenceCode  = "FT9999",
                transferType   = "in"
            };

            var result = await CreateService().ProcessSePayWebhookAsync(request);

            Assert.True(result);
            // CLEARING được cộng tiền
            _walletRepoMock.Verify(x => x.AdjustBalanceAtomicAsync(
                ClearingWallet.Id, 100_000m, 0), Times.Once);
            // Ghi ledger UnrecognizedWebhook
            _ledgerRepoMock.Verify(x => x.Add(It.Is<LedgerTransaction>(t =>
                t.ReferenceType == "UnrecognizedWebhook")), Times.Once);
        }

        // TC04 — Nội dung W{userId} → nạp tiền vào ví Driver 5, cộng 300k
        [Fact]
        public async Task Webhook_TopUpContent_ShouldCreditDriverWallet()
        {
            var request = CreateTopUpWebhook(userId: DriverUserId, amount: 300_000m, sePayId: 9002);
            var wallet  = new Wallet { Id = 10, UserId = DriverUserId, AvailableBalance = 0m };

            _walletRepoMock.Setup(x => x.GetByUserIdAsync(DriverUserId)).ReturnsAsync(wallet);

            var result = await CreateService().ProcessSePayWebhookAsync(request);

            Assert.True(result);
            // Ví Driver được cộng tiền (Atomic)
            _walletRepoMock.Verify(x => x.AdjustBalanceAtomicAsync(wallet.Id, 300_000m, 0), Times.Once);
            // Ghi ledger TopUp
            _ledgerRepoMock.Verify(x => x.Add(It.Is<LedgerTransaction>(t =>
                t.ReferenceType == "TopUp")), Times.Once);
            // Notify driver
            _notifyMock.Verify(x => x.SendAsync(
                DriverUserId, It.IsAny<string>(), It.IsAny<string>(), NotificationType.Payment), Times.Once);
            // Commit
            _transactionMock.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        // TC05 — CS{bookingId} hợp lệ, booking PendingPayment, đủ tiền → Paid + slot Booked
        [Fact]
        public async Task Webhook_BookingPayment_Success_ShouldCompletePayment()
        {
            var booking = CreatePendingBooking(driverUserId: DriverUserId, amount: 200_000m);
            var payment = new Payment
            {
                Id = 1, BookingId = BookingId, Amount = 200_000m,
                Status = PaymentStatus.Pending, PaymentMethod = PaymentMethod.BankTransfer
            };
            var trackedSlot = new ChargingSlot { Id = 1, SlotName = "Slot A", Status = SlotStatus.Active };

            _bookingRepoMock.Setup(x => x.GetByIdWithDetailsAsync(BookingId)).ReturnsAsync(booking);
            _paymentRepoMock.Setup(x => x.GetByBookingIdAsync(BookingId)).ReturnsAsync(payment);
            _slotRepoMock.Setup(x => x.GetByIdAsync(booking.SlotId, true)).ReturnsAsync(trackedSlot);

            var request = CreateBookingWebhook(bookingId: BookingId, amount: 200_000m, sePayId: SePayId);
            var result  = await CreateService().ProcessSePayWebhookAsync(request);

            Assert.True(result);

            // Booking = Paid
            Assert.Equal(BookingStatus.Paid, booking.Status);

            // Payment = Completed
            Assert.Equal(PaymentStatus.Completed, payment.Status);
            Assert.NotNull(payment.PaidAt);

            // ESCROW và CLEARING được cộng tiền
            _walletRepoMock.Verify(x => x.AdjustBalanceAtomicAsync(EscrowWallet.Id, 200_000m, 0), Times.Once);
            _walletRepoMock.Verify(x => x.AdjustBalanceAtomicAsync(ClearingWallet.Id, 200_000m, 0), Times.Once);

            // Slot = Booked
            Assert.Equal(SlotStatus.Booked, trackedSlot.Status);

            // Notify driver + owner
            _notifyMock.Verify(x => x.SendAsync(
                DriverUserId, It.IsAny<string>(), It.IsAny<string>(), NotificationType.Payment), Times.Once);
            _notifyMock.Verify(x => x.SendAsync(
                10, It.IsAny<string>(), It.IsAny<string>(), NotificationType.Payment), Times.Once); // ownerUserId = 10

            _transactionMock.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        // TC06 — Chuyển thiếu tiền (cần 200k, chỉ chuyển 100k) → hoàn vào ví Driver
        [Fact]
        public async Task Webhook_InsufficientAmount_ShouldRefundToDriverWallet()
        {
            var booking = CreatePendingBooking(driverUserId: DriverUserId, amount: 200_000m);
            var payment = new Payment
            {
                Id = 1, BookingId = BookingId, Amount = 200_000m, Status = PaymentStatus.Pending
            };
            var driverWallet = new Wallet { Id = 10, UserId = DriverUserId, AvailableBalance = 0m };

            _bookingRepoMock.Setup(x => x.GetByIdWithDetailsAsync(BookingId)).ReturnsAsync(booking);
            _paymentRepoMock.Setup(x => x.GetByBookingIdAsync(BookingId)).ReturnsAsync(payment);
            _walletRepoMock.Setup(x => x.GetByUserIdAsync(DriverUserId)).ReturnsAsync(driverWallet);

            // Chỉ chuyển 100k (thiếu 100k)
            var request = CreateBookingWebhook(bookingId: BookingId, amount: 100_000m, sePayId: SePayId);
            var result  = await CreateService().ProcessSePayWebhookAsync(request);

            Assert.True(result);

            // Booking KHÔNG bị thay đổi status
            Assert.Equal(BookingStatus.PendingPayment, booking.Status);

            // Tiền hoàn vào ví Driver
            _walletRepoMock.Verify(x => x.AdjustBalanceAtomicAsync(driverWallet.Id, 100_000m, 0), Times.Once);

            // Ledger ghi BookingFallbackDeposit
            _ledgerRepoMock.Verify(x => x.Add(It.Is<LedgerTransaction>(t =>
                t.ReferenceType == "BookingFallbackDeposit")), Times.Once);

            // Notify driver về việc tiền được hoàn
            _notifyMock.Verify(x => x.SendAsync(
                DriverUserId, It.IsAny<string>(), It.IsAny<string>(), NotificationType.Payment), Times.Once);
        }

        // TC07 — Payment đã Completed (chuyển lần 2) → hoàn vào ví Driver
        [Fact]
        public async Task Webhook_AlreadyPaid_ShouldRefundDuplicateToDriverWallet()
        {
            var booking = CreatePendingBooking(driverUserId: DriverUserId, amount: 200_000m);
            booking.Status = BookingStatus.Paid; // đã thanh toán

            var payment = new Payment
            {
                Id = 1, BookingId = BookingId, Amount = 200_000m,
                Status = PaymentStatus.Completed,
                GatewayTxnRef = "FT_OLD" // khác ref mới → không phải idempotent
            };
            var driverWallet = new Wallet { Id = 10, UserId = DriverUserId, AvailableBalance = 200_000m };

            _bookingRepoMock.Setup(x => x.GetByIdWithDetailsAsync(BookingId)).ReturnsAsync(booking);
            _paymentRepoMock.Setup(x => x.GetByBookingIdAsync(BookingId)).ReturnsAsync(payment);
            _walletRepoMock.Setup(x => x.GetByUserIdAsync(DriverUserId)).ReturnsAsync(driverWallet);

            // Chuyển lần 2 cùng số tiền 200k
            var request = CreateBookingWebhook(bookingId: BookingId, amount: 200_000m,
                sePayId: SePayId, refCode: "FT_NEW");
            var result = await CreateService().ProcessSePayWebhookAsync(request);

            Assert.True(result);

            // Tiền hoàn về ví Driver (không thanh toán booking lần 2)
            _walletRepoMock.Verify(x => x.AdjustBalanceAtomicAsync(driverWallet.Id, 200_000m, 0), Times.Once);

            // Booking status KHÔNG thay đổi
            Assert.Equal(BookingStatus.Paid, booking.Status);
        }
    }
}
