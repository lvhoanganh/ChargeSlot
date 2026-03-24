namespace ChargeSlot.Api.DTOs.Wallet
{
    public class TransactionHistoryDto
    {
        public long Id { get; set; }
        public string Type { get; set; } = null!;
        public string Direction { get; set; } = null!;
        public decimal Amount { get; set; }
        public string? Memo { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
