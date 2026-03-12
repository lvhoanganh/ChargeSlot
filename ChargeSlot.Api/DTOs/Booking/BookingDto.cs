namespace ChargeSlot.Api.DTOs.Booking
{
    public class BookingDto
    {
        public int Id { get; set; }
        public int DriverUserId { get; set; }
        public string DriverName { get; set; } = null!;
        public int SlotId { get; set; }
        public string SlotName { get; set; } = null!;
        public int StationId { get; set; }
        public string StationName { get; set; } = null!;
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public decimal? DurationHours { get; set; }
        public decimal TotalAmount { get; set; }
        public string? Note { get; set; }
        public string Status { get; set; } = null!;
        public string? RejectionReason { get; set; }
        public string? CancelReason { get; set; }
        public DateTime? PaymentExpiresAt { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
