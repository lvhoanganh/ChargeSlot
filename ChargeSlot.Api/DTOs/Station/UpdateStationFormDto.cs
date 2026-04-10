using System.ComponentModel.DataAnnotations;

namespace ChargeSlot.Api.DTOs.Station
{
    /// <summary>
    /// DTO nhận multipart/form-data khi Owner cập nhật station.
    /// Giống CreateStationFormDto nhưng dành cho Update.
    /// </summary>
    public class UpdateStationFormDto
    {
        [Required, MaxLength(255)]
        public string Name { get; set; } = null!;

        [Required, MaxLength(300)]
        public string Address { get; set; } = null!;

        [MaxLength(2000)]
        public string? Description { get; set; }

        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }

        public int? LayoutWidth { get; set; }
        public int? LayoutHeight { get; set; }

        /// <summary>Ảnh mới upload từ thiết bị (nhiều file). Nếu null/empty thì giữ nguyên ảnh cũ.</summary>
        public IFormFile[]? Images { get; set; }

        /// <summary>Danh sách URL ảnh cũ muốn GIỮ LẠI. Ảnh cũ không có trong list này sẽ bị xóa.</summary>
        public List<string>? ExistingImageUrls { get; set; }

        /// <summary>Giờ hoạt động (replace toàn bộ nếu có). Null = giữ nguyên.</summary>
        public List<OperatingHoursFormItem>? OperatingHours { get; set; }
    }
}
