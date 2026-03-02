namespace ChargeSlot.Api.Models
{
    /// <summary>Optional time-of-day pricing per slot (SQL SlotPricing).</summary>
    public class SlotPricing
    {
        public int Id { get; set; }
        public int SlotId { get; set; }
        public ChargingSlot ChargingSlot { get; set; } = null!;

        public byte? DayOfWeek { get; set; }
        public TimeOnly StartTime { get; set; }
        public TimeOnly EndTime { get; set; }
        public decimal PricePerHour { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
