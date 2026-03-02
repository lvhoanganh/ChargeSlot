using System.ComponentModel.DataAnnotations;

namespace ChargeSlot.Api.DTOs.Profile
{
    public class OwnerProfileDto
    {
        [Required]
        [MaxLength(200)]
        public string BusinessName { get; set; } = null!;

        [Required]
        [MaxLength(50)]
        public string TaxCode { get; set; } = null!;
    }
}

