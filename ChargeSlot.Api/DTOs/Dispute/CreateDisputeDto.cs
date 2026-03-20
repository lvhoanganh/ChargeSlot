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

        /// <summary>URLs of evidence files (photos, screenshots).</summary>
        public List<EvidenceFileDto>? Evidences { get; set; }
    }

    public class EvidenceFileDto
    {
        [Required]
        public string FileUrl { get; set; } = null!;

        [Required]
        [MaxLength(20)]
        public string FileType { get; set; } = null!; // "image", "video", "document"
    }
}
