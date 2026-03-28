using System.ComponentModel.DataAnnotations;

namespace ChargeSlot.Api.DTOs.Auth
{
    public class FirebaseLoginDto
    {
        [Required]
        public string FirebaseIdToken { get; set; } = null!;

        /// <summary>
        /// Role khi đăng ký lần đầu: "Driver" hoặc "Owner". 
        /// Nếu user đã tồn tại thì bỏ qua field này.
        /// </summary>
        [MaxLength(50)]
        public string? Role { get; set; }

        /// <summary>
        /// Tên hiển thị khi đăng ký lần đầu.
        /// </summary>
        [MaxLength(100)]
        public string? FullName { get; set; }
    }
}
