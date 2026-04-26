namespace ChargeSlot.Api.DTOs.Auth
{
    public class AuthResponseDto
    {
        public string AccessToken { get; set; } = null!;
        public DateTime ExpiresAtUtc { get; set; }
        public string RefreshToken { get; set; } = null!;
        public DateTime RefreshTokenExpiresAtUtc { get; set; }
        public int UserId { get; set; }
        public string PhoneNumber { get; set; } = null!;
        public string Role { get; set; } = null!;
        public string? Email { get; set; }
        /// <summary>True nếu user cũ chưa có email — frontend cần hiện popup nhập email.</summary>
        public bool RequiresEmail { get; set; }
    }
}
