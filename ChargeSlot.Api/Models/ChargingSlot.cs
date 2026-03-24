using ChargeSlot.Api.Enums;

using ChargeSlot.Api.Helpers;
namespace ChargeSlot.Api.Models
{
    /// <summary>SRS 1.5 ChargingSlot - power outlet, price per hour, status.</summary>
    public class ChargingSlot
    {
        public int Id { get; set; }
        public int StationId { get; set; }
        public ChargingStation ChargingStation { get; set; } = null!;

        public string SlotName { get; set; } = null!;

        // Vị trí trụ sạc trên sơ đồ (tọa độ tương đối %, responsive)
        public decimal? PositionX { get; set; }
        public decimal? PositionY { get; set; }

        /// <summary>Unique QR token for check-in. Generated when station is approved or slot is added to approved station.</summary>
        public string? QrCodeToken { get; set; }

        public SlotStatus Status { get; set; } = SlotStatus.Inactive;

        public DateTime CreatedAt { get; set; } = DateTimeHelper.VietnamNow();
        public DateTime? UpdatedAt { get; set; }

        public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
    }
}
