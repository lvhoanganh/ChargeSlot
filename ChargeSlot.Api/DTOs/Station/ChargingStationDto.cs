namespace ChargeSlot.Api.DTOs.Station
{
    public class ChargingStationDto
    {
        public int Id { get; set; }
        public int OwnerUserId { get; set; }
        public string Name { get; set; } = null!;
        public string Address { get; set; } = null!;
        public string? Description { get; set; }
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }

        public string? LayoutImageUrl { get; set; }
        public decimal? LayoutWidth { get; set; }
        public decimal? LayoutHeight { get; set; }

        public string ApprovalStatus { get; set; } = null!;
        public string OperationalStatus { get; set; } = null!;
        public string? AdminNote { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public List<StationImageDto> Images { get; set; } = new();
        public List<OperatingHoursDto> OperatingHours { get; set; } = new();
        public List<DTOs.Slot.ChargingSlotDto> ChargingSlots { get; set; } = new();
        public List<StationPricingDto> PricingTiers { get; set; } = new();
        public List<ExtraServiceDto> ExtraServices { get; set; } = new();
        public decimal AverageRating { get; set; }
        public int TotalReviews { get; set; }
        public double? DistanceKm { get; set; }
    }
}
