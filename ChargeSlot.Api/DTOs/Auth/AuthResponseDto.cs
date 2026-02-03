namespace ChargeSlot.Api.DTOs.Auth
{
    public class AuthResponseDto
    {
        public string AccessToken { get; set; } = null!;
        public DateTime ExpiresAtUtc { get; set; }
        public int UserId { get; set; }
        public string PhoneNumber { get; set; } = null!;
        public string Role { get; set; } = null!;
    }
}
