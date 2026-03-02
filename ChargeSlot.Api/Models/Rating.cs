using ChargeSlot.Api.Models.Identity;

namespace ChargeSlot.Api.Models
{
    /// <summary>SRS 1.5 Rating - driver rates completed booking (BR-75, BR-76).</summary>
    public class Rating
    {
        public int Id { get; set; }
        public int BookingId { get; set; }
        public Booking Booking { get; set; } = null!;

        public int DriverUserId { get; set; }
        public ApplicationUser DriverUser { get; set; } = null!;

        public int StationId { get; set; }
        public ChargingStation ChargingStation { get; set; } = null!;

        public int Score { get; set; }
        public string? Comment { get; set; }
        public bool IsAnonymous { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
