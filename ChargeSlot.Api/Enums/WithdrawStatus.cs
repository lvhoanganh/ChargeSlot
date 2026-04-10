namespace ChargeSlot.Api.Enums
{
    /// <summary>Trạng thái yêu cầu rút tiền từ ví.</summary>
    public enum WithdrawStatus
    {
        /// <summary>Chờ Admin duyệt.</summary>
        Pending = 0,
        /// <summary>Admin đã duyệt, chờ chuyển khoản.</summary>
        Approved = 1,
        /// <summary>Admin từ chối, hoàn tiền.</summary>
        Rejected = 2,
        /// <summary>Admin đã chuyển khoản + upload ảnh bill, chờ User xác nhận.</summary>
        TransferCompleted = 3,
        /// <summary>User xác nhận đã nhận tiền (hoặc auto 24h). Tiền rời hệ thống.</summary>
        Completed = 4,
        /// <summary>User báo chưa nhận được tiền.</summary>
        IssueReported = 5
    }
}
