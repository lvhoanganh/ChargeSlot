using ChargeSlot.Api.Helpers;
namespace ChargeSlot.Api.Models
{
    /// <summary>Cấu hình hệ thống (key-value), quản lý bởi Admin.</summary>
    public class SystemConfig
    {
        public string Key { get; set; } = null!;
        public string Value { get; set; } = null!;
        public string? Description { get; set; }
        public DateTime UpdatedAt { get; set; } = DateTimeHelper.VietnamNow();
    }
}
