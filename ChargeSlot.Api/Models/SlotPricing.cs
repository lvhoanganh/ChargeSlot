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

        /// <summary>Ưu tiên cao hơn override khi 2 khung giờ chồng nhau.</summary>
        public int Priority { get; set; } = 0;
        /// <summary>Có hiệu lực từ ngày.</summary>
        public DateTime EffectiveFrom { get; set; }
        /// <summary>NULL = vô thời hạn.</summary>
        public DateTime? EffectiveTo { get; set; }

        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
