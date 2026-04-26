namespace ChargeSlot.Api.Enums
{
    /// <summary>
    /// Trạng thái định danh (KYC) của Chủ trạm (Owner).
    /// </summary>
    public enum KycStatus
    {
        Unverified = 0,
        Pending = 1,
        Approved = 2,
        Rejected = 3,
        PendingUpdate = 4
    }
}
