namespace ChargeSlot.Api.DTOs.Contract
{
    public class ContractPreviewDto
    {
        public int ContractId { get; set; }
        public string ContractNumber { get; set; } = null!;
        public string? OwnerName { get; set; }
        public int OwnerUserId { get; set; }
        public string Status { get; set; } = null!;
        public string ContractHtml { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        public DateTime? SignedAt { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public int ContractDurationMonths { get; set; }
        public string? SignedPdfUrl { get; set; }
    }
}
