using System.ComponentModel.DataAnnotations;
using ChargeSlot.Api.DTOs.Slot;

namespace ChargeSlot.Api.DTOs.Station
{
    public class CreateChargingStationDto
    {
        [Required]
        [MaxLength(255)]
        public string Name { get; set; } = null!;

        [Required]
        [MaxLength(300)]
        public string Address { get; set; } = null!;

        [MaxLength(2000)]
        public string? Description { get; set; }

        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }

        [MaxLength(500)]
        public string? LayoutImageUrl { get; set; }
        public decimal? LayoutWidth { get; set; }
        public decimal? LayoutHeight { get; set; }

        public List<OperatingHoursDto>? OperatingHours { get; set; }
        public List<string>? ImageUrls { get; set; }
        public List<CreateChargingSlotDto>? Slots { get; set; }
    }
}
