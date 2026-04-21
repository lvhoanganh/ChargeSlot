using ChargeSlot.Api.Models.Identity;
using ChargeSlot.Api.Constants;
using Microsoft.AspNetCore.Identity;
using Moq;

namespace ChargeSlot.Tests.Services.AuthServiceTests
{
    public class ResetPasswordTests : AuthServiceTestBase
    {
        private const string PHONE = "0901234567";
        private const string NORMALIZED_PHONE = "0901234567";
        private const string FIREBASE_TOKEN = "firebase_id_token";
        private const string PASSWORD = "NewPass@123";

        private ApplicationUser BuildUser() => new ApplicationUser
        {
            UserName = NORMALIZED_PHONE,
            FullName = "Test User"
        };

        private void SetupSuccessFlow(ApplicationUser user)
        {
            _firebaseMock.Setup(x => x.VerifyTokenAndGetPhoneAsync(FIREBASE_TOKEN))
                .ReturnsAsync(NORMALIZED_PHONE);
            _userManagerMock.Setup(x => x.FindByNameAsync(NORMALIZED_PHONE)).ReturnsAsync(user);
            _userManagerMock.Setup(x => x.GeneratePasswordResetTokenAsync(user)).ReturnsAsync("reset_token");
            _userManagerMock.Setup(x => x.ResetPasswordAsync(user, "reset_token", PASSWORD))
                .ReturnsAsync(IdentityResult.Success);
        }

        // TC01 - Firebase trả null → throw
        [Fact]
        public async Task TC01_FirebaseReturnNull_ShouldThrow()
        {
            _firebaseMock.Setup(x => x.VerifyTokenAndGetPhoneAsync(FIREBASE_TOKEN))
                .ReturnsAsync((string?)null);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().ResetPasswordAsync(PHONE, PASSWORD, FIREBASE_TOKEN));
        }

        // TC02 - Phone mismatch → throw
        [Fact]
        public async Task TC02_PhoneMismatch_ShouldThrow()
        {
            _firebaseMock.Setup(x => x.VerifyTokenAndGetPhoneAsync(FIREBASE_TOKEN))
                .ReturnsAsync("0999999999");

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().ResetPasswordAsync(PHONE, PASSWORD, FIREBASE_TOKEN));
        }

        // TC03 - User not found → throw
        [Fact]
        public async Task TC03_UserNotFound_ShouldThrow()
        {
            _firebaseMock.Setup(x => x.VerifyTokenAndGetPhoneAsync(FIREBASE_TOKEN))
                .ReturnsAsync(NORMALIZED_PHONE);
            _userManagerMock.Setup(x => x.FindByNameAsync(It.IsAny<string>()))
                .ReturnsAsync((ApplicationUser?)null);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().ResetPasswordAsync(PHONE, PASSWORD, FIREBASE_TOKEN));
        }

        // TC04 - Success → OTP & UoW được gọi
        [Fact]
        public async Task TC04_ResetSuccess_ShouldPass()
        {
            var user = BuildUser();
            SetupSuccessFlow(user);

            await CreateService().ResetPasswordAsync(PHONE, PASSWORD, FIREBASE_TOKEN);

            _otpRepoMock.Verify(x => x.InvalidateAllOtpsAsync(NORMALIZED_PHONE), Times.Once);
            _uowMock.Verify(x => x.CompleteAsync(), Times.Once);
        }

        // TC05 - ResetPasswordAsync fail (password yếu) → throw
        [Fact]
        public async Task TC05_ResetFail_ShouldThrow()
        {
            var user = BuildUser();
            _firebaseMock.Setup(x => x.VerifyTokenAndGetPhoneAsync(FIREBASE_TOKEN)).ReturnsAsync(NORMALIZED_PHONE);
            _userManagerMock.Setup(x => x.FindByNameAsync(NORMALIZED_PHONE)).ReturnsAsync(user);
            _userManagerMock.Setup(x => x.GeneratePasswordResetTokenAsync(user)).ReturnsAsync("reset_token");
            _userManagerMock.Setup(x => x.ResetPasswordAsync(user, "reset_token", PASSWORD))
                .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Password too weak" }));

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().ResetPasswordAsync(PHONE, PASSWORD, FIREBASE_TOKEN));
        }

        // TC06 - OTP phải bị invalidate
        [Fact]
        public async Task TC06_ShouldInvalidateOtp()
        {
            var user = BuildUser();
            SetupSuccessFlow(user);

            await CreateService().ResetPasswordAsync(PHONE, PASSWORD, FIREBASE_TOKEN);

            _otpRepoMock.Verify(x => x.InvalidateAllOtpsAsync(NORMALIZED_PHONE), Times.Once);
        }

        // TC07 - UnitOfWork phải commit
        [Fact]
        public async Task TC07_ShouldCommit()
        {
            var user = BuildUser();
            SetupSuccessFlow(user);

            await CreateService().ResetPasswordAsync(PHONE, PASSWORD, FIREBASE_TOKEN);

            _uowMock.Verify(x => x.CompleteAsync(), Times.Once);
        }

        // TC08 - Full flow success
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

        // TC09 - Firebase trả chuỗi rỗng → throw
        [Fact]
        public async Task TC09_FirebaseEmptyString_ShouldThrow()
        {
            _firebaseMock.Setup(x => x.VerifyTokenAndGetPhoneAsync(FIREBASE_TOKEN))
                .ReturnsAsync(string.Empty);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().ResetPasswordAsync(PHONE, PASSWORD, FIREBASE_TOKEN));
        }

        // TC10 - E.164 format mismatch → throw
        [Fact]
        public async Task TC10_FormatMismatch_E164_ShouldThrow()
        {
            _firebaseMock.Setup(x => x.VerifyTokenAndGetPhoneAsync(FIREBASE_TOKEN))
                .ReturnsAsync("+84901234567");

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService().ResetPasswordAsync(PHONE, PASSWORD, FIREBASE_TOKEN));
        }

        // TC11 - OTP repo throw exception
        [Fact]
        public async Task TC11_OtpFail_ShouldThrow()
        {
            var user = BuildUser();
            SetupSuccessFlow(user);
            _otpRepoMock.Setup(x => x.InvalidateAllOtpsAsync(NORMALIZED_PHONE))
                .ThrowsAsync(new Exception("OTP fail"));

            await Assert.ThrowsAsync<Exception>(() =>
                CreateService().ResetPasswordAsync(PHONE, PASSWORD, FIREBASE_TOKEN));
        }

        // TC12 - UnitOfWork throw exception
        [Fact]
        public async Task TC12_UnitOfWorkFail_ShouldThrow()
        {
            var user = BuildUser();
            SetupSuccessFlow(user);
            _uowMock.Setup(x => x.CompleteAsync()).ThrowsAsync(new Exception("DB fail"));

            await Assert.ThrowsAsync<Exception>(() =>
                CreateService().ResetPasswordAsync(PHONE, PASSWORD, FIREBASE_TOKEN));
        }
    }
}
