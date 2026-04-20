using Xunit;
using Moq;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using ChargeSlot.Api.Services.Implementation;
using ChargeSlot.Api.Models.Identity;
using ChargeSlot.Api.Models;
using ChargeSlot.Api.Repositories.Interfaces;
using ChargeSlot.Api.Services.Interfaces;
using ChargeSlot.Api.Constants;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace ChargeSlot.Tests.Services.AuthServiceTests
{
    public class ResetPasswordTests
    {
        // ===== MOCKS =====
        private readonly Mock<UserManager<ApplicationUser>> _userManagerMock;
        private readonly Mock<SignInManager<ApplicationUser>> _signInManagerMock;
        private readonly Mock<RoleManager<IdentityRole<int>>> _roleManagerMock;
        private readonly Mock<IConfiguration> _configMock = new();
        private readonly Mock<IUserOtpRepository> _otpRepoMock = new();
        private readonly Mock<IFirebaseAuthService> _firebaseMock = new();
        private readonly Mock<IOwnerRepository> _ownerRepoMock = new();
        private readonly Mock<IDriverRepository> _driverRepoMock = new();
        private readonly Mock<IRefreshTokenRepository> _refreshTokenMock = new();
        private readonly Mock<IUnitOfWork> _uowMock = new();
        private readonly Mock<IEmailService> _emailMock = new();
        private readonly Mock<ILogger<AuthService>> _loggerMock = new();

        // PhoneNumberHelper: "0901234567" → giữ nguyên "0901234567" (không chuyển E.164).
        // "+84901234567" → chuyển thành "0901234567" (trùng).
        // Firebase phải trả đúng "0901234567" để match.
        private const string PHONE = "0901234567";
        private const string NORMALIZED_PHONE = "0901234567";
        private const string FIREBASE_TOKEN = "firebase_id_token";
        private const string PASSWORD = "NewPass@123";

        // ===== CONSTRUCTOR =====
        public ResetPasswordTests()
        {
            var userStore = new Mock<IUserStore<ApplicationUser>>();
            _userManagerMock = new Mock<UserManager<ApplicationUser>>(
                userStore.Object, null, null, null, null, null, null, null, null);

            var httpCtx = new Mock<Microsoft.AspNetCore.Http.IHttpContextAccessor>();
            var claimFactory = new Mock<IUserClaimsPrincipalFactory<ApplicationUser>>();
            _signInManagerMock = new Mock<SignInManager<ApplicationUser>>(
                _userManagerMock.Object, httpCtx.Object, claimFactory.Object, null, null, null, null);

            var roleStore = new Mock<IRoleStore<IdentityRole<int>>>();
            _roleManagerMock = new Mock<RoleManager<IdentityRole<int>>>(roleStore.Object, null, null, null, null);

            // Default: OTP invalidate & UoW thành công
            _otpRepoMock.Setup(x => x.InvalidateAllOtpsAsync(It.IsAny<string>()))
                .Returns(Task.CompletedTask);
            _uowMock.Setup(x => x.CompleteAsync()).ReturnsAsync(1);

            // Default: user không tồn tại
            _userManagerMock.Setup(x => x.FindByNameAsync(It.IsAny<string>()))
                .ReturnsAsync((ApplicationUser?)null);
        }

        // ===== FACTORY =====
        private AuthService CreateService() => new AuthService(
            _userManagerMock.Object,
            _roleManagerMock.Object,
            _signInManagerMock.Object,
            _configMock.Object,
            _otpRepoMock.Object,
            _firebaseMock.Object,
            _ownerRepoMock.Object,
            _driverRepoMock.Object,
            _refreshTokenMock.Object,
            _uowMock.Object,
            _emailMock.Object,
            _loggerMock.Object);

        // ===== HELPER =====
        private static ApplicationUser BuildUser() => new ApplicationUser
        {
            UserName = NORMALIZED_PHONE,
            FullName = "Test User"
        };

        // Helper setup full success path (Firebase + user + reset token)
        private void SetupSuccessFlow(ApplicationUser user)
        {
            _firebaseMock.Setup(x => x.VerifyTokenAndGetPhoneAsync(FIREBASE_TOKEN))
                .ReturnsAsync(NORMALIZED_PHONE);

            _userManagerMock.Setup(x => x.FindByNameAsync(NORMALIZED_PHONE))
                .ReturnsAsync(user);

            _userManagerMock.Setup(x => x.GeneratePasswordResetTokenAsync(user))
                .ReturnsAsync("reset_token");

            _userManagerMock.Setup(x => x.ResetPasswordAsync(user, "reset_token", PASSWORD))
                .ReturnsAsync(IdentityResult.Success);
        }

        // ===============================
        // TC01 - Firebase trả null → mismatch → throw
        // ===============================
        [Fact]
        public async Task TC01_FirebaseReturnNull_ShouldThrow()
        {
            _firebaseMock.Setup(x => x.VerifyTokenAndGetPhoneAsync(FIREBASE_TOKEN))
                .ReturnsAsync((string?)null);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().ResetPasswordAsync(PHONE, PASSWORD, FIREBASE_TOKEN));
        }

        // ===============================
        // TC02 - Firebase trả số khác → phone mismatch → throw
        // ===============================
        [Fact]
        public async Task TC02_PhoneMismatch_ShouldThrow()
        {
            _firebaseMock.Setup(x => x.VerifyTokenAndGetPhoneAsync(FIREBASE_TOKEN))
                .ReturnsAsync("0999999999");

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().ResetPasswordAsync(PHONE, PASSWORD, FIREBASE_TOKEN));
        }

        // ===============================
        // TC03 - User not found → throw
        // ===============================
        [Fact]
        public async Task TC03_UserNotFound_ShouldThrow()
        {
            _firebaseMock.Setup(x => x.VerifyTokenAndGetPhoneAsync(FIREBASE_TOKEN))
                .ReturnsAsync(NORMALIZED_PHONE);
            // Default mock đã trả null cho FindByNameAsync → không cần setup thêm

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().ResetPasswordAsync(PHONE, PASSWORD, FIREBASE_TOKEN));
        }

        // ===============================
        // TC04 - Success → không throw, OTP & UoW được gọi
        // ===============================
        [Fact]
        public async Task TC04_ResetSuccess_ShouldPass()
        {
            var user = BuildUser();
            SetupSuccessFlow(user);

            await CreateService().ResetPasswordAsync(PHONE, PASSWORD, FIREBASE_TOKEN);

            _otpRepoMock.Verify(x => x.InvalidateAllOtpsAsync(NORMALIZED_PHONE), Times.Once);
            _uowMock.Verify(x => x.CompleteAsync(), Times.Once);
        }

        // ===============================
        // TC05 - ResetPasswordAsync fail (password yếu) → throw
        // ===============================
        [Fact]
        public async Task TC05_ResetFail_ShouldThrow()
        {
            var user = BuildUser();

            _firebaseMock.Setup(x => x.VerifyTokenAndGetPhoneAsync(FIREBASE_TOKEN))
                .ReturnsAsync(NORMALIZED_PHONE);

            _userManagerMock.Setup(x => x.FindByNameAsync(NORMALIZED_PHONE))
                .ReturnsAsync(user);

            _userManagerMock.Setup(x => x.GeneratePasswordResetTokenAsync(user))
                .ReturnsAsync("reset_token");

            _userManagerMock.Setup(x => x.ResetPasswordAsync(user, "reset_token", PASSWORD))
                .ReturnsAsync(IdentityResult.Failed(
                    new IdentityError { Description = "Password too weak" }));

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().ResetPasswordAsync(PHONE, PASSWORD, FIREBASE_TOKEN));
        }

        // ===============================
        // TC06 - OTP phải bị invalidate sau reset thành công
        // ===============================
        [Fact]
        public async Task TC06_ShouldInvalidateOtp()
        {
            var user = BuildUser();
            SetupSuccessFlow(user);

            await CreateService().ResetPasswordAsync(PHONE, PASSWORD, FIREBASE_TOKEN);

            _otpRepoMock.Verify(x => x.InvalidateAllOtpsAsync(NORMALIZED_PHONE), Times.Once);
        }

        // ===============================
        // TC07 - UnitOfWork phải được commit sau reset thành công
        // ===============================
        [Fact]
        public async Task TC07_ShouldCommit()
        {
            var user = BuildUser();
            SetupSuccessFlow(user);

            await CreateService().ResetPasswordAsync(PHONE, PASSWORD, FIREBASE_TOKEN);

            _uowMock.Verify(x => x.CompleteAsync(), Times.Once);
        }

        // ===============================
        // TC08 - Full flow success (end-to-end verify)
        // ===============================
        [Fact]
        public async Task TC08_FullFlow_ShouldPass()
        {
            var user = BuildUser();
            SetupSuccessFlow(user);

            await CreateService().ResetPasswordAsync(PHONE, PASSWORD, FIREBASE_TOKEN);

            _userManagerMock.Verify(x => x.GeneratePasswordResetTokenAsync(user), Times.Once);
            _userManagerMock.Verify(x => x.ResetPasswordAsync(user, "reset_token", PASSWORD), Times.Once);
            _otpRepoMock.Verify(x => x.InvalidateAllOtpsAsync(NORMALIZED_PHONE), Times.Once);
            _uowMock.Verify(x => x.CompleteAsync(), Times.Once);
        }

        // ===============================
        // TC09 - Firebase trả chuỗi rỗng → mismatch → throw
        // ===============================
        [Fact]
        public async Task TC09_FirebaseEmptyString_ShouldThrow()
        {
            _firebaseMock.Setup(x => x.VerifyTokenAndGetPhoneAsync(FIREBASE_TOKEN))
                .ReturnsAsync(string.Empty);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().ResetPasswordAsync(PHONE, PASSWORD, FIREBASE_TOKEN));
        }

        // ===============================
        // TC10 - Firebase trả định dạng E.164 (+84...) trong khi normalize luôn
        //        giữ format 0xxx → mismatch → throw
        // ===============================
        [Fact]
        public async Task TC10_FormatMismatch_E164_ShouldThrow()
        {
            // NormalizePhone("0901234567") = "0901234567"
            // Firebase trả "+84901234567" (E.164) → không khớp → throw
            _firebaseMock.Setup(x => x.VerifyTokenAndGetPhoneAsync(FIREBASE_TOKEN))
                .ReturnsAsync("+84901234567");

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().ResetPasswordAsync(PHONE, PASSWORD, FIREBASE_TOKEN));
        }

        // ===============================
        // TC11 - OTP repo throw exception
        // ===============================
        [Fact]
        public async Task TC11_OtpFail_ShouldThrow()
        {
            var user = BuildUser();

            _firebaseMock.Setup(x => x.VerifyTokenAndGetPhoneAsync(FIREBASE_TOKEN))
                .ReturnsAsync(NORMALIZED_PHONE);

            _userManagerMock.Setup(x => x.FindByNameAsync(NORMALIZED_PHONE))
                .ReturnsAsync(user);

            _userManagerMock.Setup(x => x.GeneratePasswordResetTokenAsync(user))
                .ReturnsAsync("reset_token");

            _userManagerMock.Setup(x => x.ResetPasswordAsync(user, "reset_token", PASSWORD))
                .ReturnsAsync(IdentityResult.Success);

            _otpRepoMock.Setup(x => x.InvalidateAllOtpsAsync(NORMALIZED_PHONE))
                .ThrowsAsync(new Exception("OTP fail"));

            await Assert.ThrowsAsync<Exception>(() =>
                CreateService().ResetPasswordAsync(PHONE, PASSWORD, FIREBASE_TOKEN));
        }

        // ===============================
        // TC12 - UnitOfWork throw exception
        // ===============================
        [Fact]
        public async Task TC12_UnitOfWorkFail_ShouldThrow()
        {
            var user = BuildUser();

            _firebaseMock.Setup(x => x.VerifyTokenAndGetPhoneAsync(FIREBASE_TOKEN))
                .ReturnsAsync(NORMALIZED_PHONE);

            _userManagerMock.Setup(x => x.FindByNameAsync(NORMALIZED_PHONE))
                .ReturnsAsync(user);

            _userManagerMock.Setup(x => x.GeneratePasswordResetTokenAsync(user))
                .ReturnsAsync("reset_token");

            _userManagerMock.Setup(x => x.ResetPasswordAsync(user, "reset_token", PASSWORD))
                .ReturnsAsync(IdentityResult.Success);

            _uowMock.Setup(x => x.CompleteAsync())
                .ThrowsAsync(new Exception("DB fail"));

            await Assert.ThrowsAsync<Exception>(() =>
                CreateService().ResetPasswordAsync(PHONE, PASSWORD, FIREBASE_TOKEN));
        }
    }
}
