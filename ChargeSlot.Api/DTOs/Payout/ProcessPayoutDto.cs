namespace ChargeSlot.Api.DTOs.Payout
{
    public class ProcessPayoutDto
    {
        /// <summary>true = duyệt, false = từ chối</summary>
        public bool Approve { get; set; }
        public string? Note { get; set; }
    }
}
