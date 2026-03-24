namespace ChargeSlot.Api.Enums
{
    /// <summary>Charging station approval status (SRS 2.3.2, 2.3.5, 2.4.1).</summary>
    public enum ApprovalStatus
    {
        Draft = 0,
        PendingApproval = 1,
        Approved = 2,
        Rejected = 3
    }
}
