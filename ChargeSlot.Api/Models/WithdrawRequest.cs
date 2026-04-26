using ChargeSlot.Api.Enums;
using ChargeSlot.Api.Models.Identity;
using ChargeSlot.Api.Helpers;

namespace ChargeSlot.Api.Models
{
    /// <summary>Yêu cầu rút tiền từ ví → luồng xác nhận đa bước.</summary>
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

        // Admin duyệt/từ chối
        public DateTime? ProcessedAt { get; set; }
        public int? ProcessedByUserId { get; set; }
        public string? AdminNote { get; set; }
        public string? UserNote { get; set; }

        // Admin chuyển khoản
        /// <summary>URL ảnh biên lai chuyển khoản (Firebase Storage).</summary>
        public string? TransferReceiptUrl { get; set; }
        /// <summary>Thời điểm Admin xác nhận đã chuyển khoản.</summary>
        public DateTime? TransferredAt { get; set; }

        // User xác nhận
        /// <summary>Thời điểm User xác nhận đã nhận tiền.</summary>
        public DateTime? UserConfirmedAt { get; set; }

        // User báo lỗi
        /// <summary>Thời điểm User báo chưa nhận được tiền.</summary>
        public DateTime? IssueReportedAt { get; set; }
        /// <summary>Ghi chú lý do chưa nhận được tiền.</summary>
        public string? IssueNote { get; set; }

        /// <summary>Đã gửi nhắc nhở trước auto-confirm (tránh gửi trùng).</summary>
        public DateTime? ReminderSentAt { get; set; }
    }
}
