using ChargeSlot.Api.DTOs.Slot;
using ChargeSlot.Api.DTOs.Station;
using ChargeSlot.Api.Enums;
using ChargeSlot.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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

        /// <summary>List tất cả trạm Approved + Active (cho Driver tìm kiếm).</summary>
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var stations = await _db.ChargingStations
                .Include(s => s.Images)
                .Include(s => s.OperatingHours)
                .Include(s => s.ChargingSlots)
                    .ThenInclude(slot => slot.SlotPricings)
                .Include(s => s.ExtraServices)
                .Where(s => s.ApprovalStatus == ApprovalStatus.Approved
                    && s.OperationalStatus == OperationalStatus.Active)
                .OrderBy(s => s.Name)
                .ToListAsync();

            return Ok(stations.Select(MapToPublicDto));
        }

        /// <summary>Chi tiết 1 trạm (chỉ trả về nếu Approved + Active).</summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var station = await _db.ChargingStations
                .Include(s => s.Images)
                .Include(s => s.OperatingHours)
                .Include(s => s.ChargingSlots)
                    .ThenInclude(slot => slot.SlotPricings)
                .Include(s => s.ExtraServices)
                .FirstOrDefaultAsync(s => s.Id == id
                    && s.ApprovalStatus == ApprovalStatus.Approved
                    && s.OperationalStatus == OperationalStatus.Active);

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
                    UpdatedAt = s.UpdatedAt,
                    // Note: không expose QrCodeToken cho public API
                    PricingTiers = s.SlotPricings?.Where(p => p.IsActive).Select(p => new SlotPricingDto
                    {
                        Id = p.Id,
                        SlotId = p.SlotId,
                        DayOfWeek = p.DayOfWeek,
                        StartTime = p.StartTime,
                        EndTime = p.EndTime,
                        PricePerHour = p.PricePerHour,
                        Priority = p.Priority,
                        EffectiveFrom = p.EffectiveFrom,
                        EffectiveTo = p.EffectiveTo,
                        IsActive = p.IsActive,
                        CreatedAt = p.CreatedAt
                    }).OrderBy(p => p.StartTime).ToList()
                }).ToList()
            };
        }
    }
}
