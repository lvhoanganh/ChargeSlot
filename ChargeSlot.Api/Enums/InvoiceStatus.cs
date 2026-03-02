namespace ChargeSlot.Api.Enums
{
    /// <summary>SRS 1.5 Invoice status, UC-21, UC-22.</summary>
    public enum InvoiceStatus
    {
        PendingConfirm = 0,
        Confirmed = 1,
        UnderDispute = 2
    }
}
