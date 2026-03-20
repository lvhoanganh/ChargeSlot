using System.ComponentModel.DataAnnotations;

namespace ChargeSlot.Api.DTOs.Slot
{
    public class CreateChargingSlotDto
    {
        [Required]
        [MaxLength(100)]
        public string SlotName { get; set; } = null!;

        public decimal? PositionX { get; set; }
        public decimal? PositionY { get; set; }

        /// <summary>Giá theo khung giờ (bắt buộc ít nhất 1 khung).</summary>
        public List<PricingTierItem>? PricingTiers { get; set; }
    }
}
