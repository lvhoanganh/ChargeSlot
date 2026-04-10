namespace ChargeSlot.Api.DTOs.Wallet
{
    public class ProcessWithdrawDto
    {
        /// <summary>true = duyệt, false = từ chối</summary>
        public bool Approve { get; set; }
        public string? AdminNote { get; set; }
    }
}
