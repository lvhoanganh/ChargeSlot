namespace ChargeSlot.Api.DTOs.Station
{
    public class StationPricingDto
    {
        public int Id { get; set; }
        public int StationId { get; set; }
        public byte? DayOfWeek { get; set; }
        public TimeOnly StartTime { get; set; }
        public TimeOnly EndTime { get; set; }
        public decimal PricePerHour { get; set; }
        public int Priority { get; set; }
        public DateTime EffectiveFrom { get; set; }
        public DateTime? EffectiveTo { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class CreateStationPricingDto
    {
        public byte? DayOfWeek { get; set; }
        public string StartTime { get; set; } = null!; // "00:00"
        public string EndTime { get; set; } = null!;   // "08:00"
        public decimal PricePerHour { get; set; }
        public int Priority { get; set; } = 0;
        public DateTime? EffectiveFrom { get; set; }
        public DateTime? EffectiveTo { get; set; }
    }

    public class UpdateStationPricingDto
    {
        public byte? DayOfWeek { get; set; }
        public string StartTime { get; set; } = null!;
        public string EndTime { get; set; } = null!;
        public decimal PricePerHour { get; set; }
        public int Priority { get; set; } = 0;
        public DateTime? EffectiveFrom { get; set; }
        public DateTime? EffectiveTo { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
