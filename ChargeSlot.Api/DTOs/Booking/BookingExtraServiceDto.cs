namespace ChargeSlot.Api.DTOs.Booking
{
    /// <summary>Response DTO cho dịch vụ đã chọn trong booking.</summary>
    public class BookingExtraServiceDto
    {
        public int ServiceId { get; set; }
        public string ServiceName { get; set; } = null!;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice { get; set; }
    }
}
