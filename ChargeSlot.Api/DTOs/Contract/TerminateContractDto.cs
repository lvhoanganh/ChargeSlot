using System.ComponentModel.DataAnnotations;

namespace ChargeSlot.Api.DTOs.Contract
{
    public class TerminateContractDto
    {
        [Required(ErrorMessage = "Vui lòng nhập lý do chấm dứt hợp đồng.")]
        [MinLength(10, ErrorMessage = "Lý do phải có ít nhất 10 ký tự.")]
        public string Reason { get; set; } = null!;
    }
}
