namespace ChargeSlot.Api.DTOs.Booking
{
    /// <summary>
    /// Preview phí hủy trước khi Driver xác nhận. FE dùng để hiện popup cảnh báo.
    /// </summary>
    public class CancelPreviewDto
    {
        public int BookingId { get; set; }
        public string Status { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public decimal RefundPercent { get; set; }
        public decimal RefundAmount { get; set; }
        public decimal PenaltyAmount { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
