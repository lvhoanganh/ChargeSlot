using ChargeSlot.Api.Models.Identity;

namespace ChargeSlot.Api.Models
{
    public class ChargingStation
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = null!;
        public string Address { get; set; } = null!;

        public double Latitude { get; set; }
        public double Longitude { get; set; }

        // Identity User (Owner)
        public int OwnerId { get; set; }
        public ApplicationUser Owner { get; set; } = null!;

        public bool IsApproved { get; set; }
        public DateTime CreatedAt { get; set; }

        public ICollection<ChargingSlot> ChargingSlots { get; set; }
            = new List<ChargingSlot>();
    }
}
