namespace ChargeSlot.Api.DTOs.Booking
{
    /// <summary>
    /// Lịch đặt chỗ tại slot — chỉ hiện thông tin thời gian, không lộ danh tính driver.
    /// </summary>
    public class SlotScheduleDto
    {
        public int BookingId { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public decimal DurationHours { get; set; }
        public string Status { get; set; } = string.Empty;
    }
}
