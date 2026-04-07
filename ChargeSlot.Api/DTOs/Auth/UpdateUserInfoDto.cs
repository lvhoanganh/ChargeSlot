using System.ComponentModel.DataAnnotations;

namespace ChargeSlot.Api.DTOs.Auth
{
    public class UpdateUserInfoDto
    {
        [Required(ErrorMessage = "Vui lòng nhập họ và tên.")]
        [MaxLength(100, ErrorMessage = "Tên không được vượt quá 100 ký tự.")]
        public string FullName { get; set; } = null!;
    }
}
