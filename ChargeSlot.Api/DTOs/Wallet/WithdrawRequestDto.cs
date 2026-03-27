namespace ChargeSlot.Api.DTOs.Wallet
{
    public class WithdrawRequestDto
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string? UserFullName { get; set; }
        public decimal Amount { get; set; }
        public string BankName { get; set; } = null!;
        public string BankAccountNumber { get; set; } = null!;
        public string BankAccountHolder { get; set; } = null!;
        public string Status { get; set; } = null!;
        public DateTime RequestedAt { get; set; }
        public DateTime? ProcessedAt { get; set; }
        public string? AdminNote { get; set; }
        public string? UserNote { get; set; }
    }
}
