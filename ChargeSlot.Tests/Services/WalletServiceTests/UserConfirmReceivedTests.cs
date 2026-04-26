using ChargeSlot.Api.Enums;
using ChargeSlot.Api.Models;
using Moq;

namespace ChargeSlot.Tests.Services.WalletServiceTests
{
    public class UserConfirmReceivedTests : WalletServiceTestBase
    {
        private const int UserId    = 5;
        private const int WrongUser = 99;
        private const int RequestId = 1;

        // TC01
        [Fact]
        public async Task UserConfirm_WrongUser_ShouldThrow()
        {
            var request = CreateTransferCompletedRequest(userId: UserId);
            _withdrawRepoMock.Setup(x => x.GetByIdWithUserAndWalletAsync(RequestId)).ReturnsAsync(request);

            var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                CreateService().UserConfirmReceivedAsync(WrongUser, RequestId));

            Assert.Contains("không thuộc", ex.Message);
        }

        // TC02 — Status sai: Pending thay vì TransferCompleted
        [Fact]
        public async Task UserConfirm_WrongStatus_ShouldThrow()
        {
            var request = CreateTransferCompletedRequest(userId: UserId);
            request.Status = WithdrawStatus.Pending; // sai status

            _withdrawRepoMock.Setup(x => x.GetByIdWithUserAndWalletAsync(RequestId)).ReturnsAsync(request);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().UserConfirmReceivedAsync(UserId, RequestId));

            Assert.Contains("TransferCompleted", ex.Message);
        }

        // TC03 — Happy path: xác nhận đã nhận 500k
        // Verify: AdjustBalanceAtomic trừ frozen, status = Completed, UserConfirmedAt set,
        //         ledger ghi CLEARING debit, withdrawRepo.Update gọi
        [Fact]
        public async Task UserConfirm_Success_ShouldFinalizeAndDeductFrozen()
        {
            var request = CreateTransferCompletedRequest(userId: UserId, amount: 500_000m);

            _withdrawRepoMock.Setup(x => x.GetByIdWithUserAndWalletAsync(RequestId)).ReturnsAsync(request);

            var result = await CreateService().UserConfirmReceivedAsync(UserId, RequestId);

            // Status = Completed
            Assert.Equal(WithdrawStatus.Completed, request.Status);
            Assert.Equal("Completed", result.Status);

            // UserConfirmedAt được set
            Assert.NotNull(request.UserConfirmedAt);

            // FrozenBalance giảm: AdjustBalanceAtomicAsync(walletId, 0, -500_000)
            _walletRepoMock.Verify(x => x.AdjustBalanceAtomicAsync(
                request.WalletId, 0, -500_000m), Times.Once);

            // CLEARING cũng bị điều chỉnh (tiền rời hệ thống)
            _walletRepoMock.Verify(x => x.AdjustBalanceAtomicAsync(
                ClearingWallet.Id, -500_000m, 0), Times.Once);

            // Ledger ghi
            _ledgerRepoMock.Verify(x => x.Add(It.Is<LedgerTransaction>(t =>
                t.ReferenceType == "WithdrawCompleted")), Times.Once);

            // WithdrawRequest được update
            _withdrawRepoMock.Verify(x => x.Update(request), Times.Once);
        }
        // TC04 — Request không tồn tại
        [Fact]
        public async Task UserConfirm_RequestNotFound_ShouldThrow()
        {
            // default: GetByIdWithUserAndWalletAsync → null

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().UserConfirmReceivedAsync(UserId, RequestId));

            Assert.Contains("không tồn tại", ex.Message);
        }
    }
}
