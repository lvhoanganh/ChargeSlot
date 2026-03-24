namespace ChargeSlot.Api.Enums
{
    /// <summary>SRS 2.4.2 - Dispute lifecycle.</summary>
    public enum DisputeStatus
    {
        Open = 0,
        WaitingOwnerEvidence = 1,
        PendingReview = 2,
        ResolvedRefund = 3,
        ResolvedPayout = 4
    }
}
