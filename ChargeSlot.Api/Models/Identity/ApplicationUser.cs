using Microsoft.AspNetCore.Identity;

using ChargeSlot.Api.Helpers;
namespace ChargeSlot.Api.Models.Identity
{
    /// <summary>SRS 1.5 User - login by phone number, email optional.</summary>
    public class ApplicationUser : IdentityUser<int>
    {
        public string FullName { get; set; } = null!;
        public string? AvatarUrl { get; set; }
        /// <summary>BR-05: verified via SMS/OTP.</summary>
        public bool IsPhoneVerified { get; set; }
        /// <summary>ACTIVE | BANNED | SUSPENDED (BR-08, BR-149).</summary>
        public string Status { get; set; } = "ACTIVE";
        public DateTime CreatedAt { get; set; } = DateTimeHelper.VietnamNow();
        public DateTime? UpdatedAt { get; set; }
        
        // Tracking Dispute Bans
        public int BanCount { get; set; } = 0;
        public DateTime? BannedUntil { get; set; }

        /// <summary>Secondary Password Hash primarily for Administrative config changes.</summary>
        public string? SecondaryPasswordHash { get; set; }

        public Driver? DriverProfile { get; set; }
        public Owner? OwnerProfile { get; set; }
    }
}
