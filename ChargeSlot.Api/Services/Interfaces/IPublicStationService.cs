namespace ChargeSlot.Api.Services.Interfaces
{
    public interface IPublicStationService
    {
        Task<object> GetAllAsync(
            string? keyword,
            decimal? minRating,
            double? lat,
            double? lng,
            double radiusKm,
            DateTime? startTime,
            DateTime? endTime,
            string? sortBy,
            int page,
            int pageSize);

        Task<object> GetNearbyAsync(
            double lat,
            double lng,
            double radiusKm,
            int top);

        Task<object?> GetByIdAsync(int id);
    }
}
