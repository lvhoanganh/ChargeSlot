using System.ComponentModel.DataAnnotations;

namespace ChargeSlot.Api.DTOs.Admin
{
    public class SetupSecondaryPasswordDto
    {
        [Required(ErrorMessage = "Vui lòng nhập mật khẩu cấp 2")]
        [MinLength(6, ErrorMessage = "Mật khẩu cấp 2 phải có ít nhất 6 ký tự")]
        public string NewSecondaryPassword { get; set; } = null!;
        
        [Required(ErrorMessage = "Vui lòng nhập mật khẩu tài khoản cấp 1 để xác thực")]
        public string PrimaryPassword { get; set; } = null!;
    }

    public class ResetSecondaryPasswordRequestDto
    {
        // Gửi OTP về email admin
        // Không nhận email từ form, lấy trực tiếp email của Admin đang đăng nhập
    }

    public class ConfirmResetSecondaryPasswordDto
    {
        [Required(ErrorMessage = "Vui lòng nhập mã OTP")]
        public string OtpCode { get; set; } = null!;

        [Required(ErrorMessage = "Vui lòng nhập mật khẩu cấp 2 mới")]
        [MinLength(6, ErrorMessage = "Mật khẩu cấp 2 phải có ít nhất 6 ký tự")]
        public string NewSecondaryPassword { get; set; } = null!;
    }
}
