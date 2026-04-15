using System.ComponentModel.DataAnnotations;

namespace ChargeSlot.Api.DTOs.Contract
{
    public class SignContractDto
    {
        /// <summary>
        /// Base64 encoded signature image. Format: "data:image/png;base64,iVBOR..."
        /// </summary>
        [Required(ErrorMessage = "Chữ ký là bắt buộc.")]
        public string SignatureBase64 { get; set; } = null!;
    }
}
