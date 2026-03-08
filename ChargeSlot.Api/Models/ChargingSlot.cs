using ChargeSlot.Api.Enums;

namespace ChargeSlot.Api.Models
{
    /// <summary>SRS 1.5 ChargingSlot - connector type, price per hour, status.</summary>
    public class ChargingSlot
    {
        public int Id { get; set; }
        public int StationId { get; set; }
        public ChargingStation ChargingStation { get; set; } = null!;

        public string SlotName { get; set; } = null!;
        public string ConnectorType { get; set; } = null!;
        public decimal? PowerKw { get; set; }
        public decimal BasePricePerHour { get; set; }

        // Vị trí trụ sạc trên sơ đồ (tọa độ tương đối %, responsive)
        public decimal? PositionX { get; set; }
        public decimal? PositionY { get; set; }

        public SlotStatus Status { get; set; } = SlotStatus.Inactive;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
        public ICollection<SlotPricing> SlotPricings { get; set; } = new List<SlotPricing>();
    }
}
