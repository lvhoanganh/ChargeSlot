using ChargeSlot.Api.Enums;

namespace ChargeSlot.Api.Models
{
    public class UserOtp
    {
        public Guid Id { get; set; }

        public string PhoneNumber { get; set; } = null!;

        public string OtpHash { get; set; } = null!;

        public DateTime ExpiredAt { get; set; }

        public bool IsUsed { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        // OTP dùng cho việc gì
        public OtpPurpose Purpose { get; set; }

        // Thời điểm OTP được verify thành công
        public DateTime? VerifiedAt { get; set; }
    }
}
