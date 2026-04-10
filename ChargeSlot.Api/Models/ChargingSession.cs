using ChargeSlot.Api.Helpers;
namespace ChargeSlot.Api.Models
{
    /// <summary>SRS 1.5 ChargingSession - actual start/end, duration.</summary>
    public class ChargingSession
    {
        public int Id { get; set; }
        public int BookingId { get; set; }
        public Booking Booking { get; set; } = null!;

        public DateTime? CheckinTime { get; set; }
        public DateTime? ActualStartTime { get; set; }
        public DateTime? ActualEndTime { get; set; }
        public decimal? ActualDurationHours { get; set; }
        public DateTime CreatedAt { get; set; } = DateTimeHelper.VietnamNow();
    }
}
