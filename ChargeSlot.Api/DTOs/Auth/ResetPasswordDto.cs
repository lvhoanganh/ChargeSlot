namespace ChargeSlot.Api.DTOs.Auth
{
    public class ResetPasswordDto
    {
        public string PhoneNumber { get; set; } = null!;
        public string NewPassword { get; set; } = null!;
    }
}
