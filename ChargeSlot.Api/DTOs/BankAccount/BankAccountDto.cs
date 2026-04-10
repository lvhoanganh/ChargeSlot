namespace ChargeSlot.Api.DTOs.BankAccount
{
    public class BankAccountDto
    {
        public int Id { get; set; }
        public string BankName { get; set; } = null!;
        public string BankAccountNumber { get; set; } = null!;
        public string BankAccountHolder { get; set; } = null!;
        public bool IsDefault { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
