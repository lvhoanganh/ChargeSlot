using System.ComponentModel.DataAnnotations;

namespace ChargeSlot.Api.DTOs.Auth
{
    public class VerifyEmailDto
    {
        [Required]
        public int UserId { get; set; }

        [Required]
        public string Token { get; set; } = null!;
    }
}
