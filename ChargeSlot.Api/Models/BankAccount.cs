using ChargeSlot.Api.Models.Identity;

namespace ChargeSlot.Api.Models
{
    /// <summary>SRS 1.5 - Owner payout method (UC-39).</summary>
    public class BankAccount
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public ApplicationUser User { get; set; } = null!;

        public string BankName { get; set; } = null!;
        public string BankAccountNumber { get; set; } = null!;
        public string BankAccountHolder { get; set; } = null!;
        public bool IsDefault { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<PayoutRequest> PayoutRequests { get; set; } = new List<PayoutRequest>();
    }
}
