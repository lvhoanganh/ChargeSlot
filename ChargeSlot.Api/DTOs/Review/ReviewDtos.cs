using System.ComponentModel.DataAnnotations;

namespace ChargeSlot.Api.DTOs.Review
{
    public class CreateReviewDto
    {
        [Required]
        public int BookingId { get; set; }

        [Required]
        [Range(1, 5)]
        public int Rating { get; set; }

        [MaxLength(1000)]
        public string? Comment { get; set; }
    }

    public class OwnerReplyDto
    {
        [Required]
        [MaxLength(1000)]
        public string Reply { get; set; } = null!;
    }

    public class ReviewDto
    {
        public int Id { get; set; }
        public int BookingId { get; set; }
        public int StationId { get; set; }
        public int DriverUserId { get; set; }
        public string DriverName { get; set; } = null!;
        public string? DriverAvatarUrl { get; set; }
        public int Rating { get; set; }
        public string? Comment { get; set; }
        public string? OwnerReply { get; set; }
        public DateTime? OwnerRepliedAt { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class StationRatingSummaryDto
    {
        public int StationId { get; set; }
        public string StationName { get; set; } = null!;
        public decimal AverageRating { get; set; }
        public int TotalReviews { get; set; }
        public int Star5 { get; set; }
        public int Star4 { get; set; }
        public int Star3 { get; set; }
        public int Star2 { get; set; }
        public int Star1 { get; set; }
    }
}
