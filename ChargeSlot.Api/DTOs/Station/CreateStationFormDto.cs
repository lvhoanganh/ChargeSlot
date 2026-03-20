using System.ComponentModel.DataAnnotations;

namespace ChargeSlot.Api.DTOs.Station
{
    /// <summary>
    /// DTO nhận multipart/form-data từ FE.
    /// </summary>
    public class CreateStationFormDto
    {
        [Required, MaxLength(255)]
        public string Name { get; set; } = null!;

        [Required, MaxLength(300)]
        public string Address { get; set; } = null!;

        [MaxLength(2000)]
        public string? Description { get; set; }

        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }

        [Required]
        public int LayoutWidth { get; set; }
        [Required]
        public int LayoutHeight { get; set; }

        /// <summary>Ảnh upload từ thiết bị (nhiều file).</summary>
        public IFormFile[]? Images { get; set; }

        /// <summary>Giờ hoạt động (indexed array from form-data).</summary>
        public List<OperatingHoursFormItem>? OperatingHours { get; set; }

        /// <summary>Danh sách ổ sạc.</summary>
        public List<SlotFormItem>? Slots { get; set; }

        /// <summary>Giá theo khung giờ — áp dụng chung cho TẤT CẢ slots.</summary>
        public List<StationPricingFormItem>? StationPricing { get; set; }
    }

    public class OperatingHoursFormItem
    {
        public int DayOfWeek { get; set; }
        public bool IsClosed { get; set; }
        public string? OpenTime { get; set; }  // "HH:mm"
        public string? CloseTime { get; set; } // "HH:mm"
    }

    public class SlotFormItem
    {
        [Required, MaxLength(100)]
        public string SlotName { get; set; } = null!;
        public int? PositionX { get; set; }
        public int? PositionY { get; set; }
    }

    public class StationPricingFormItem
    {
        [Required]
        public string StartTime { get; set; } = null!;  // "HH:mm"
        [Required]
        public string EndTime { get; set; } = null!;     // "HH:mm"
        public decimal PricePerHour { get; set; }
    }
}
