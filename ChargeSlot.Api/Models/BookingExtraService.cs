namespace ChargeSlot.Api.Models
{
    /// <summary>SRS 1.5 BookingExtraService - selected extras at booking time.</summary>
    public class BookingExtraService
    {
        public int Id { get; set; }
        public int BookingId { get; set; }
        public Booking Booking { get; set; } = null!;

        public int ServiceId { get; set; }
        public ExtraService ExtraService { get; set; } = null!;

        public int Quantity { get; set; } = 1;
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice { get; set; }
    }
}
