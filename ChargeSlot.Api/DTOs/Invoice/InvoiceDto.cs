namespace ChargeSlot.Api.DTOs.Invoice
{
    public class InvoiceDto
    {
        public int Id { get; set; }
        public int BookingId { get; set; }
        public string? DriverName { get; set; }
        public string? StationName { get; set; }
        public decimal ChargingAmount { get; set; }
        public decimal ServiceAmount { get; set; }
        public decimal VatAmount { get; set; }
        public decimal PlatformFee { get; set; }
        public decimal TotalAmount { get; set; }
        public string Status { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
