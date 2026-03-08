using System.ComponentModel.DataAnnotations;

namespace ChargeSlot.Api.DTOs.Slot
{
    public class UpdateChargingSlotDto
    {
        [Required]
        [MaxLength(100)]
        public string SlotName { get; set; } = null!;

        [Required]
        [MaxLength(100)]
        public string ConnectorType { get; set; } = null!;

        public decimal? PowerKw { get; set; }

        [Required]
        [Range(0, double.MaxValue)]
        public decimal BasePricePerHour { get; set; }

        public decimal? PositionX { get; set; }
        public decimal? PositionY { get; set; }
    }
}
