using ChargeSlot.Api.Helpers;
namespace ChargeSlot.Api.Models
{
    /// <summary>Station-level pricing tiers (giá theo khung giờ, áp dụng chung cho tất cả slot).</summary>
    public class StationPricing
    {
        public int Id { get; set; }
        public int StationId { get; set; }
        public ChargingStation ChargingStation { get; set; } = null!;

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
        public DateTime CreatedAt { get; set; } = DateTimeHelper.VietnamNow();
    }
}
