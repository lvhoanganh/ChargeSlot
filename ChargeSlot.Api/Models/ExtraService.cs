namespace ChargeSlot.Api.Models
{
    /// <summary>SRS 1.5 ExtraService - e.g. car wash, fast charging.</summary>
    public class ExtraService
    {
        public int Id { get; set; }
        public int StationId { get; set; }
        public ChargingStation ChargingStation { get; set; } = null!;

        public string ServiceName { get; set; } = null!;
        public string? Description { get; set; }
        public decimal Price { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<BookingExtraService> BookingExtraServices { get; set; } = new List<BookingExtraService>();
    }
}
