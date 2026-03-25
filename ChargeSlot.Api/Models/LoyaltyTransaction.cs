using ChargeSlot.Api.Helpers;
namespace ChargeSlot.Api.Models
{
    /// <summary>Lịch sử tích/dùng điểm tích lũy.</summary>
    public class LoyaltyTransaction
    {
        public int Id { get; set; }
        public int DriverUserId { get; set; }
        public Driver Driver { get; set; } = null!;

        public int? BookingId { get; set; }
        public Booking? Booking { get; set; }

        /// <summary>"Earn" hoặc "Redeem"</summary>
        public string Type { get; set; } = null!;
        public decimal Points { get; set; }
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; } = DateTimeHelper.VietnamNow();
    }
}
