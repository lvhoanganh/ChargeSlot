using System.ComponentModel.DataAnnotations;

namespace ChargeSlot.Api.DTOs.Dispute
{
    public class ResolveDisputeDto
    {
        /// <summary>true = Driver wins (refund), false = Owner wins (payout).</summary>
        [Required]
        public bool IsDriverWin { get; set; }

        [Required]
        [MaxLength(2000)]
        public string AdminNote { get; set; } = null!;
    }
}
