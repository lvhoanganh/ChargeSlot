namespace ChargeSlot.Api.Services.Interfaces
{
    public interface IFirebaseAuthService
    {
        /// <summary>
        /// Verify Firebase ID Token và trả về phone number đã xác thực.
        /// Ném exception nếu token không hợp lệ.
        /// </summary>
        Task<string> VerifyTokenAndGetPhoneAsync(string firebaseIdToken);
    }
}
