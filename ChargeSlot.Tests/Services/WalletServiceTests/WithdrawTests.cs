using ChargeSlot.Api.Enums;
using ChargeSlot.Api.Models;
using ChargeSlot.Api.Models.Identity;
using ChargeSlot.Api.DTOs.Wallet;
using Moq;

namespace ChargeSlot.Tests.Services.WalletServiceTests
{
    public class WithdrawTests : WalletServiceTestBase
    {
        private const int UserId      = 5;
        private const int OwnerUserId = 8;

        // TC01 — Amount dưới mức tối thiểu 50,000đ
        [Fact]
        public async Task Withdraw_BelowMinimum_ShouldThrow()
        {
            var dto = CreateValidWithdrawDto(amount: 30_000m); // dưới minimum

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().WithdrawAsync(UserId, dto));

            Assert.Contains("50,000", ex.Message);
        }

        // TC02 — Owner KYC chưa được duyệt
        [Fact]
        public async Task Withdraw_OwnerKycNotApproved_ShouldThrow()
        {
            var dto   = CreateValidWithdrawDto(500_000m);
            var owner = new Owner { UserId = OwnerUserId, KycStatus = KycStatus.Pending };

            _ownerRepoMock.Setup(x => x.GetByUserIdAsync(OwnerUserId, It.IsAny<bool>()))
                          .ReturnsAsync(owner);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().WithdrawAsync(OwnerUserId, dto));

