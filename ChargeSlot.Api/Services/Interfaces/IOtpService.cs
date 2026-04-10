using ChargeSlot.Api.Enums;

namespace ChargeSlot.Api.Services.Interfaces
{
    public interface IOtpService
    {
        Task SendOtpAsync(string phoneNumber, OtpPurpose purpose);
        Task SendOtpRegister(string phoneNumber, OtpPurpose purpose);
        Task VerifyOtpAsync(string phoneNumber, string otp, OtpPurpose purpose);
    }
}
