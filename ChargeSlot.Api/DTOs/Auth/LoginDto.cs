using System.ComponentModel.DataAnnotations;

namespace ChargeSlot.Api.DTOs.Auth
{
    public class LoginDto
    {
        [Required]
        [MaxLength(20)]
        public string PhoneNumber { get; set; } = null!;

        [Required]
        [MinLength(6)]
        [MaxLength(100)]
        public string Password { get; set; } = null!;
    }
}
