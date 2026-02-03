namespace ChargeSlot.Api.DTOs.Auth
{
    public class LoginDto
    {
        public string PhoneNumber { get; set; } = null!;
        public string Password { get; set; } = null!;
    }
}
