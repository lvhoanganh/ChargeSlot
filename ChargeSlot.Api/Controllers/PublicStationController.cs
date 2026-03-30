using ChargeSlot.Api.DTOs.Slot;
using ChargeSlot.Api.DTOs.Station;
using ChargeSlot.Api.Enums;
using ChargeSlot.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ChargeSlot.Api.Constants;

namespace ChargeSlot.Api.Controllers
{
    /// <summary>
    /// Public endpoints cho Driver browse trạm sạc (không cần Owner ownership).
    /// Chỉ hiển thị trạm Approved + Active.
    /// </summary>
    [ApiController]
    [Route("api/public/stations")]
    public class PublicStationController : ControllerBase
    {
        private readonly Data.ChargeSlotDbContext _db;

        public PublicStationController(Data.ChargeSlotDbContext db)
        {
            _db = db;
        }

        /// <summary>
        /// List trạm Approved + Active cho Driver tìm kiếm.
        /// Filter: keyword, minRating, lat/lng/radiusKm (tìm gần).
        /// Sort: name (default), rating, reviews, distance (cần truyền lat/lng).
        /// Pagination: page, pageSize.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAll(
            [FromQuery] string? keyword = null,
            [FromQuery] decimal? minRating = null,
            [FromQuery] double? lat = null,
            [FromQuery] double? lng = null,
            [FromQuery] double radiusKm = 50,
            [FromQuery] string? sortBy = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            var query = _db.ChargingStations
                .Include(s => s.Images)
                .Include(s => s.OperatingHours)
                .Include(s => s.ChargingSlots)
                .Include(s => s.StationPricings)
                .Include(s => s.ExtraServices)
                .Where(s => s.ApprovalStatus == ApprovalStatus.Approved
                    && s.OperationalStatus == OperationalStatus.Active
                    && s.Owner.User.Status == UserStatusConstants.Active);

            // Filter by keyword (tên hoặc địa chỉ)
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var kw = keyword.ToLower();
                query = query.Where(s => s.Name.ToLower().Contains(kw) || s.Address.ToLower().Contains(kw));
            }

            // Filter by minimum rating
            if (minRating.HasValue)
            {
                query = query.Where(s => s.AverageRating >= minRating.Value);
            }

            var stations = await query.ToListAsync();

            // Location-based filter + distance calculation
            List<(ChargingStation station, double? distanceKm)> stationsWithDistance;
            if (lat.HasValue && lng.HasValue)
            {
                stationsWithDistance = stations
                    .Where(s => s.Latitude.HasValue && s.Longitude.HasValue)
                    .Select(s => (station: s, distanceKm: (double?)HaversineKm(lat.Value, lng.Value, (double)s.Latitude!.Value, (double)s.Longitude!.Value)))
                    .Where(x => x.distanceKm <= radiusKm)
                    .ToList();

                // Thêm lại các trạm chưa có tọa độ (hiển thị cuối, distance = null)
                var noCoordStations = stations
                    .Where(s => !s.Latitude.HasValue || !s.Longitude.HasValue)
                    .Select(s => (station: s, distanceKm: (double?)null));
                stationsWithDistance.AddRange(noCoordStations);
            }
            else
            {
                stationsWithDistance = stations.Select(s => (station: s, distanceKm: (double?)null)).ToList();
            }

            // Sort
            stationsWithDistance = sortBy?.ToLower() switch
            {
                "distance" when lat.HasValue && lng.HasValue =>
                    stationsWithDistance.OrderBy(x => x.distanceKm ?? double.MaxValue).ToList(),
                "rating" =>
                    stationsWithDistance.OrderByDescending(x => x.station.AverageRating)
                        .ThenByDescending(x => x.station.TotalReviews).ToList(),
                "reviews" =>
                    stationsWithDistance.OrderByDescending(x => x.station.TotalReviews)
                        .ThenByDescending(x => x.station.AverageRating).ToList(),
                _ when lat.HasValue && lng.HasValue =>
                    stationsWithDistance.OrderBy(x => x.distanceKm ?? double.MaxValue).ToList(),
                _ =>
                    stationsWithDistance.OrderBy(x => x.station.Name).ToList()
            };

