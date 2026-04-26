using System.ComponentModel.DataAnnotations;

namespace ChargeSlot.Api.DTOs.BankAccount
{
    public class CreateBankAccountDto
    {
        [Required]
        [MaxLength(200)]
        public string BankName { get; set; } = null!;

        [Required]
        [MaxLength(50)]
        public string BankAccountNumber { get; set; } = null!;

        [Required]
        [MaxLength(200)]
        public string BankAccountHolder { get; set; } = null!;

        public bool IsDefault { get; set; }
    }
}
