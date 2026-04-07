using ChargeSlot.Api.Enums;

using ChargeSlot.Api.Helpers;
namespace ChargeSlot.Api.Models
{
    /// <summary>SRS 1.5 ChargingStation - owner, location, approval, operational status.</summary>
    public class ChargingStation
    {
        public int Id { get; set; }
        public int OwnerUserId { get; set; }
        public Owner Owner { get; set; } = null!;

        public string Name { get; set; } = null!;
        public string Address { get; set; } = null!;
        public string? Description { get; set; }
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }

        // Sơ đồ mặt bằng trạm sạc
        public string? LayoutImageUrl { get; set; }
        public decimal? LayoutWidth { get; set; }
        public decimal? LayoutHeight { get; set; }

        public ApprovalStatus ApprovalStatus { get; set; } = ApprovalStatus.Draft;
        public OperationalStatus OperationalStatus { get; set; } = OperationalStatus.Inactive;
        public DateTime? SubmittedAt { get; set; }
        public DateTime? ReviewedAt { get; set; }
        /// <summary>Admin Id (0) — không FK vì Admin config trong appsettings, không lưu DB.</summary>
        public int? ReviewedByUserId { get; set; }
        public string? AdminNote { get; set; }

        public DateTime CreatedAt { get; set; } = DateTimeHelper.VietnamNow();
        public DateTime? UpdatedAt { get; set; }

        // Tracking Dispute Bans
        public int BanCount { get; set; } = 0;
        public DateTime? BannedUntil { get; set; }

        /// <summary>Đánh giá trung bình (denormalized for performance).</summary>
        public decimal AverageRating { get; set; } = 0;
        /// <summary>Tổng số đánh giá.</summary>
        public int TotalReviews { get; set; } = 0;

        public ICollection<StationImage> Images { get; set; } = new List<StationImage>();
        public ICollection<StationOperatingHours> OperatingHours { get; set; } = new List<StationOperatingHours>();
        public ICollection<StationUnavailableDate> UnavailableDates { get; set; } = new List<StationUnavailableDate>();
        public ICollection<ExtraService> ExtraServices { get; set; } = new List<ExtraService>();
        public ICollection<ChargingSlot> ChargingSlots { get; set; } = new List<ChargingSlot>();
        public ICollection<StationPricing> StationPricings { get; set; } = new List<StationPricing>();
        public ICollection<Rating> Ratings { get; set; } = new List<Rating>();
    }
}
