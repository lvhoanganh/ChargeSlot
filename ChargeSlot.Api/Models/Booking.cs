using ChargeSlot.Api.Enums;

using ChargeSlot.Api.Helpers;
namespace ChargeSlot.Api.Models
{
    /// <summary>SRS 1.5 Booking - driver, slot, time range, status.</summary>
    public class Booking
    {
        public int Id { get; set; }
        public int DriverUserId { get; set; }
        public Driver Driver { get; set; } = null!;

        public int SlotId { get; set; }
        public ChargingSlot ChargingSlot { get; set; } = null!;

        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public decimal? DurationHours { get; set; }
        public string? Note { get; set; }

        public BookingStatus Status { get; set; } = BookingStatus.Draft;
        public DateTime? PaymentExpiresAt { get; set; }
        public DateTime? CheckedInAt { get; set; }
        public DateTime? CheckinDeadlineAt { get; set; }
        public DateTime? CancelledAt { get; set; }
        public string? CancelReason { get; set; }
        public string? RejectionReason { get; set; }
        public decimal TotalAmount { get; set; }

        /// <summary>Số điểm Driver đã dùng cho booking này.</summary>
        public decimal PointsUsed { get; set; }
        /// <summary>Số tiền giảm tương ứng từ điểm (= PointsUsed vì 1 điểm = 1 VND).</summary>
        public decimal PointsDiscountAmount { get; set; }
        /// <summary>Số điểm nhận được khi booking Completed.</summary>
        public decimal PointsEarned { get; set; }

        public DateTime CreatedAt { get; set; } = DateTimeHelper.VietnamNow();
        public DateTime? UpdatedAt { get; set; }

        // Snapshot Configs for Business Rules
        public DateTime? Refund100DeadlineAt { get; set; }
        public DateTime? Refund50DeadlineAt { get; set; }
        public decimal PlatformFeeRateSnapshot { get; set; } = 0.05m;
        public decimal VatRateSnapshot { get; set; } = 0.08m;
        public decimal LoyaltyEarnRateSnapshot { get; set; } = 0.05m;

        /// <summary>Driver yêu cầu kết thúc sạc sớm.</summary>
        public DateTime? EarlyEndRequestedAt { get; set; }

        /// <summary>Driver gửi yêu cầu xác nhận thủ công (khi không check-in được do lỗi mạng/app).</summary>
        public DateTime? ManualCheckinRequestedAt { get; set; }

        public ICollection<BookingExtraService> BookingExtraServices { get; set; } = new List<BookingExtraService>();
        public Payment? Payment { get; set; }
        public ChargingSession? ChargingSession { get; set; }
        public Invoice? Invoice { get; set; }
        public Rating? Rating { get; set; }
        public Dispute? Dispute { get; set; }
    }
}
