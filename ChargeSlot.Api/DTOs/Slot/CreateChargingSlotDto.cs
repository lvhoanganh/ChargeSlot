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
    }
}
