using ChargeSlot.Api.Enums;
using ChargeSlot.Api.Models.Identity;

namespace ChargeSlot.Api.Models
{
    /// <summary>
    /// Hợp đồng hợp tác kinh doanh giữa ChargeSlot (Bên A) và Owner (Bên B).
    /// Snapshot PII tại thời điểm tạo — không thay đổi dù Owner cập nhật KYC sau.
    /// </summary>
    public class Contract
    {
        public int Id { get; set; }
        public int OwnerUserId { get; set; }
        public Owner Owner { get; set; } = null!;

        /// <summary>Số hợp đồng, VD: "CS-2026-0001"</summary>
        public string ContractNumber { get; set; } = null!;
        public ContractStatus Status { get; set; } = ContractStatus.Pending;

        // ── Snapshot PII Bên B (cố định tại thời điểm tạo) ──
        public string OwnerName { get; set; } = null!;
        public string OwnerIdCard { get; set; } = null!;
        public string OwnerTaxCode { get; set; } = null!;
        public string OwnerAddress { get; set; } = null!;
        public string OwnerBusinessLicense { get; set; } = null!;
        public string OwnerPhone { get; set; } = null!;
        public string OwnerEmail { get; set; } = null!;

        // ── Chữ ký + PDF ──
        public string? SignatureImageUrl { get; set; }
        public string? SignedPdfUrl { get; set; }

        // ── Timestamps ──
        public DateTime CreatedAt { get; set; }
        public DateTime? SignedAt { get; set; }
        public int ContractDurationMonths { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public DateTime? RenewalNotifiedAt { get; set; }
        public DateTime? TerminatedAt { get; set; }
        public string? TerminationReason { get; set; }
    }
}