            // Pagination
            var total = stationsWithDistance.Count;
            var items = stationsWithDistance
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x =>
                {
                    var dto = MapToPublicDto(x.station);
                    dto.DistanceKm = x.distanceKm.HasValue ? Math.Round(x.distanceKm.Value, 2) : null;
                    return dto;
                })
                .ToList();

            return Ok(new { total, page, pageSize, items });
        }

        /// <summary>
        /// Tìm trạm gần nhất (shortcut cho FE map view).
        /// Trả top N trạm gần nhất theo tọa độ.
        /// </summary>
        [HttpGet("nearby")]
        public async Task<IActionResult> GetNearby(
            [FromQuery] double lat,
            [FromQuery] double lng,
            [FromQuery] double radiusKm = 10,
            [FromQuery] int top = 10)
        {
            var stations = await _db.ChargingStations
                .Include(s => s.Images)
                .Include(s => s.ChargingSlots)
                .Where(s => s.ApprovalStatus == ApprovalStatus.Approved
                    && s.OperationalStatus == OperationalStatus.Active
                    && s.Latitude.HasValue && s.Longitude.HasValue)
                .ToListAsync();

            var nearby = stations
                .Select(s => new
                {
                    Station = s,
                    DistanceKm = HaversineKm(lat, lng, (double)s.Latitude!.Value, (double)s.Longitude!.Value)
                })
                .Where(x => x.DistanceKm <= radiusKm)
                .OrderBy(x => x.DistanceKm)
                .Take(top)
                .Select(x => new
                {
                    x.Station.Id,
                    x.Station.Name,
                    x.Station.Address,
                    x.Station.Latitude,
                    x.Station.Longitude,
                    DistanceKm = Math.Round(x.DistanceKm, 2),
                    AvailableSlots = x.Station.ChargingSlots.Count(sl => sl.Status == SlotStatus.Active),
                    TotalSlots = x.Station.ChargingSlots.Count,
                    x.Station.AverageRating,
                    x.Station.TotalReviews,
                    ThumbnailUrl = x.Station.Images.FirstOrDefault()?.ImageUrl
                })
                .ToList();

            return Ok(nearby);
        }

        /// <summary>Chi tiết 1 trạm (chỉ trả về nếu Approved + Active).</summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var station = await _db.ChargingStations
                .Include(s => s.Images)
                .Include(s => s.OperatingHours)
                .Include(s => s.ChargingSlots)
                .Include(s => s.StationPricings)
                .Include(s => s.ExtraServices)
                .FirstOrDefaultAsync(s => s.Id == id
                    && s.ApprovalStatus == ApprovalStatus.Approved
                    && s.OperationalStatus == OperationalStatus.Active
                    && s.Owner.User.Status == UserStatusConstants.Active);

            if (station == null) return NotFound(new { message = "Trạm sạc không tồn tại hoặc chưa hoạt động." });

            return Ok(MapToPublicDto(station));
        }

        private static ChargingStationDto MapToPublicDto(ChargingStation station)
        {
            return new ChargingStationDto
            {
                Id = station.Id,
                OwnerUserId = station.OwnerUserId,
                Name = station.Name,
                Address = station.Address,
                Description = station.Description,
                Latitude = station.Latitude,
                Longitude = station.Longitude,
                LayoutImageUrl = station.LayoutImageUrl,
                LayoutWidth = station.LayoutWidth,
                LayoutHeight = station.LayoutHeight,
                ApprovalStatus = station.ApprovalStatus.ToString(),
                OperationalStatus = station.OperationalStatus.ToString(),
                CreatedAt = station.CreatedAt,
                Images = station.Images.Select(i => new StationImageDto
                {
                    Id = i.Id,
                    ImageUrl = i.ImageUrl
                }).ToList(),
                OperatingHours = station.OperatingHours.Select(h => new OperatingHoursDto
                {
                    DayOfWeek = h.DayOfWeek,
                    IsClosed = h.IsClosed,
                    OpenTime = h.OpenTime,
                    CloseTime = h.CloseTime
                }).ToList(),
                ChargingSlots = station.ChargingSlots.Select(s => new ChargingSlotDto
                {
                    Id = s.Id,
                    StationId = s.StationId,
                    SlotName = s.SlotName,
                    PositionX = s.PositionX,
                    PositionY = s.PositionY,
                    Status = s.Status.ToString(),
                    CreatedAt = s.CreatedAt,
                    UpdatedAt = s.UpdatedAt
                    // Note: không expose QrCodeToken cho public API
                }).ToList(),
                PricingTiers = station.StationPricings?.Where(p => p.IsActive).Select(p => new StationPricingDto
                {
                    Id = p.Id,
                    StationId = p.StationId,
                    DayOfWeek = p.DayOfWeek,
                    StartTime = p.StartTime,
                    EndTime = p.EndTime,
                    PricePerHour = p.PricePerHour,
                    Priority = p.Priority,
                    EffectiveFrom = p.EffectiveFrom,
                    EffectiveTo = p.EffectiveTo,
                    IsActive = p.IsActive,
                    CreatedAt = p.CreatedAt
                }).OrderBy(p => p.StartTime).ToList() ?? new(),
                ExtraServices = station.ExtraServices?.Where(es => es.IsActive).Select(es => new ExtraServiceDto
                {
                    Id = es.Id,
                    ServiceName = es.ServiceName,
                    Description = es.Description,
                    Price = es.Price,
                    TotalStock = es.TotalStock,
                    IsActive = es.IsActive
                }).ToList() ?? new(),
                AverageRating = station.AverageRating,
                TotalReviews = station.TotalReviews
            };
        }

        /// <summary>
        /// Haversine formula — tính khoảng cách (km) giữa 2 tọa độ GPS.
        /// </summary>
        private static double HaversineKm(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371; // Bán kính Trái Đất (km)
            var dLat = ToRad(lat2 - lat1);
            var dLon = ToRad(lon2 - lon1);
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                  + Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2))
                  * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c;
        }

        private static double ToRad(double deg) => deg * Math.PI / 180;
    }
}
