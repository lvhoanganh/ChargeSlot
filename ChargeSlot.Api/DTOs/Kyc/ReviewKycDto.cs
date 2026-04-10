using System.ComponentModel.DataAnnotations;

namespace ChargeSlot.Api.DTOs.Kyc
{
    public class ReviewKycDto
    {
        [Required]
        public bool IsApproved { get; set; }

        public string? RejectReason { get; set; }
    }
}
