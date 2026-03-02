using ChargeSlot.Api.Enums;
using ChargeSlot.Api.Models.Identity;

namespace ChargeSlot.Api.Models
{
    /// <summary>SRS 1.5 OwnerBalance / ví tiền - DRIVER, OWNER, SYSTEM (ESCROW, PLATFORM_REVENUE).</summary>
    public class Wallet
    {
        public int Id { get; set; }
        public WalletType WalletType { get; set; }
        public int? UserId { get; set; }
        public ApplicationUser? User { get; set; }
        /// <summary>System wallets: ESCROW, PLATFORM_REVENUE, CLEARING.</summary>
        public string? SystemCode { get; set; }

        public decimal AvailableBalance { get; set; }
        public decimal FrozenBalance { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<LedgerEntry> LedgerEntries { get; set; } = new List<LedgerEntry>();
    }
}
