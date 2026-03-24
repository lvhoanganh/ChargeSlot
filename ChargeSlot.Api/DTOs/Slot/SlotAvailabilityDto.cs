namespace ChargeSlot.Api.DTOs.Slot
{
    public class SlotAvailabilityDto
    {
        public int SlotId { get; set; }
        public string SlotName { get; set; } = null!;
        public string Status { get; set; } = null!;
        public List<BookedTimeRangeDto> BookedRanges { get; set; } = new();
        public DateTime? NextAvailableAt { get; set; }
    }

    public class BookedTimeRangeDto
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }       // Đã bao gồm 15 phút buffer
        public string Status { get; set; } = null!;  // PendingPayment, Paid, CheckedIn...
    }
}
