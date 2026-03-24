using ChargeSlot.Api.Enums;
using ChargeSlot.Api.Models.Identity;

using ChargeSlot.Api.Helpers;
namespace ChargeSlot.Api.Models
{
    /// <summary>SRS 1.5 PasswordResetToken/OTP - BR-12 to BR-19, CleanOTP job.</summary>
    public class UserOtp
    {
        public int Id { get; set; }
        /// <summary>Optional; required for register flow we only have phone.</summary>
        public int? UserId { get; set; }
        public ApplicationUser? User { get; set; }
        /// <summary>Required for lookup when user does not exist yet (e.g. register).</summary>
        public string PhoneNumber { get; set; } = null!;

        public string OtpHash { get; set; } = null!;
        public OtpPurpose Purpose { get; set; }
        public DateTime ExpiredAt { get; set; }
        public bool IsUsed { get; set; }
        public DateTime? VerifiedAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTimeHelper.VietnamNow();
    }
}
