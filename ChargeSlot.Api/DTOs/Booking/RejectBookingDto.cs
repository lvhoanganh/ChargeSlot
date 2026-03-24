using System.ComponentModel.DataAnnotations;

namespace ChargeSlot.Api.DTOs.Booking
{
    public class RejectBookingDto
    {
        [Required]
        [MaxLength(500)]
        public string RejectionReason { get; set; } = null!;
    }
}
