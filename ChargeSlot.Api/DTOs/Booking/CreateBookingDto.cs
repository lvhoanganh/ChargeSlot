using System.ComponentModel.DataAnnotations;

namespace ChargeSlot.Api.DTOs.Booking
{
    public class CreateBookingDto
    {
        [Required]
        public int SlotId { get; set; }

        [Required]
        public DateTime StartTime { get; set; }

        [Required]
        [Range(0.5, 24)]
        public decimal DurationHours { get; set; }

        public string? Note { get; set; }

        /// <summary>Dịch vụ thêm (cho thuê sạc, nước uống...) — optional, giống topping.</summary>
        public List<BookingExtraServiceItemDto>? ExtraServices { get; set; }

        /// <summary>Số điểm tích lũy muốn dùng (0 = không dùng). 1 điểm = 1 VND.</summary>
        public decimal PointsToUse { get; set; } = 0;
    }
}
