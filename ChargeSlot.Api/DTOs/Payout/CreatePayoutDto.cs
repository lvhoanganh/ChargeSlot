using System.ComponentModel.DataAnnotations;

namespace ChargeSlot.Api.DTOs.Payout
{
    public class CreatePayoutDto
    {
        [Required]
        [Range(10000, 50000000)]
        public decimal Amount { get; set; }

        [Required]
        public int BankAccountId { get; set; }

        [MaxLength(2000)]
        public string? Note { get; set; }
    }
}
