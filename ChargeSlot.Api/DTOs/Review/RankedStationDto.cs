namespace ChargeSlot.Api.DTOs.Review
{
    public class RankedStationDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public string Address { get; set; } = null!;
        public string? ImageUrl { get; set; }
        public decimal AverageRating { get; set; }
        public int TotalReviews { get; set; }
        public int TotalSlots { get; set; }
    }
}
