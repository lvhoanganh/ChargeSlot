using System;
using System.Collections.Generic;

namespace ChargeSlot.Api.Models
{
    public class ChargingSlot
    {
        public Guid Id { get; set; }

        public string SlotName { get; set; } = null!;
        public decimal PricePerHour { get; set; }

        public Guid ChargingStationId { get; set; }
        public ChargingStation ChargingStation { get; set; } = null!;

        public bool IsActive { get; set; } = true;

        // Navigation
        public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
    }
}
