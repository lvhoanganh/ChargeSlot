namespace ChargeSlot.Api.DTOs.Station
{
    public class FavoriteStationDto
    {
        public int StationId { get; set; }
        public string Name { get; set; } = null!;
        public string Address { get; set; } = null!;
        public string? ImageUrl { get; set; }
        public decimal AverageRating { get; set; }
        public int TotalReviews { get; set; }
        public bool IsFavorite { get; set; }
        public DateTime FavoritedAt { get; set; }
    }
}
