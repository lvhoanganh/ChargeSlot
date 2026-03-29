using System.ComponentModel.DataAnnotations;

namespace ChargeSlot.Api.DTOs.Auth
{
    public class ResetPasswordDto
    {
        [Required]
        [MaxLength(20)]
        public string PhoneNumber { get; set; } = null!;

        [Required]
        [MinLength(6)]
        [MaxLength(100)]
        public string NewPassword { get; set; } = null!;

        /// <summary>
        /// Firebase ID Token để xác thực SĐT.
        /// </summary>
        [Required]
        public string FirebaseIdToken { get; set; } = null!;
    }
}
