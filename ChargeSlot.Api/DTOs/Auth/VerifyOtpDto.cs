namespace ChargeSlot.Api.DTOs.Auth
{
    public class VerifyOtpDto
    {
        public string PhoneNumber { get; set; } = null!;
        public string Otp { get; set; } = null!;
    }
}
