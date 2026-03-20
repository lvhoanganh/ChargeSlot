using System.ComponentModel.DataAnnotations;

namespace ChargeSlot.Api.DTOs.Slot
{
    public class UpdateChargingSlotDto
    {
        [Required]
        [MaxLength(100)]
        public string SlotName { get; set; } = null!;

        public decimal? PositionX { get; set; }
        public decimal? PositionY { get; set; }

        /// <summary>Giá theo khung giờ — gửi lại toàn bộ, BE sẽ replace hết.</summary>
        public List<PricingTierItem>? PricingTiers { get; set; }
    }

    /// <summary>Một khung giá theo giờ.</summary>
    public class PricingTierItem
    {
        [Required]
        public string StartTime { get; set; } = null!;  // "HH:mm", e.g. "00:00"
        [Required]
        public string EndTime { get; set; } = null!;    // "HH:mm", e.g. "08:00"
        [Required]
        public decimal PricePerHour { get; set; }
    }
}
