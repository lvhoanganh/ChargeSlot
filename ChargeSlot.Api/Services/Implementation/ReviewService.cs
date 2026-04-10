using ChargeSlot.Api.DTOs.Review;
using ChargeSlot.Api.Enums;
using ChargeSlot.Api.Models;
using ChargeSlot.Api.Repositories.Interfaces;
using ChargeSlot.Api.Services.Interfaces;

using ChargeSlot.Api.Helpers;
namespace ChargeSlot.Api.Services.Implementation
{
    public class ReviewService : IReviewService
    {
        private readonly IBookingRepository _bookingRepo;
        private readonly IRatingRepository _ratingRepo;
        private readonly IChargingStationRepository _stationRepo;
        private readonly IUnitOfWork _unitOfWork;
        private readonly INotificationService _notificationService;

        public ReviewService(
            IBookingRepository bookingRepo,
            IRatingRepository ratingRepo,
            IChargingStationRepository stationRepo,
            IUnitOfWork unitOfWork,
            INotificationService notificationService)
        {
            _bookingRepo = bookingRepo;
            _ratingRepo = ratingRepo;
            _stationRepo = stationRepo;
            _unitOfWork = unitOfWork;
            _notificationService = notificationService;
        }

        /// <summary>
        /// Driver đánh giá trạm sạc sau khi booking hoàn thành.
        /// </summary>
        public async Task<ReviewDto> CreateReviewAsync(int driverUserId, CreateReviewDto dto)
        {
            var booking = await _bookingRepo.GetByIdWithDetailsAsync(dto.BookingId)
                ?? throw new InvalidOperationException("Booking không tồn tại.");

            if (booking.DriverUserId != driverUserId)
                throw new InvalidOperationException("Booking này không thuộc về bạn.");

            if (booking.Status != BookingStatus.Completed)
                throw new InvalidOperationException("Chỉ có thể đánh giá sau khi booking hoàn thành.");

            var exists = await _ratingRepo.HasRatingForBookingAsync(dto.BookingId);
            if (exists)
                throw new InvalidOperationException("Bạn đã đánh giá booking này rồi.");

            var stationId = booking.ChargingSlot.StationId;

            var rating = new Rating
            {
                BookingId = dto.BookingId,
                StationId = stationId,
                DriverUserId = driverUserId,
                Score = dto.Rating,
                Comment = dto.Comment,
                IsAnonymous = dto.IsAnonymous,
                CreatedAt = DateTimeHelper.VietnamNow()
            };

            _ratingRepo.Add(rating);
            await _unitOfWork.CompleteAsync();

            // Recalculate station average
            await RecalculateStationRatingAsync(stationId);

            // Notify Owner
            var ownerUserId = booking.ChargingSlot.ChargingStation.OwnerUserId;
            var starText = new string('⭐', dto.Rating);
            await _notificationService.SendAsync(
                ownerUserId,
                "Đánh giá mới",
                $"Trạm {booking.ChargingSlot?.ChargingStation?.Name} nhận được đánh giá {starText} ({dto.Rating}/5).{(dto.Comment != null ? $" \"{dto.Comment}\"" : "")}",
                NotificationType.Booking);

