using ChargeSlot.Api.Models.Identity;

namespace ChargeSlot.Api.Models
{
    /// <summary>SRS UC-23 - evidence (images/documents) for dispute.</summary>
    public class DisputeEvidence
    {
        public int Id { get; set; }
        public int DisputeId { get; set; }
        public Dispute Dispute { get; set; } = null!;

        /// <summary>Ai upload: DriverUserId hoặc OwnerUserId.</summary>
        public int UploadedByUserId { get; set; }
        public ApplicationUser UploadedByUser { get; set; } = null!;

        public string FileUrl { get; set; } = null!;
        public string FileType { get; set; } = null!;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
