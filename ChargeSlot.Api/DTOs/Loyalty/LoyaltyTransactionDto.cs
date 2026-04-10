namespace ChargeSlot.Api.DTOs.Loyalty
{
    public class LoyaltyTransactionDto
    {
        public int Id { get; set; }
        public int? BookingId { get; set; }
        public string Type { get; set; } = null!;
        public decimal Points { get; set; }
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
