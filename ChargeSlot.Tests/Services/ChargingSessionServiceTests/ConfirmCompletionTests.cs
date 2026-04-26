using ChargeSlot.Api.Enums;
using ChargeSlot.Api.Models;
using Moq;

namespace ChargeSlot.Tests.Services.ChargingSessionServiceTests
{
    public class ConfirmCompletionTests : ChargingSessionServiceTestBase
    {
        private const int DriverUserId = 5;
        private const int WrongDriver  = 99;
        private const int SessionId    = 1;

        private Booking CreateCheckedInBooking(decimal totalAmount = 200_000m)
        {
            var now     = DateTime.Now;
            var booking = CreatePaidBooking(
                driverUserId: DriverUserId,
                start: now.AddHours(-2),
                end:   now.AddMinutes(-5));
            booking.Status     = BookingStatus.CompletedPendingInvoice;
            booking.CheckedInAt= now.AddHours(-2);
            booking.TotalAmount= totalAmount;
            return booking;
        }

        // TC01
        [Fact]
        public async Task ConfirmCompletion_WrongDriver_ShouldThrow()
        {
            var booking = CreateCheckedInBooking();
            var session = CreateActiveSession(booking);

            _sessionRepoMock.Setup(x => x.GetByIdWithDetailsAsync(SessionId)).ReturnsAsync(session);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().ConfirmCompletionAsync(WrongDriver, SessionId));

            Assert.Contains("không thuộc", ex.Message);
        }

        // TC02
        [Fact]
        public async Task ConfirmCompletion_WrongStatus_ShouldThrow()
        {
            var booking = CreatePaidBooking(driverUserId: DriverUserId);
            booking.Status = BookingStatus.Paid; // sai trạng thái
            var session    = CreateActiveSession(booking);

            _sessionRepoMock.Setup(x => x.GetByIdWithDetailsAsync(SessionId)).ReturnsAsync(session);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().ConfirmCompletionAsync(DriverUserId, SessionId));

            Assert.Contains("chờ xác nhận", ex.Message);
        }

        // TC03 — Happy path: có CheckedInAt → tích điểm + wallet settlement
        // Booking 200k → earnRate 5% → 10,000 điểm
        // VAT 8% → 16k, Platform 5% → 10k, ownerNet = 174k
        [Fact]
        public async Task ConfirmCompletion_Success_WithLoyaltyAndSettlement()
        {
            var booking = CreateCheckedInBooking(totalAmount: 200_000m);
            var session = CreateActiveSession(booking);

            var invoice = new Invoice
            {
                Id              = 1,
                BookingId       = booking.Id,
                Status          = InvoiceStatus.PendingConfirm,
                TotalAmount     = 200_000m,
                ChargingAmount  = 174_000m,  // gross - vat(16k) - fee(10k)
                VatAmount       = 16_000m,
                PlatformFee     = 10_000m
            };

            _sessionRepoMock.Setup(x => x.GetByIdWithDetailsAsync(SessionId)).ReturnsAsync(session);
            _invoiceRepoMock.Setup(x => x.GetByBookingIdAsync(booking.Id)).ReturnsAsync(invoice);

            // Owner wallet
            var ownerWallet = new Wallet { Id = 200, UserId = 10, AvailableBalance = 0m };
            _walletRepoMock.Setup(x => x.GetByUserIdAsync(10)).ReturnsAsync(ownerWallet);

            var result = await CreateService().ConfirmCompletionAsync(DriverUserId, SessionId);

            // Booking = Completed
            Assert.Equal(BookingStatus.Completed.ToString(), result.Status);

            // Invoice confirmed
            Assert.Equal(InvoiceStatus.Confirmed, invoice.Status);
            _invoiceRepoMock.Verify(x => x.Update(invoice), Times.Once);

            // Loyalty points: 200_000 * 5% = 10_000
            Assert.Equal(10_000m, booking.PointsEarned);
            Assert.Equal(11_000m, booking.Driver.LoyaltyPoints); // 1000 cũ + 10000 mới
            _loyaltyRepoMock.Verify(x => x.Add(It.Is<LoyaltyTransaction>(t =>
                t.Points == 10_000m && t.Type == "Earn")), Times.Once);

            // Wallet settlement: TransferAtomicAsync được gọi ít nhất 2 lần (escrow→owner, escrow→platform)
            _walletRepoMock.Verify(x => x.TransferAtomicAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<decimal>()), Times.AtLeast(2));

            // Transaction commit
            _transactionMock.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        // TC04 — CheckedInAt = null → không tích điểm
        [Fact]
        public async Task ConfirmCompletion_NoCheckinAt_ShouldNotEarnPoints()
        {
            var booking = CreateCheckedInBooking();
            booking.CheckedInAt = null; // no-show scenario

            var session = CreateActiveSession(booking);
            var invoice = new Invoice
            {
                Id = 1, BookingId = booking.Id,
                Status = InvoiceStatus.PendingConfirm,
                TotalAmount = 200_000m, ChargingAmount = 174_000m, VatAmount = 16_000m, PlatformFee = 10_000m
            };

            _sessionRepoMock.Setup(x => x.GetByIdWithDetailsAsync(SessionId)).ReturnsAsync(session);
            _invoiceRepoMock.Setup(x => x.GetByBookingIdAsync(booking.Id)).ReturnsAsync(invoice);
            _walletRepoMock.Setup(x => x.GetByUserIdAsync(10)).ReturnsAsync(new Wallet { Id = 200, UserId = 10 });

            var result = await CreateService().ConfirmCompletionAsync(DriverUserId, SessionId);

            // Booking vẫn Completed
            Assert.Equal(BookingStatus.Completed.ToString(), result.Status);

            // Không tích điểm
            _loyaltyRepoMock.Verify(x => x.Add(It.IsAny<LoyaltyTransaction>()), Times.Never);
            Assert.Equal(0m, booking.PointsEarned);
        }

        // TC05 — Invoice null → skip settlement, booking vẫn Completed
        [Fact]
        public async Task ConfirmCompletion_NoInvoice_ShouldSkipSettlement()
        {
            var booking = CreateCheckedInBooking();
            var session = CreateActiveSession(booking);

            _sessionRepoMock.Setup(x => x.GetByIdWithDetailsAsync(SessionId)).ReturnsAsync(session);
            // invoiceRepoMock → default null

            var result = await CreateService().ConfirmCompletionAsync(DriverUserId, SessionId);

            Assert.Equal(BookingStatus.Completed.ToString(), result.Status);
            // Không gọi TransferAtomic vì invoice null
            _walletRepoMock.Verify(x => x.TransferAtomicAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<decimal>()), Times.Never);
        }
    }
}
