using System.ComponentModel.DataAnnotations;

namespace ChargeSlot.Api.DTOs.Dispute
{
    public class CreateDisputeDto
    {
        [Required]
        public int BookingId { get; set; }

        [Required]
        [MaxLength(50)]
        public string Reason { get; set; } = null!;

        [Required]
        [MaxLength(2000)]
        public string Description { get; set; } = null!;

        /// <summary>Upload ảnh/video bằng chứng trực tiếp (multipart/form-data).</summary>
        public IFormFile[]? Files { get; set; }
    }
}
