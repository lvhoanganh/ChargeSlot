using Moq;
using ChargeSlot.Api.Enums;
using ChargeSlot.Api.Models;
using ChargeSlot.Api.Models.Identity;

namespace ChargeSlot.Tests.Services.DisputeServiceTests
{
    /// <summary>
    /// Unit tests cho DisputeService.ResolveDisputeAsync — 9 TCs.
    /// </summary>
    public class ResolveDisputeTests : DisputeServiceTestBase
    {
        // ────── ABNORMAL: Validation Failures ──────

        [Fact]
        public async Task TC01_DisputeNotFound_ThrowsException()
        {
            // Arrange: default returns null
            var svc = CreateService();

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => svc.ResolveDisputeAsync(AdminUserId, DisputeId, CreateResolveDto(true)));
            Assert.Contains("không tồn tại", ex.Message);
        }

        [Fact]
        public async Task TC02_StatusNotPendingReview_ThrowsException()
        {
            // Arrange: dispute ở WaitingOwnerEvidence, chưa sẵn sàng
            var booking = CreateCompletedBooking();
            var dispute = CreatePendingReviewDispute(booking);
            dispute.Status = DisputeStatus.WaitingOwnerEvidence;

            _disputeRepoMock.Setup(x => x.GetByIdWithDetailsAsync(DisputeId)).ReturnsAsync(dispute);
            var svc = CreateService();

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => svc.ResolveDisputeAsync(AdminUserId, DisputeId, CreateResolveDto(true)));
            Assert.Contains("chưa sẵn sàng", ex.Message);
        }

        // ────── NORMAL: Driver Wins ──────

        [Fact]
        public async Task TC03_DriverWins_RefundToDriver()
        {
            // Arrange
            var booking = CreateCompletedBooking(totalAmount: 200_000m);
            var invoice = CreateInvoice();
            var dispute = CreatePendingReviewDispute(booking, invoice);

            _disputeRepoMock.Setup(x => x.GetByIdWithDetailsAsync(DisputeId)).ReturnsAsync(dispute);

            var svc = CreateService();

            // Act
            var result = await svc.ResolveDisputeAsync(AdminUserId, DisputeId, CreateResolveDto(true));

            // Assert: Dispute → ResolvedRefund
            Assert.Equal(DisputeStatus.ResolvedRefund, dispute.Status);
            Assert.Equal(AdminUserId, dispute.ResolvedByUserId);
            Assert.NotNull(dispute.ResolvedAt);

            // Invoice → Resolved
            Assert.Equal(InvoiceStatus.Resolved, invoice.Status);

            // Booking → Completed
            Assert.Equal(BookingStatus.Completed, booking.Status);

            // ESCROW unfrozen + Driver credited (RefundToDriverAsync)
            _walletRepoMock.Verify(x => x.AdjustBalanceAtomicAsync(
                EscrowWallet.Id, 0, -200_000m), Times.Once);

            // Ledger ghi Refund
            _ledgerRepoMock.Verify(x => x.Add(It.Is<LedgerTransaction>(
                t => t.ReferenceType == "Refund")), Times.Once);

            // Transaction committed
            _transactionMock.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        // ────── NORMAL: Owner Wins ──────

        [Fact]
        public async Task TC04_OwnerWins_SettleToOwner()
        {
            // Arrange
            var booking = CreateCompletedBooking(totalAmount: 200_000m);
            var invoice = CreateInvoice(charging: 160_000m, platformFee: 32_000m, vat: 8_000m);
            var dispute = CreatePendingReviewDispute(booking, invoice);

            _disputeRepoMock.Setup(x => x.GetByIdWithDetailsAsync(DisputeId)).ReturnsAsync(dispute);

            // Owner wallet
            var ownerWallet = new Wallet { Id = 20, UserId = OwnerUserId, WalletType = WalletType.Owner };
            _walletRepoMock.Setup(x => x.GetByUserIdAsync(OwnerUserId)).ReturnsAsync(ownerWallet);

            var svc = CreateService();

            // Act
            var result = await svc.ResolveDisputeAsync(AdminUserId, DisputeId, CreateResolveDto(false));

            // Assert: Dispute → ResolvedPayout
            Assert.Equal(DisputeStatus.ResolvedPayout, dispute.Status);

            // Booking → Completed
            Assert.Equal(BookingStatus.Completed, booking.Status);

            // ESCROW → Owner (net charging amount)
            _walletRepoMock.Verify(x => x.TransferAtomicAsync(
                EscrowWallet.Id, ownerWallet.Id, 160_000m), Times.Once);

            // ESCROW → Platform (fee)
            _walletRepoMock.Verify(x => x.TransferAtomicAsync(
                EscrowWallet.Id, PlatformWallet.Id, 32_000m), Times.Once);

            // ESCROW → Tax (VAT)
            _walletRepoMock.Verify(x => x.TransferAtomicAsync(
                EscrowWallet.Id, TaxWallet.Id, 8_000m), Times.Once);

            // Unfreeze ESCROW trước settle
            _walletRepoMock.Verify(x => x.UnfreezeAtomicAsync(
                EscrowWallet.Id, It.IsAny<decimal>()), Times.Once);

            // Ledger entries (DisputeSettlement + PlatformFee + TaxHold)
            _ledgerRepoMock.Verify(x => x.Add(It.IsAny<LedgerTransaction>()), Times.AtLeast(3));
        }

        // ────── BOUNDARY: Loyalty Points Refund ──────

        [Fact]
        public async Task TC05_DriverWins_WithLoyaltyPoints_RefundsPoints()
        {
            // Arrange: booking dùng 50 điểm
            var booking = CreateCompletedBooking(totalAmount: 180_000m, pointsUsed: 50m);
            var dispute = CreatePendingReviewDispute(booking);

            _disputeRepoMock.Setup(x => x.GetByIdWithDetailsAsync(DisputeId)).ReturnsAsync(dispute);

            // Driver repo trả driver entity (cho loyalty refund)
            var driver = booking.Driver!;
            driver.LoyaltyPoints = 100m; // trước refund
            _driverRepoMock.Setup(x => x.GetByUserIdAsync(DriverUserId, true)).ReturnsAsync(driver);

            var svc = CreateService();

            // Act
            await svc.ResolveDisputeAsync(AdminUserId, DisputeId, CreateResolveDto(true));

            // Assert: 100 + 50 = 150 điểm
            Assert.Equal(150m, driver.LoyaltyPoints);

            // LoyaltyTransaction created với Type = "Refund"
            _loyaltyRepoMock.Verify(x => x.Add(It.Is<LoyaltyTransaction>(
                lt => lt.Type == "Refund" && lt.Points == 50m)), Times.Once);
        }

        // ────── BOUNDARY: Banning — Driver Loses >= 3 ──────

        [Fact]
        public async Task TC06_DriverLoses_ExceedsThreshold_Suspended()
        {
            // Arrange: IsDriverWin = false, driver đã thua 3 lần trong tháng
            var booking = CreateCompletedBooking(totalAmount: 200_000m);
            var dispute = CreatePendingReviewDispute(booking); // invoice = null → skip settle

            _disputeRepoMock.Setup(x => x.GetByIdWithDetailsAsync(DisputeId)).ReturnsAsync(dispute);
            _disputeRepoMock.Setup(x => x.GetDriverLoseCountInMonthAsync(
                DriverUserId, It.IsAny<DateTime>())).ReturnsAsync(3); // >= threshold 3

            var driverUser = booking.Driver!.User;
            var svc = CreateService();

            // Act
            await svc.ResolveDisputeAsync(AdminUserId, DisputeId, CreateResolveDto(false));

            // Assert: Driver bị suspend
            Assert.Equal(1, driverUser.BanCount);
            Assert.NotNull(driverUser.BannedUntil);

            // UserManager.UpdateAsync called
            _userManagerMock.Verify(x => x.UpdateAsync(
                It.Is<ApplicationUser>(u => u.Id == DriverUserId)), Times.Once);

            // CancelDriverBookingsAsync called
            _bookingRepoMock.Verify(x => x.GetActiveBookingsByDriverAsync(
                DriverUserId, It.IsAny<BookingStatus[]>()), Times.Once);
        }

        [Fact]
        public async Task TC07_DriverLoses_BelowThreshold_WarningOnly()
        {
            // Arrange: driver thua 1 lần (< 3), chỉ cảnh cáo
            var booking = CreateCompletedBooking();
            var dispute = CreatePendingReviewDispute(booking);

            _disputeRepoMock.Setup(x => x.GetByIdWithDetailsAsync(DisputeId)).ReturnsAsync(dispute);
            _disputeRepoMock.Setup(x => x.GetDriverLoseCountInMonthAsync(
                DriverUserId, It.IsAny<DateTime>())).ReturnsAsync(1);

            var driverUser = booking.Driver!.User;
            var svc = CreateService();

            // Act
            await svc.ResolveDisputeAsync(AdminUserId, DisputeId, CreateResolveDto(false));

            // Assert: NOT suspended
            Assert.Equal(0, driverUser.BanCount);
            Assert.Null(driverUser.BannedUntil);

            // Warning notification sent
            _notifyMock.Verify(x => x.SendAsync(
                DriverUserId, It.Is<string>(s => s.Contains("Cảnh cáo")),
                It.IsAny<string>(), NotificationType.System), Times.Once);

            // UpdateAsync NOT called (user not banned)
            _userManagerMock.Verify(x => x.UpdateAsync(
                It.IsAny<ApplicationUser>()), Times.Never);
        }

        // ────── BOUNDARY: Banning — Station Loses >= 5 ──────

        [Fact]
        public async Task TC08_StationLoses_ExceedsThreshold_Inactive()
        {
            // Arrange: IsDriverWin = true, station thua 5 lần
            var booking = CreateCompletedBooking();
            var dispute = CreatePendingReviewDispute(booking);

            _disputeRepoMock.Setup(x => x.GetByIdWithDetailsAsync(DisputeId)).ReturnsAsync(dispute);
            _disputeRepoMock.Setup(x => x.GetStationLoseCountInMonthAsync(
                StationId, It.IsAny<DateTime>())).ReturnsAsync(5); // >= threshold 5

            var station = booking.ChargingSlot!.ChargingStation!;
            var svc = CreateService();

            // Act
            await svc.ResolveDisputeAsync(AdminUserId, DisputeId, CreateResolveDto(true));

            // Assert: Station bị inactive
            Assert.Equal(OperationalStatus.Inactive, station.OperationalStatus);
            Assert.Equal(1, station.BanCount);
            Assert.NotNull(station.BannedUntil);

            // Station updated
            _stationRepoMock.Verify(x => x.Update(
                It.Is<ChargingStation>(s => s.Id == StationId)), Times.Once);

            // CancelStationBookingsAsync called
            _bookingRepoMock.Verify(x => x.GetActiveBookingsByStationIdsAsync(
                It.Is<List<int>>(ids => ids.Contains(StationId)),
                It.IsAny<BookingStatus[]>()), Times.Once);
        }

        [Fact]
        public async Task TC09_StationLoses_BelowThreshold_WarningOnly()
        {
            // Arrange: station thua 2 lần (< 5), chỉ cảnh cáo
            var booking = CreateCompletedBooking();
            var dispute = CreatePendingReviewDispute(booking);

            _disputeRepoMock.Setup(x => x.GetByIdWithDetailsAsync(DisputeId)).ReturnsAsync(dispute);
            _disputeRepoMock.Setup(x => x.GetStationLoseCountInMonthAsync(
                StationId, It.IsAny<DateTime>())).ReturnsAsync(2);

            var station = booking.ChargingSlot!.ChargingStation!;
            var svc = CreateService();

            // Act
            await svc.ResolveDisputeAsync(AdminUserId, DisputeId, CreateResolveDto(true));

            // Assert: station vẫn Active
            Assert.Equal(OperationalStatus.Active, station.OperationalStatus);
            Assert.Equal(0, station.BanCount);
            Assert.Null(station.BannedUntil);

            // Warning notification sent to owner
            _notifyMock.Verify(x => x.SendAsync(
                OwnerUserId, It.Is<string>(s => s.Contains("Cảnh cáo")),
                It.IsAny<string>(), NotificationType.System), Times.Once);
        }
    }
}
