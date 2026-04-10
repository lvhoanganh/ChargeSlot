using ChargeSlot.Api.Enums;

using ChargeSlot.Api.Helpers;
namespace ChargeSlot.Api.Models
{
    /// <summary>Single leg of double-entry (DEBIT or CREDIT to a Wallet).</summary>
    public class LedgerEntry
    {
        public long Id { get; set; }
        public long LedgerTransactionId { get; set; }
        public LedgerTransaction LedgerTransaction { get; set; } = null!;

        public int WalletId { get; set; }
        public Wallet Wallet { get; set; } = null!;

        public LedgerDirection Direction { get; set; }
        public decimal Amount { get; set; }
        public DateTime CreatedAt { get; set; } = DateTimeHelper.VietnamNow();
    }
}
