namespace ChargeSlot.Api.DTOs.Dispute
{
    public class DisputeDto
    {
        public int Id { get; set; }
        public int BookingId { get; set; }
        public int? InvoiceId { get; set; }
        public int CreatedByUserId { get; set; }
        public string CreatedByName { get; set; } = null!;
        public string Reason { get; set; } = null!;
        public string Description { get; set; } = null!;
        public string Status { get; set; } = null!;
        public string? OwnerResponse { get; set; }
        public string? AdminNote { get; set; }
        public int? ResolvedByUserId { get; set; }
        public DateTime? ResolvedAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<DisputeEvidenceDto> Evidences { get; set; } = new();
    }

    public class DisputeEvidenceDto
    {
        public int Id { get; set; }
        public int UploadedByUserId { get; set; }
        public string UploadedByName { get; set; } = null!;
        public string FileUrl { get; set; } = null!;
        public string FileType { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
    }
}
