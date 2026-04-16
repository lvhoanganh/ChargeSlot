namespace ChargeSlot.Api.Enums
{
    /// <summary>SRS 1.5 Booking status, UC-15–UC-24.</summary>
    public enum BookingStatus
    {
        WaitingOwner = 1,
        PendingPayment = 2,
        Expired = 3,
        Paid = 4,
        CheckedIn = 5,
        CompletedPendingInvoice = 8,
        Completed = 9,
        Cancelled = 10,
        NoShow = 11,
        Rejected = 12,
        Disputed = 13
    }
}
