namespace ChargeSlot.Api.DTOs.Slot
{
    public class ChargingSlotDto
    {
        public int Id { get; set; }
        public int StationId { get; set; }
        public string SlotName { get; set; } = null!;
        public decimal BasePricePerHour { get; set; }
        public decimal? PositionX { get; set; }
        public decimal? PositionY { get; set; }
        public string? QrCodeToken { get; set; }
        public string Status { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public List<SlotPricingDto>? PricingTiers { get; set; }
    }
}
