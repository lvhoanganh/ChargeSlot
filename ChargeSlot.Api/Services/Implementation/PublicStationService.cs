using ChargeSlot.Api.DTOs.Slot;
using ChargeSlot.Api.DTOs.Station;
using ChargeSlot.Api.Enums;
using ChargeSlot.Api.Models;
using ChargeSlot.Api.Repositories.Interfaces;
using ChargeSlot.Api.Services.Interfaces;
using ChargeSlot.Api.Helpers;

namespace ChargeSlot.Api.Services.Implementation
{
    public class PublicStationService : IPublicStationService
    {
        private readonly IChargingStationRepository _stationRepo;
        private readonly IBookingRepository _bookingRepo;
        private readonly ISystemConfigService _configService;

        public PublicStationService(
            IChargingStationRepository stationRepo,
            IBookingRepository bookingRepo,
            ISystemConfigService configService)
        {
            _stationRepo = stationRepo;
            _bookingRepo = bookingRepo;
            _configService = configService;
        }

        public async Task<object> GetAllAsync(
            string? keyword,
            decimal? minRating,
            double? lat,
            double? lng,
            double radiusKm,
            DateTime? startTime,
            DateTime? endTime,
            string? sortBy,
            int page,
            int pageSize)
        {
            var rawStations = await _stationRepo.GetPublicStationsAsync(keyword, minRating);

            // ─── LỌC THEO THỜI GIAN (Availability Filtering) ───
            var now = DateTimeHelper.VietnamNow();
            DateTime filterStart = startTime ?? now;
            DateTime filterEnd = endTime ?? (startTime.HasValue ? startTime.Value.AddHours(1) : new DateTime(now.Year, now.Month, now.Day, 23, 59, 59));

            if (filterStart > filterEnd) (filterStart, filterEnd) = (filterEnd, filterStart);

            var stationIds = rawStations.Select(s => s.Id).ToList();
            
            var stationConfigs = await _configService.GetCurrentConfigsAsync();

            var overlappingBookings = await _bookingRepo.GetOverlappingActiveBookingsForStationsAsync(
                stationIds, filterStart, filterEnd, stationConfigs.Slot_Buffer_Minutes);

            var availableStations = new List<(ChargingStation station, int availableSlots)>();

            foreach (var station in rawStations)
            {
                var isUnavailable = false;
                for (var d = filterStart.Date; d <= filterEnd.Date; d = d.AddDays(1))
                {
                    if (station.UnavailableDates != null && station.UnavailableDates.Any(ud => ud.Date == DateOnly.FromDateTime(d)))
                    {
                        isUnavailable = true;
                        break;
                    }
                }
                
                if (isUnavailable) continue;

                var stationSlots = station.ChargingSlots.Where(s => s.Status == SlotStatus.Active).ToList();
                int availableCount = 0;

                foreach (var slot in stationSlots)
                {
                    bool isSlotBooked = overlappingBookings.Any(b => b.SlotId == slot.Id);
                    if (!isSlotBooked) availableCount++;
                }

                if (availableCount > 0)
                {
                    availableStations.Add((station, availableCount));
                }
            }

            // ─── LỌC VÀ TÍNH KHOẢNG CÁCH KÉP TỌA ĐỘ ───
            List<(ChargingStation station, double? distanceKm, int availableSlots)> stationsWithCalculations;
            if (lat.HasValue && lng.HasValue)
            {
                stationsWithCalculations = availableStations
                    .Where(x => x.station.Latitude.HasValue && x.station.Longitude.HasValue)
                    .Select(x => (x.station, distanceKm: (double?)HaversineKm(lat.Value, lng.Value, (double)x.station.Latitude!.Value, (double)x.station.Longitude!.Value), x.availableSlots))
                    .Where(x => x.distanceKm <= radiusKm)
                    .ToList();

                var noCoordStations = availableStations
                    .Where(x => !x.station.Latitude.HasValue || !x.station.Longitude.HasValue)
                    .Select(x => (x.station, distanceKm: (double?)null, x.availableSlots));
                stationsWithCalculations.AddRange(noCoordStations);
            }
            else
            {
                stationsWithCalculations = availableStations.Select(x => (x.station, distanceKm: (double?)null, x.availableSlots)).ToList();
            }

            // Sort
            stationsWithCalculations = sortBy?.ToLower() switch
            {
                "distance" when lat.HasValue && lng.HasValue =>
                    stationsWithCalculations.OrderBy(x => x.distanceKm ?? double.MaxValue).ToList(),
                "rating" =>
                    stationsWithCalculations.OrderByDescending(x => x.station.AverageRating)
                        .ThenByDescending(x => x.station.TotalReviews).ToList(),
                "reviews" =>
                    stationsWithCalculations.OrderByDescending(x => x.station.TotalReviews)
                        .ThenByDescending(x => x.station.AverageRating).ToList(),
                _ when lat.HasValue && lng.HasValue =>
                    stationsWithCalculations.OrderBy(x => x.distanceKm ?? double.MaxValue).ToList(),
                _ =>
                    stationsWithCalculations.OrderBy(x => x.station.Name).ToList()
            };

            var total = stationsWithCalculations.Count;
            var items = stationsWithCalculations
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x =>
                {
                    var dto = MapToPublicDto(x.station);
                    dto.DistanceKm = x.distanceKm.HasValue ? Math.Round(x.distanceKm.Value, 2) : null;
                    dto.AvailableSlotsCount = x.availableSlots;
                    return dto;
                })
                .ToList();

            return new { total, page, pageSize, items };
        }

        public async Task<object> GetNearbyAsync(double lat, double lng, double radiusKm, int top)
        {
            var stations = await _stationRepo.GetPublicStationsWithCoordinatesAsync();

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

            return nearby;
        }

        public async Task<object?> GetByIdAsync(int id)
        {
            var station = await _stationRepo.GetByIdAsync(id, includeDetails: true);

            if (station == null || 
                station.ApprovalStatus != ApprovalStatus.Approved || 
                station.OperationalStatus != OperationalStatus.Active)
            {
                return null;
            }

            return MapToPublicDto(station);
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

        private static double HaversineKm(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371; 
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
