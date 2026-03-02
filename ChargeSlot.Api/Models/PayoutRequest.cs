using ChargeSlot.Api.Enums;
using ChargeSlot.Api.Models.Identity;

namespace ChargeSlot.Api.Models
{
    /// <summary>SRS 1.5 PayoutRequest - owner withdraws available balance (UC-39).</summary>
    public class PayoutRequest
    {
        public int Id { get; set; }
        public int OwnerUserId { get; set; }
        public Owner Owner { get; set; } = null!;

        public int BankAccountId { get; set; }
        public BankAccount BankAccount { get; set; } = null!;

        public decimal Amount { get; set; }
        public PayoutStatus Status { get; set; } = PayoutStatus.Pending;
        public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ProcessedAt { get; set; }
        public int? ProcessedByUserId { get; set; }
        public ApplicationUser? ProcessedByUser { get; set; }
        public string? Note { get; set; }
    }
}
