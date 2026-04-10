namespace ChargeSlot.Api.DTOs.Auth
{
    /// <summary>
    /// Thông tin tài khoản hiện tại — dùng cho GET /api/auth/me
    /// </summary>
    public class UserInfoDto
    {
        public int UserId { get; set; }
        public string FullName { get; set; } = null!;
        public string PhoneNumber { get; set; } = null!;
        public string? Email { get; set; }
        public bool EmailConfirmed { get; set; }
        public string Role { get; set; } = null!;
        public string? AvatarUrl { get; set; }
        public string Status { get; set; } = null!;
        /// <summary>True nếu user chưa có email hoặc chưa verify — frontend cần hiện popup.</summary>
        public bool RequiresEmail { get; set; }
        
        // ─── KYC fields for Owner ───
        public string? KycStatus { get; set; }
        public string? KycRejectReason { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
