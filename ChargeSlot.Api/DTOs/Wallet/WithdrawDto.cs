using System.ComponentModel.DataAnnotations;

namespace ChargeSlot.Api.DTOs.Wallet
{
    public class WithdrawDto
    {
        [Required]
        [Range(10000, 50000000)]
        public decimal Amount { get; set; }

        [Required]
        [MaxLength(200)]
        public string BankName { get; set; } = null!;

        [Required]
        [MaxLength(50)]
        public string BankAccountNumber { get; set; } = null!;

        [Required]
        [MaxLength(200)]
        public string BankAccountHolder { get; set; } = null!;

        [MaxLength(2000)]
        public string? UserNote { get; set; }
    }
}
