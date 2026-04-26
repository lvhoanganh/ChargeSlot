using ChargeSlot.Api.Enums;
using ChargeSlot.Api.Models;
using ChargeSlot.Api.Models.Identity;
using Moq;

namespace ChargeSlot.Tests.Services.WalletServiceTests
{
    public class UserReportIssueTests : WalletServiceTestBase
    {
        private const int UserId    = 5;
        private const int WrongUser = 99;
        private const int RequestId = 1;

        private const string IssueNote = "Tôi chưa nhận được tiền trong tài khoản Vietcombank 1234567890.";

        // TC01
        [Fact]
        public async Task UserReportIssue_WrongUser_ShouldThrow()
        {
            var request = CreateTransferCompletedRequest(userId: UserId);
            _withdrawRepoMock.Setup(x => x.GetByIdWithUserAsync(RequestId)).ReturnsAsync(request);

            var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                CreateService().UserReportIssueAsync(WrongUser, RequestId, IssueNote));

            Assert.Contains("không thuộc", ex.Message);
        }

        // TC02 — Happy path: báo chưa nhận tiền → IssueReported, notify tất cả admin
        [Fact]
        public async Task UserReportIssue_Success_ShouldSetStatusAndNotifyAdmins()
        {
            var request = CreateTransferCompletedRequest(userId: UserId, amount: 500_000m);
            request.User = new ApplicationUser { Id = UserId, FullName = "Nguyen Van A" };

            _withdrawRepoMock.Setup(x => x.GetByIdWithUserAsync(RequestId)).ReturnsAsync(request);

            // Admin list: 2 admins
            var admins = new List<ApplicationUser>
            {
                new ApplicationUser { Id = 1, FullName = "Admin 1" },
                new ApplicationUser { Id = 2, FullName = "Admin 2" }
            };
            _userManagerMock.Setup(x => x.GetUsersInRoleAsync("Admin")).ReturnsAsync(admins);

            var result = await CreateService().UserReportIssueAsync(UserId, RequestId, IssueNote);

            // Status = IssueReported
            Assert.Equal(WithdrawStatus.IssueReported, request.Status);
            Assert.Equal("IssueReported", result.Status);

            // IssueNote và IssueReportedAt được set
            Assert.Equal(IssueNote, request.IssueNote);
            Assert.NotNull(request.IssueReportedAt);

            // WithdrawRequest được update
            _withdrawRepoMock.Verify(x => x.Update(request), Times.Once);
            _uowMock.Verify(x => x.CompleteAsync(), Times.Once);

            // Notify 2 admin
            _notifyMock.Verify(x => x.SendAsync(
                1, It.IsAny<string>(), It.IsAny<string>(), NotificationType.Wallet), Times.Once);
            _notifyMock.Verify(x => x.SendAsync(
                2, It.IsAny<string>(), It.IsAny<string>(), NotificationType.Wallet), Times.Once);
        }
        // TC03 — Status không phải TransferCompleted → throw
        [Fact]
        public async Task UserReportIssue_WrongStatus_ShouldThrow()
        {
            var request = CreateTransferCompletedRequest(userId: UserId);
            request.Status = WithdrawStatus.Approved; // sai trạng thái

            _withdrawRepoMock.Setup(x => x.GetByIdWithUserAsync(RequestId)).ReturnsAsync(request);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().UserReportIssueAsync(UserId, RequestId, IssueNote));

            Assert.Contains("TransferCompleted", ex.Message);
        }

        // TC04 — Request không tồn tại
        [Fact]
        public async Task UserReportIssue_RequestNotFound_ShouldThrow()
        {
            // default: GetByIdWithUserAsync → null

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().UserReportIssueAsync(UserId, RequestId, IssueNote));

            Assert.Contains("không tồn tại", ex.Message);
        }
    }
}
