using ChargeSlot.Api.Enums;
using ChargeSlot.Api.Models.Identity;
using ChargeSlot.Api.Helpers;

namespace ChargeSlot.Api.Models
{
    /// <summary>Yêu cầu rút tiền từ ví → chờ Admin duyệt.</summary>
    public class WithdrawRequest
    {
        public int Id { get; set; }

        public int UserId { get; set; }
        public ApplicationUser User { get; set; } = null!;

        public int WalletId { get; set; }
        public Wallet Wallet { get; set; } = null!;

        public decimal Amount { get; set; }

        // Snapshot thông tin ngân hàng tại thời điểm yêu cầu
        public string BankName { get; set; } = null!;
        public string BankAccountNumber { get; set; } = null!;
        public string BankAccountHolder { get; set; } = null!;

        public WithdrawStatus Status { get; set; } = WithdrawStatus.Pending;
        public DateTime RequestedAt { get; set; } = DateTimeHelper.VietnamNow();
        public DateTime? ProcessedAt { get; set; }
        public int? ProcessedByUserId { get; set; }
        public string? AdminNote { get; set; }
        public string? UserNote { get; set; }
    }
}
