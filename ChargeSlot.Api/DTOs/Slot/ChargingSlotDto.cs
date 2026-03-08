namespace ChargeSlot.Api.DTOs.Slot
{
    public class ChargingSlotDto
    {
        public int Id { get; set; }
        public int StationId { get; set; }
        public string SlotName { get; set; } = null!;
        public string ConnectorType { get; set; } = null!;
        public decimal? PowerKw { get; set; }
        public decimal BasePricePerHour { get; set; }
        public decimal? PositionX { get; set; }
        public decimal? PositionY { get; set; }
        public string Status { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
