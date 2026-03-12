using System.ComponentModel.DataAnnotations;

namespace ChargeSlot.Api.DTOs.Wallet
{
    public class TopUpDto
    {
        [Required]
        [Range(10000, 50000000)]
        public decimal Amount { get; set; }
    }
}
