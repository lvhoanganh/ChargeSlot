namespace ChargeSlot.Api.DTOs.Payment
{
    public class PaymentDto
    {
        public int Id { get; set; }
        public int BookingId { get; set; }
        public decimal Amount { get; set; }
        public string PaymentMethod { get; set; } = null!;
        public string? GatewayTxnRef { get; set; }
        public string Status { get; set; } = null!;
        public DateTime? PaidAt { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
