using ChargeSlot.Api.Enums;

namespace ChargeSlot.Api.Models
{
    /// <summary>SRS 1.5 Booking - driver, slot, time range, status.</summary>
    public class Booking
    {
        public int Id { get; set; }
        public int DriverUserId { get; set; }
        public Driver Driver { get; set; } = null!;

        public int SlotId { get; set; }
        public ChargingSlot ChargingSlot { get; set; } = null!;

        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public decimal? DurationHours { get; set; }
        public string? Note { get; set; }

        public BookingStatus Status { get; set; } = BookingStatus.Draft;
        public DateTime? PaymentExpiresAt { get; set; }
        public DateTime? CheckedInAt { get; set; }
        public DateTime? CheckinDeadlineAt { get; set; }
        public DateTime? CancelledAt { get; set; }
        public string? CancelReason { get; set; }
        public string? RejectionReason { get; set; }
        public decimal TotalAmount { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        /// <summary>Driver yêu cầu kết thúc sạc sớm.</summary>
        public DateTime? EarlyEndRequestedAt { get; set; }

        public ICollection<BookingExtraService> BookingExtraServices { get; set; } = new List<BookingExtraService>();
        public Payment? Payment { get; set; }
        public ChargingSession? ChargingSession { get; set; }
        public Invoice? Invoice { get; set; }
        public Rating? Rating { get; set; }
        public Dispute? Dispute { get; set; }
    }
}
