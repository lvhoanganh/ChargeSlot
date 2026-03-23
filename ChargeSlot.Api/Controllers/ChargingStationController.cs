using System.Security.Claims;
using ChargeSlot.Api.Constants;
using ChargeSlot.Api.DTOs.Station;
using ChargeSlot.Api.Models;
using ChargeSlot.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ChargeSlot.Api.Controllers
{
    [ApiController]
    [Route("api/stations")]
    [Authorize(Roles = RoleConstants.Owner)]
    public class ChargingStationController : ControllerBase
    {
        private readonly IChargingStationService _stationService;
        private readonly Data.ChargeSlotDbContext _db;

        public ChargingStationController(IChargingStationService stationService, Data.ChargeSlotDbContext db)
        {
            _stationService = stationService;
            _db = db;
        }

        private int GetUserId()
        {
            var id = User.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? throw new InvalidOperationException("UserId missing in token");
            return int.Parse(id);
        }

        /// <summary>List all stations owned by the current Owner.</summary>
        [HttpGet]
        public async Task<ActionResult<List<ChargingStationDto>>> GetMyStations()
        {
            var userId = GetUserId();
            var stations = await _stationService.GetAllByOwnerAsync(userId);
            return Ok(stations);
        }

        /// <summary>Get a single station by ID (must be owned by current Owner).</summary>
        [HttpGet("{id:int}")]
        public async Task<ActionResult<ChargingStationDto>> GetById(int id)
        {
            var userId = GetUserId();
            var station = await _stationService.GetByIdAsync(id, userId);
            if (station == null) return NotFound();
            return Ok(station);
        }

        /// <summary>Create a new station (initially in Draft status). Accepts multipart/form-data with image uploads.</summary>
        [HttpPost]
        [Consumes("multipart/form-data")]
        public async Task<ActionResult<ChargingStationDto>> Create([FromForm] CreateStationFormDto dto)
        {
            var userId = GetUserId();
            try
            {
                var created = await _stationService.CreateFromFormAsync(userId, dto, HttpContext.Request);
                return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>Update station info (only when Draft or Rejected).</summary>
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateChargingStationDto dto)
        {
            var userId = GetUserId();
            try
            {
                await _stationService.UpdateAsync(id, userId, dto);
                return NoContent();
            }
            catch (KeyNotFoundException) { return NotFound(); }
            catch (UnauthorizedAccessException) { return Forbid(); }
            catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
        }

        /// <summary>Delete station (only when Draft or Rejected).</summary>
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var userId = GetUserId();
            try
            {
                await _stationService.DeleteAsync(id, userId);
                return NoContent();
            }
            catch (KeyNotFoundException) { return NotFound(); }
            catch (UnauthorizedAccessException) { return Forbid(); }
            catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
        }

        /// <summary>Submit station for admin approval (Draft/Rejected → PendingApproval).</summary>
        [HttpPost("{id:int}/submit")]
        public async Task<IActionResult> SubmitForApproval(int id)
        {
            var userId = GetUserId();
            try
            {
                await _stationService.SubmitForApprovalAsync(id, userId);
                return Ok(new { message = "Station submitted for approval." });
            }
            catch (KeyNotFoundException) { return NotFound(); }
            catch (UnauthorizedAccessException) { return Forbid(); }
            catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
        }

        /// <summary>
        /// Owner bật/tắt station (Active ↔ Inactive). Chỉ cho station đã Approved.
        /// PATCH /api/stations/{id}/status { "operationalStatus": "Inactive" }
        /// </summary>
        [HttpPatch("{id:int}/status")]
        public async Task<IActionResult> UpdateOperationalStatus(int id, [FromBody] UpdateStationStatusDto dto)
        {
            var userId = GetUserId();
            var station = await _db.ChargingStations.FindAsync(id);
            if (station == null) return NotFound();
            if (station.OwnerUserId != userId) return Forbid();

            if (station.ApprovalStatus != Enums.ApprovalStatus.Approved)
                return BadRequest(new { error = "Chỉ có thể thay đổi trạng thái hoạt động khi station đã được Approved." });

            if (!Enum.TryParse<Enums.OperationalStatus>(dto.OperationalStatus, true, out var newStatus))
                return BadRequest(new { error = "OperationalStatus không hợp lệ. Sử dụng: Active, Inactive." });

            station.OperationalStatus = newStatus;
            station.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            return Ok(new { message = $"Station đã chuyển sang {newStatus}.", operationalStatus = newStatus.ToString() });
        }

        // ─────────────── STATION PRICING (giá theo khung giờ) ───────────────

        /// <summary>List all pricing rules for a station.</summary>
        [HttpGet("{stationId:int}/pricing")]
        public async Task<IActionResult> GetPricing(int stationId)
        {
            var userId = GetUserId();
            var station = await _db.ChargingStations.FindAsync(stationId);
            if (station == null) return NotFound();
            if (station.OwnerUserId != userId) return Forbid();

            var pricings = await _db.StationPricings
                .Where(p => p.StationId == stationId)
                .OrderBy(p => p.StartTime)
                .ToListAsync();

            return Ok(pricings.Select(MapPricingDto));
        }

        /// <summary>Add a pricing rule. Ví dụ: 0h-8h = 10,000đ, 8h-24h = 18,000đ.</summary>
        [HttpPost("{stationId:int}/pricing")]
        public async Task<IActionResult> CreatePricing(int stationId, [FromBody] CreateStationPricingDto dto)
        {
            var userId = GetUserId();
            var station = await _db.ChargingStations.FindAsync(stationId);
            if (station == null) return NotFound();
            if (station.OwnerUserId != userId) return Forbid();

            if (!TimeOnly.TryParse(dto.StartTime, out var startTime) || !TimeOnly.TryParse(dto.EndTime, out var endTime))
                return BadRequest(new { message = "StartTime/EndTime phải ở dạng HH:mm, ví dụ 08:00" });

            var pricing = new StationPricing
            {
                StationId = stationId,
                DayOfWeek = dto.DayOfWeek,
                StartTime = startTime,
                EndTime = endTime,
                PricePerHour = dto.PricePerHour,
                Priority = dto.Priority,
                EffectiveFrom = dto.EffectiveFrom ?? DateTime.UtcNow,
                EffectiveTo = dto.EffectiveTo,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _db.StationPricings.Add(pricing);
            await _db.SaveChangesAsync();

            return CreatedAtAction(nameof(GetPricing), new { stationId }, MapPricingDto(pricing));
        }

        /// <summary>Update a pricing rule.</summary>
        [HttpPut("{stationId:int}/pricing/{pricingId:int}")]
        public async Task<IActionResult> UpdatePricing(int stationId, int pricingId, [FromBody] UpdateStationPricingDto dto)
        {
            var userId = GetUserId();
            var station = await _db.ChargingStations.FindAsync(stationId);
            if (station == null) return NotFound();
            if (station.OwnerUserId != userId) return Forbid();

            var pricing = await _db.StationPricings
                .FirstOrDefaultAsync(p => p.Id == pricingId && p.StationId == stationId);
            if (pricing == null) return NotFound(new { message = "Pricing rule không tồn tại." });

            if (!TimeOnly.TryParse(dto.StartTime, out var startTime) || !TimeOnly.TryParse(dto.EndTime, out var endTime))
                return BadRequest(new { message = "StartTime/EndTime phải ở dạng HH:mm" });

            pricing.DayOfWeek = dto.DayOfWeek;
            pricing.StartTime = startTime;
            pricing.EndTime = endTime;
            pricing.PricePerHour = dto.PricePerHour;
            pricing.Priority = dto.Priority;
            pricing.EffectiveFrom = dto.EffectiveFrom ?? pricing.EffectiveFrom;
            pricing.EffectiveTo = dto.EffectiveTo;
            pricing.IsActive = dto.IsActive;

            await _db.SaveChangesAsync();
            return Ok(MapPricingDto(pricing));
        }

        /// <summary>Delete a pricing rule.</summary>
        [HttpDelete("{stationId:int}/pricing/{pricingId:int}")]
        public async Task<IActionResult> DeletePricing(int stationId, int pricingId)
        {
            var userId = GetUserId();
            var station = await _db.ChargingStations.FindAsync(stationId);
            if (station == null) return NotFound();
            if (station.OwnerUserId != userId) return Forbid();

            var pricing = await _db.StationPricings
                .FirstOrDefaultAsync(p => p.Id == pricingId && p.StationId == stationId);
            if (pricing == null) return NotFound();

            _db.StationPricings.Remove(pricing);
            await _db.SaveChangesAsync();
            return NoContent();
        }

        private static StationPricingDto MapPricingDto(StationPricing p)
        {
            return new StationPricingDto
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
            };
        }
    }
}

