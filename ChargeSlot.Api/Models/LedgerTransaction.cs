using ChargeSlot.Api.Models.Identity;

namespace ChargeSlot.Api.Models
{
    /// <summary>Reference for double-entry ledger (payment, payout, refund, etc.).</summary>
    public class LedgerTransaction
    {
        public long Id { get; set; }
        public string ReferenceType { get; set; } = null!;
        public long ReferenceId { get; set; }
        public string? Memo { get; set; }
        public int? CreatedByUserId { get; set; }
        public ApplicationUser? CreatedByUser { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<LedgerEntry> Entries { get; set; } = new List<LedgerEntry>();
    }
}
