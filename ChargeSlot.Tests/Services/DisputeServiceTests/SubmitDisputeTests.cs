using Moq;
using ChargeSlot.Api.Enums;
using ChargeSlot.Api.Models;

namespace ChargeSlot.Tests.Services.DisputeServiceTests
{
    /// <summary>
    /// Unit tests cho DisputeService.SubmitDisputeAsync — 7 TCs.
    /// </summary>
    public class SubmitDisputeTests : DisputeServiceTestBase
    {
        // ────── ABNORMAL: Validation Failures ──────

        [Fact]
        public async Task TC01_BookingNotFound_ThrowsException()
        {
            // Arrange: default mock returns null for booking
            var svc = CreateService();
            var dto = CreateValidSubmitDto();

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => svc.SubmitDisputeAsync(DriverUserId, dto));
            Assert.Contains("không tồn tại", ex.Message);
        }

        [Fact]
        public async Task TC02_WrongDriver_ThrowsException()
        {
            // Arrange: booking thuộc driver khác
            var booking = CreateCompletedBooking(driverUserId: 99);
            _bookingRepoMock.Setup(x => x.GetByIdWithDetailsAsync(BookingId)).ReturnsAsync(booking);
            var svc = CreateService();

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => svc.SubmitDisputeAsync(DriverUserId, CreateValidSubmitDto()));
            Assert.Contains("không thuộc", ex.Message);
        }

        [Fact]
        public async Task TC03_WrongBookingStatus_ThrowsException()
        {
            // Arrange: booking ở trạng thái Paid, không phải CompletedPendingInvoice
            var booking = CreateCompletedBooking();
            booking.Status = BookingStatus.Paid;
            _bookingRepoMock.Setup(x => x.GetByIdWithDetailsAsync(BookingId)).ReturnsAsync(booking);
            var svc = CreateService();

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => svc.SubmitDisputeAsync(DriverUserId, CreateValidSubmitDto()));
            Assert.Contains("chờ xác nhận", ex.Message);
        }

        [Fact]
        public async Task TC04_DuplicateDispute_ThrowsException()
        {
            // Arrange: đã có dispute cho booking này
            var booking = CreateCompletedBooking();
            _bookingRepoMock.Setup(x => x.GetByIdWithDetailsAsync(BookingId)).ReturnsAsync(booking);
            _disputeRepoMock.Setup(x => x.HasDisputeForBookingAsync(BookingId)).ReturnsAsync(true);
            var svc = CreateService();

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => svc.SubmitDisputeAsync(DriverUserId, CreateValidSubmitDto()));
            Assert.Contains("Đã có", ex.Message);
        }

        [Fact]
        public async Task TC05_RateLimitExceeded_ThrowsException()
        {
            // Arrange: đã đạt giới hạn 3 dispute/tháng
            var booking = CreateCompletedBooking();
            _bookingRepoMock.Setup(x => x.GetByIdWithDetailsAsync(BookingId)).ReturnsAsync(booking);
            _disputeRepoMock.Setup(x => x.GetDisputeCountByDriverInMonthAsync(
                DriverUserId, It.IsAny<DateTime>())).ReturnsAsync(3); // limit = 3
            var svc = CreateService();

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => svc.SubmitDisputeAsync(DriverUserId, CreateValidSubmitDto()));
            Assert.Contains("giới hạn", ex.Message);
        }

        // ────── NORMAL: Happy Paths ──────

        [Fact]
        public async Task TC06_HappyPath_WithInvoice_CreatesDisputeAndFreezes()
        {
            // Arrange
            var booking = CreateCompletedBooking();
            var invoice = CreateInvoice();
            _bookingRepoMock.Setup(x => x.GetByIdWithDetailsAsync(BookingId)).ReturnsAsync(booking);
            _invoiceRepoMock.Setup(x => x.GetByBookingIdAsync(BookingId)).ReturnsAsync(invoice);

            // Assign Id khi Add để LoadDisputeWithDetailsAsync tìm được
            _disputeRepoMock.Setup(x => x.Add(It.IsAny<Dispute>()))
                .Callback<Dispute>(d => d.Id = DisputeId);

            // Return dispute cho MapToDto cuối flow
            var returnDispute = CreatePendingReviewDispute(booking, invoice);
            SetupDisputeReturnForMapping(returnDispute);

            var svc = CreateService();

            // Act
            var result = await svc.SubmitDisputeAsync(DriverUserId, CreateValidSubmitDto());

            // Assert
            Assert.NotNull(result);
            Assert.Equal(BookingId, result.BookingId);

            // Booking → Disputed
            Assert.Equal(BookingStatus.Disputed, booking.Status);

            // Invoice → UnderDispute
            Assert.Equal(InvoiceStatus.UnderDispute, invoice.Status);

            // ESCROW frozen: AdjustBalanceAtomicAsync(escrow, -totalAmount, +totalAmount)
            _walletRepoMock.Verify(x => x.AdjustBalanceAtomicAsync(
                EscrowWallet.Id, -200_000m, 200_000m), Times.Once);

            // Transaction committed
            _transactionMock.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);

            // Dispute added
            _disputeRepoMock.Verify(x => x.Add(It.IsAny<Dispute>()), Times.Once);
        }

        [Fact]
        public async Task TC07_HappyPath_NoInvoice_CreatesDisputeWithoutInvoiceUpdate()
        {
            // Arrange: invoice = null
            var booking = CreateCompletedBooking();
            _bookingRepoMock.Setup(x => x.GetByIdWithDetailsAsync(BookingId)).ReturnsAsync(booking);
            // invoiceRepoMock default returns null

            _disputeRepoMock.Setup(x => x.Add(It.IsAny<Dispute>()))
                .Callback<Dispute>(d => d.Id = DisputeId);

            var returnDispute = CreatePendingReviewDispute(booking);
            SetupDisputeReturnForMapping(returnDispute);

            var svc = CreateService();

            // Act
            var result = await svc.SubmitDisputeAsync(DriverUserId, CreateValidSubmitDto());

            // Assert
            Assert.NotNull(result);
            Assert.Equal(BookingStatus.Disputed, booking.Status);

            // Invoice KHÔNG bị update (vì null)
            _invoiceRepoMock.Verify(x => x.Update(It.IsAny<Invoice>()), Times.Never);

            // Dispute vẫn được tạo
            _disputeRepoMock.Verify(x => x.Add(It.IsAny<Dispute>()), Times.Once);
            _transactionMock.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
