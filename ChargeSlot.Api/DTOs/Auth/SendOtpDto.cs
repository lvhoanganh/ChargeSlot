using System.ComponentModel.DataAnnotations;

namespace ChargeSlot.Api.DTOs.Auth
{
    public class SendOtpDto
    {
        [Required]
        [MaxLength(20)]
        public string PhoneNumber { get; set; } = null!;
    }
}
