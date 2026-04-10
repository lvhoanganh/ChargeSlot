using System.ComponentModel.DataAnnotations;

namespace ChargeSlot.Api.DTOs.Auth
{
    public class ResendVerificationDto
    {
        [Required]
        public int UserId { get; set; }
    }
}
