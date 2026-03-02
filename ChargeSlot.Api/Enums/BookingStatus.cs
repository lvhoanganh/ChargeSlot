namespace ChargeSlot.Api.Enums
{
    /// <summary>SRS 1.5 Booking status, UC-15–UC-24.</summary>
    public enum BookingStatus
    {
        Draft = 0,
        PendingPayment = 1,
        Paid = 2,
        CheckedIn = 3,
        InProgress = 4,
        CompletedPendingResult = 5,
        CompletedPendingInvoice = 6,
        Completed = 7,
        Cancelled = 8,
        NoShow = 9,
        Rejected = 10,
        Disputed = 11
    }
}
