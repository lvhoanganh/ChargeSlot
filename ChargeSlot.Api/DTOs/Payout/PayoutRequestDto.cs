namespace ChargeSlot.Api.DTOs.Payout
{
    public class PayoutRequestDto
    {
        public int Id { get; set; }
        public int OwnerUserId { get; set; }
        public string? OwnerName { get; set; }
        public decimal Amount { get; set; }
        public string BankName { get; set; } = null!;
        public string BankAccountNumber { get; set; } = null!;
        public string BankAccountHolder { get; set; } = null!;
        public string Status { get; set; } = null!;
        public DateTime RequestedAt { get; set; }
        public DateTime? ProcessedAt { get; set; }
        public int? ProcessedByUserId { get; set; }
        public string? Note { get; set; }
    }
}
