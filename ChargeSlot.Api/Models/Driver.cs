using ChargeSlot.Api.Models.Identity;

namespace ChargeSlot.Api.Models
{
    /// <summary>SRS 1.5 Driver - extends User, driver-specific data.</summary>
    public class Driver
    {
        public int UserId { get; set; }
        public ApplicationUser User { get; set; } = null!;

        public string? VehicleType { get; set; }
        public string? LicensePlate { get; set; }
        public string? LicenseNumber { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
    }
}
