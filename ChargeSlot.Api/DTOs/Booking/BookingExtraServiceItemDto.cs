using System.ComponentModel.DataAnnotations;

namespace ChargeSlot.Api.DTOs.Booking
{
    /// <summary>Item dịch vụ thêm khi tạo booking (input DTO).</summary>
    public class BookingExtraServiceItemDto
    {
        [Required]
        public int ServiceId { get; set; }

        [Range(1, 10)]
        public int Quantity { get; set; } = 1;
    }
}
