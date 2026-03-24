using System.ComponentModel.DataAnnotations;

namespace ChargeSlot.Api.DTOs.Auth
{
    public class VerifyOtpDto
    {
        [Required]
        [MaxLength(20)]
        public string PhoneNumber { get; set; } = null!;

        [Required]
        [StringLength(6, MinimumLength = 4)]
        public string Otp { get; set; } = null!;
    }
}
