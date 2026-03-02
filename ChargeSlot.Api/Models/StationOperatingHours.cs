namespace ChargeSlot.Api.Models
{
    /// <summary>Operating hours per day of week (0=Sunday..6=Saturday).</summary>
    public class StationOperatingHours
    {
        public int StationId { get; set; }
        public ChargingStation ChargingStation { get; set; } = null!;

        public byte DayOfWeek { get; set; }
        public bool IsClosed { get; set; }
        public TimeOnly? OpenTime { get; set; }
        public TimeOnly? CloseTime { get; set; }
    }
}
