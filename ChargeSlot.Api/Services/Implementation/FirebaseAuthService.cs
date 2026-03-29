using ChargeSlot.Api.Helpers;
using ChargeSlot.Api.Services.Interfaces;
using FirebaseAdmin.Auth;

namespace ChargeSlot.Api.Services.Implementation
{
    /// <summary>
    /// Xác thực Firebase ID Token.
    /// Dùng cho: Register (verify SĐT) và Forgot Password (verify SĐT).
    /// KHÔNG dùng để login — login vẫn cần SĐT + mật khẩu.
    /// </summary>
    public class FirebaseAuthService : IFirebaseAuthService
    {
        private readonly ILogger<FirebaseAuthService> _logger;

        public FirebaseAuthService(ILogger<FirebaseAuthService> logger)
        {
            _logger = logger;
        }

        public async Task<string> VerifyTokenAndGetPhoneAsync(string firebaseIdToken)
        {
            // 1. Verify Firebase ID Token
            FirebaseToken decodedToken;
            try
            {
                decodedToken = await FirebaseAuth.DefaultInstance.VerifyIdTokenAsync(firebaseIdToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Firebase token verification failed");
                throw new InvalidOperationException("Firebase token không hợp lệ hoặc đã hết hạn.");
            }

            // 2. Lấy phone number từ Firebase token
            var phoneNumber = decodedToken.Claims.TryGetValue("phone_number", out var phoneClaim)
                ? phoneClaim?.ToString()
                : null;

            if (string.IsNullOrEmpty(phoneNumber))
                throw new InvalidOperationException("Firebase token không chứa số điện thoại.");

            // 3. Normalize phone: +84xxx → 0xxx
            return PhoneNumberHelper.NormalizeAndValidate(phoneNumber);
        }
    }
}
