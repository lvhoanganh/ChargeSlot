using System.ComponentModel.DataAnnotations;

namespace ChargeSlot.Api.DTOs.Dispute
{
    public class OwnerEvidenceDto
    {
        [MaxLength(2000)]
        public string? Response { get; set; }

        /// <summary>Upload ảnh/video bằng chứng phản hồi (multipart/form-data).</summary>
        public IFormFile[]? Files { get; set; }
    }
}
