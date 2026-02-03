namespace ChargeSlot.Api.DTOs.Auth
{
    public class RegisterDto
    {
        public string PhoneNumber { get; set; } = null!;
        public string FullName { get; set; } = null!;
        public string Password { get; set; } = null!;
        public string? Role { get; set; }
    }
}
