using ChargeSlot.Api.Enums;
using ChargeSlot.Api.Models.Identity;

using ChargeSlot.Api.Helpers;
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
        public DateTime RequestedAt { get; set; } = DateTimeHelper.VietnamNow();
        public DateTime? ProcessedAt { get; set; }
        /// <summary>Admin Id (0) — không FK vì Admin config trong appsettings, không lưu DB.</summary>
        public int? ProcessedByUserId { get; set; }
        public string? Note { get; set; }
    }
}