            return await GetRatingDtoAsync(rating.Id);
        }

        /// <summary>
        /// Owner phản hồi đánh giá.
        /// </summary>
        public async Task<ReviewDto> ReplyToReviewAsync(int ownerUserId, int ratingId, OwnerReplyDto dto)
        {
            var rating = await _ratingRepo.GetByIdWithStationAsync(ratingId)
                ?? throw new InvalidOperationException("Đánh giá không tồn tại.");

            if (rating.ChargingStation.OwnerUserId != ownerUserId)
                throw new UnauthorizedAccessException("Bạn không có quyền phản hồi đánh giá này.");

            if (rating.OwnerReply != null)
                throw new InvalidOperationException("Đã phản hồi đánh giá này rồi.");

            rating.OwnerReply = dto.Reply;
            rating.OwnerRepliedAt = DateTimeHelper.VietnamNow();
            await _unitOfWork.CompleteAsync();

            await _notificationService.SendAsync(
                rating.DriverUserId,
                "Chủ trạm đã phản hồi đánh giá",
                $"Chủ trạm {rating.ChargingStation?.Name} phản hồi đánh giá của bạn: \"{dto.Reply}\"",
                NotificationType.Booking);

            return await GetRatingDtoAsync(rating.Id);
        }

        /// <summary>
        /// Danh sách đánh giá của 1 trạm (mới nhất trước, phân trang).
        /// </summary>
        public async Task<object> GetByStationAsync(int stationId, int page = 1, int pageSize = 10)
        {
            var (items, totalCount) = await _ratingRepo.GetRatingsByStationPagedAsync(stationId, page, pageSize);

            return new
            {
                items = items.Select(MapToDto).ToList(),
                totalCount,
                page,
                pageSize
            };
        }

        /// <summary>
        /// Tổng quan rating (breakdown theo sao).
        /// </summary>
        public async Task<StationRatingSummaryDto?> GetRatingSummaryAsync(int stationId)
        {
            var station = await _stationRepo.GetByIdAsync(stationId, includeDetails: false);
            if (station == null) return null;

            var lookup = await _ratingRepo.GetRatingCountsByStationAsync(stationId);

            return new StationRatingSummaryDto
            {
                StationId = stationId,
                StationName = station.Name,
                AverageRating = station.AverageRating,
                TotalReviews = station.TotalReviews,
                Star5 = lookup.GetValueOrDefault(5),
                Star4 = lookup.GetValueOrDefault(4),
                Star3 = lookup.GetValueOrDefault(3),
                Star2 = lookup.GetValueOrDefault(2),
                Star1 = lookup.GetValueOrDefault(1)
            };
        }

        /// <summary>
        /// Top trạm sạc theo rating (cho trang chủ, kiểu BeFood).
        /// </summary>
        public async Task<List<RankedStationDto>> GetTopRatedStationsAsync(int limit = 10)
        {
            var stations = await _stationRepo.GetTopRatedStationsAsync(limit);

            return stations.Select(s => new RankedStationDto
            {
                Id = s.Id,
                Name = s.Name,
                Address = s.Address,
                ImageUrl = s.Images.FirstOrDefault()?.ImageUrl,
                AverageRating = s.AverageRating,
                TotalReviews = s.TotalReviews,
                TotalSlots = s.ChargingSlots.Count
            }).ToList();
        }

        // ─────────────── HELPERS ───────────────

        private async Task RecalculateStationRatingAsync(int stationId)
        {
            var station = await _stationRepo.GetByIdAsync(stationId, tracking: true, includeDetails: false);
            if (station == null) return;

            var stats = await _ratingRepo.GetRatingStatsAsync(stationId);

            if (stats != null)
            {
                station.AverageRating = Math.Round(stats.Value.Average, 1);
                station.TotalReviews = stats.Value.Count;
                _stationRepo.Update(station);
                await _unitOfWork.CompleteAsync();
            }
        }

        private async Task<ReviewDto> GetRatingDtoAsync(int ratingId)
        {
            var rating = await _ratingRepo.GetByIdWithDetailsAsync(ratingId)
                ?? throw new InvalidOperationException("Rating not found.");
            return MapToDto(rating);
        }

        private static ReviewDto MapToDto(Rating r)
        {
            return new ReviewDto
            {
                Id = r.Id,
                BookingId = r.BookingId,
                StationId = r.StationId,
                DriverUserId = r.DriverUserId,
                DriverName = r.IsAnonymous ? "Ẩn danh" : (r.DriverUser?.FullName ?? ""),
                DriverAvatarUrl = r.IsAnonymous ? null : r.DriverUser?.AvatarUrl,
                Rating = r.Score,
                Comment = r.Comment,
                OwnerReply = r.OwnerReply,
                OwnerRepliedAt = r.OwnerRepliedAt,
                CreatedAt = r.CreatedAt
            };
        }
    }
}
