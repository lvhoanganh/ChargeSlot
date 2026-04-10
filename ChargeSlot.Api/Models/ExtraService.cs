using ChargeSlot.Api.Helpers;
namespace ChargeSlot.Api.Models
{
    /// <summary>SRS 1.5 ExtraService - e.g. cho thuê củ sạc, bơm lốp, rửa xe.</summary>
    public class ExtraService
    {
        public int Id { get; set; }
        public int StationId { get; set; }
        public ChargingStation ChargingStation { get; set; } = null!;

        public string ServiceName { get; set; } = null!;
        public string? Description { get; set; }
        public decimal Price { get; set; }
        /// <summary>NULL = không giới hạn (dịch vụ). Có giá trị = số lượng vật lý cho thuê (ví dụ: 5 củ sạc).</summary>
        public int? TotalStock { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTimeHelper.VietnamNow();

        public ICollection<BookingExtraService> BookingExtraServices { get; set; } = new List<BookingExtraService>();
    }
}
