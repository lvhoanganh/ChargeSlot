namespace ChargeSlot.Api.DTOs.Kyc
{
    public class OwnerKycProfileDto
    {
        public int OwnerUserId { get; set; }
        public string BusinessName { get; set; } = null!;
        public string TaxCode { get; set; } = null!;
        
        public string? IdCardNumber { get; set; }
        public string? IdCardDate { get; set; }
        public string? FrontIdCardUrl { get; set; }
        public string? BackIdCardUrl { get; set; }
        
        public string? BusinessLicenseNumber { get; set; }
        public string? BusinessLicenseUrl { get; set; }
        public string? Address { get; set; }

        public string KycStatus { get; set; } = null!;
        public string? KycRejectReason { get; set; }
        public DateTime? KycSubmittedAt { get; set; }
        public DateTime? KycReviewedAt { get; set; }
    }
}
