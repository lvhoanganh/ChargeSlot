using ChargeSlot.Api.Enums;

using ChargeSlot.Api.Helpers;
namespace ChargeSlot.Api.Models
{
    /// <summary>SRS 1.5 Payment - booking payment, E-wallet or Bank transfer.</summary>
    public class Payment
    {
        public int Id { get; set; }
        public int BookingId { get; set; }
        public Booking Booking { get; set; } = null!;

        public decimal Amount { get; set; }
        public PaymentMethod PaymentMethod { get; set; }
        public string? GatewayTxnRef { get; set; }
        public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
        public DateTime? PaidAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTimeHelper.VietnamNow();
    }
}
