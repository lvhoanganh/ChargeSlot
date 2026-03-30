namespace ChargeSlot.Api.DTOs.Station
{
    public class TopFavoriteStationDto
    {
        public int Rank { get; set; }
        public int StationId { get; set; }
        public string Name { get; set; } = null!;
        public string Address { get; set; } = null!;
        public string? ImageUrl { get; set; }
        public decimal AverageRating { get; set; }
        public int TotalReviews { get; set; }
        public int FavoriteCount { get; set; }
    }
}
