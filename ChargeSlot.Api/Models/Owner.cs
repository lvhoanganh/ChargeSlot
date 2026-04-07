using ChargeSlot.Api.Models.Identity;
using ChargeSlot.Api.Enums;
using ChargeSlot.Api.Helpers;

namespace ChargeSlot.Api.Models
{
    /// <summary>SRS 1.5 Owner - extends User, business info. Payout via BankAccount.</summary>
    public class Owner
    {
        public int UserId { get; set; }
        public ApplicationUser User { get; set; } = null!;

        public string BusinessName { get; set; } = null!;
        public string TaxCode { get; set; } = null!;
        public DateTime CreatedAt { get; set; } = DateTimeHelper.VietnamNow();

        // ─── KYC & LEGAL VERIFICATION (All Owners are Businesses) ───
        public string? IdCardNumber { get; set; }
        public string? IdCardDate { get; set; } // Ngày cấp
        public string? FrontIdCardUrl { get; set; }
        public string? BackIdCardUrl { get; set; }

        public string? BusinessLicenseNumber { get; set; }
        public string? BusinessLicenseUrl { get; set; }
        public string? Address { get; set; } // Trụ sở chính

        public KycStatus KycStatus { get; set; } = KycStatus.Unverified;
        public string? KycRejectReason { get; set; }
        public DateTime? KycSubmittedAt { get; set; }
        public DateTime? KycReviewedAt { get; set; }

        public int? KycReviewedByUserId { get; set; }
        public ApplicationUser? KycReviewedByUser { get; set; }

        public ICollection<ChargingStation> ChargingStations { get; set; } = new List<ChargingStation>();
    }
}
