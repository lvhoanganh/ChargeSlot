using ChargeSlot.Api.DTOs.Review;

namespace ChargeSlot.Api.Services.Interfaces
{
    public interface IReviewService
    {
        Task<ReviewDto> CreateReviewAsync(int driverUserId, CreateReviewDto dto);
        Task<ReviewDto> ReplyToReviewAsync(int ownerUserId, int reviewId, OwnerReplyDto dto);
        Task<object> GetByStationAsync(int stationId, int page = 1, int pageSize = 10);
        Task<StationRatingSummaryDto?> GetRatingSummaryAsync(int stationId);
        Task<List<RankedStationDto>> GetTopRatedStationsAsync(int limit = 10);
    }
}
