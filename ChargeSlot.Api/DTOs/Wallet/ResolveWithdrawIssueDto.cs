namespace ChargeSlot.Api.DTOs.Wallet
{
    /// <summary>Admin xử lý issue rút tiền.</summary>
    public class ResolveWithdrawIssueDto
    {
        /// <summary>true = hoàn tiền (Rejected), false = chuyển lại (TransferCompleted).</summary>
        public bool Refund { get; set; }
        public string? AdminNote { get; set; }
    }
}
