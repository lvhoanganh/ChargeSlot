using ChargeSlot.Api.Enums;
using ChargeSlot.Api.Models.Identity;

namespace ChargeSlot.Api.Models
{
    public class Booking
    {
        public Guid Id { get; set; }

        // Identity User (Driver)
        public int DriverId { get; set; }
        public ApplicationUser Driver { get; set; } = null!;

        public Guid ChargingSlotId { get; set; }
        public ChargingSlot ChargingSlot { get; set; } = null!;

        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }

        public BookingStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }

        public Invoice? Invoice { get; set; }
    }
}
