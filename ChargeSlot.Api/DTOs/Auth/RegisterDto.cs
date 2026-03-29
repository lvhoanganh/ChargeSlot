using System.ComponentModel.DataAnnotations;

namespace ChargeSlot.Api.DTOs.Auth
{
    public class RegisterDto
    {
        [Required]
        [MaxLength(20)]
        public string PhoneNumber { get; set; } = null!;

        [Required]
        [MaxLength(100)]
        public string FullName { get; set; } = null!;

        [Required]
        [MinLength(6)]
        [MaxLength(100)]
        public string Password { get; set; } = null!;

        [MaxLength(50)]
        public string? Role { get; set; }

        /// <summary>
        /// Firebase ID Token để xác thực SĐT (thay thế OTP cũ).
        /// Frontend gửi OTP qua Firebase → verify → lấy token → gửi kèm khi register.
        /// </summary>
        [Required]
        public string FirebaseIdToken { get; set; } = null!;
    }
}