            Assert.Contains("KYC", ex.Message);
        }

        // TC03 — Số dư không đủ: muốn rút 500k nhưng ví chỉ có 200k
        [Fact]
        public async Task Withdraw_InsufficientBalance_ShouldThrow()
        {
            var dto    = CreateValidWithdrawDto(amount: 500_000m);
            var wallet = CreateDriverWallet(UserId, balance: 200_000m); // chỉ có 200k

            _walletRepoMock.Setup(x => x.GetByUserIdAsync(UserId)).ReturnsAsync(wallet);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().WithdrawAsync(UserId, dto));

            Assert.Contains("Số dư không đủ", ex.Message);
        }

        // TC04 — Happy path: Driver (non-owner, bỏ qua KYC check)
        // Rút 500k từ ví 1,000k → WithdrawRequest tạo, frozen, ledger ghi, notify user
        [Fact]
        public async Task Withdraw_Success_NonOwner_ShouldCreateRequestAndFreeze()
        {
            var dto    = CreateValidWithdrawDto(amount: 500_000m);
            var wallet = CreateDriverWallet(UserId, balance: 1_000_000m);

            // Non-owner: ownerRepo trả null (default)
            _walletRepoMock.Setup(x => x.GetByUserIdAsync(UserId)).ReturnsAsync(wallet);

            var user = new ApplicationUser { Id = UserId, FullName = "Nguyen Van A" };
            _userManagerMock.Setup(x => x.FindByIdAsync(UserId.ToString())).ReturnsAsync(user);

            var result = await CreateService().WithdrawAsync(UserId, dto);

            // WithdrawRequest được tạo
            _withdrawRepoMock.Verify(x => x.Add(It.Is<WithdrawRequest>(r =>
                r.Amount == 500_000m &&
                r.BankName == "Vietcombank" &&
                r.Status == WithdrawStatus.Pending)), Times.Once);

            // FreezeIfSufficient gọi với đúng amount
            _walletRepoMock.Verify(x => x.FreezeIfSufficientAsync(wallet.Id, 500_000m), Times.Once);

            // Ledger double-entry ghi
            _ledgerRepoMock.Verify(x => x.Add(It.IsAny<LedgerTransaction>()), Times.Once);

            // Notify user
            _notifyMock.Verify(x => x.SendAsync(UserId, It.IsAny<string>(),
                It.IsAny<string>(), NotificationType.Wallet), Times.Once);

            // Transaction commit
            _transactionMock.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);

            Assert.Equal("Pending", result.Status);
            Assert.Equal(500_000m, result.Amount);
        }

        // TC05 — Happy path: Owner đã KYC Approved → rút được tiền bình thường
        [Fact]
        public async Task Withdraw_Success_ApprovedOwner_ShouldCreateRequest()
        {
            var dto    = CreateValidWithdrawDto(amount: 2_000_000m); // 2 triệu
            var wallet = new Wallet{ Id = 20, UserId = OwnerUserId, WalletType = WalletType.Owner, AvailableBalance = 5_000_000m };
            var owner  = new Owner { UserId = OwnerUserId, KycStatus = KycStatus.Approved };

            _ownerRepoMock.Setup(x => x.GetByUserIdAsync(OwnerUserId, It.IsAny<bool>())).ReturnsAsync(owner);
            _walletRepoMock.Setup(x => x.GetByUserIdAsync(OwnerUserId)).ReturnsAsync(wallet);

            var user = new ApplicationUser { Id = OwnerUserId, FullName = "Owner A" };
            _userManagerMock.Setup(x => x.FindByIdAsync(OwnerUserId.ToString())).ReturnsAsync(user);

            var result = await CreateService().WithdrawAsync(OwnerUserId, dto);

            _withdrawRepoMock.Verify(x => x.Add(It.Is<WithdrawRequest>(r =>
                r.Amount == 2_000_000m && r.UserId == OwnerUserId)), Times.Once);

            _walletRepoMock.Verify(x => x.FreezeIfSufficientAsync(wallet.Id, 2_000_000m), Times.Once);

            Assert.Equal("Pending", result.Status);
        }

        // TC06 — Race condition: FreezeIfSufficient trả 0 (ví bị đổi bởi transaction khác)
        [Fact]
        public async Task Withdraw_RaceCondition_FreezeFails_ShouldThrow()
        {
            var dto    = CreateValidWithdrawDto(amount: 500_000m);
            var wallet = CreateDriverWallet(UserId, balance: 1_000_000m);

            _walletRepoMock.Setup(x => x.GetByUserIdAsync(UserId)).ReturnsAsync(wallet);
            // FreezeIfSufficient trả 0 sau khi đã qua check balance
            _walletRepoMock.Setup(x => x.FreezeIfSufficientAsync(wallet.Id, 500_000m)).ReturnsAsync(0);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().WithdrawAsync(UserId, dto));

            Assert.Contains("giao dịch khác", ex.Message);
            // Transaction rollback
            _transactionMock.Verify(x => x.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        // TC07 — User chưa có ví → tự động tạo ví mới rồi freeze
        [Fact]
        public async Task Withdraw_WalletNotExist_ShouldAutoCreateAndFreeze()
        {
            var dto  = CreateValidWithdrawDto(amount: 500_000m);
            // wallet ban đầu null → GetOrCreateWalletInternalAsync tạo mới
            // Nhưng vì ví mới có balance = 0 < 500k → throw InsufficientBalance
            // → đây test nánh auto-create + insufficient
            _walletRepoMock.Setup(x => x.GetByUserIdAsync(UserId)).ReturnsAsync((Wallet?)null);

            var newWallet = new Wallet { Id = 99, UserId = UserId, AvailableBalance = 0m };
            // Sau khi Add, GetByUserIdAsync gọi lần sau vẫn null (EF chưa save) → có sẵn wallet
            // Thực tế: GetOrCreate chỉ gọi 1 lần, rồi trả wallet mới balance=0
            _walletRepoMock.Setup(x => x.Add(It.IsAny<Wallet>()));

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().WithdrawAsync(UserId, dto));

            // Ví mới được tạo
            _walletRepoMock.Verify(x => x.Add(It.Is<Wallet>(w => w.UserId == UserId)), Times.Once);
            // Balance 0 < 500k → throw
            Assert.Contains("Số dư không đủ", ex.Message);
        }
    }
}
