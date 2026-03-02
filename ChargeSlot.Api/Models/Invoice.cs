using ChargeSlot.Api.Enums;

namespace ChargeSlot.Api.Models
{
    /// <summary>SRS 1.5 Invoice - charging + service + VAT + platform fee.</summary>
    public class Invoice
    {
        public int Id { get; set; }
        public int BookingId { get; set; }
        public Booking Booking { get; set; } = null!;

        public decimal ChargingAmount { get; set; }
        public decimal ServiceAmount { get; set; }
        public decimal VatAmount { get; set; }
        public decimal PlatformFee { get; set; }
        public decimal TotalAmount { get; set; }
        public InvoiceStatus Status { get; set; } = InvoiceStatus.PendingConfirm;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        public ICollection<Dispute> Disputes { get; set; } = new List<Dispute>();
    }
}
