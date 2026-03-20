using System.Security.Claims;
using ChargeSlot.Api.Constants;
using ChargeSlot.Api.DTOs.Slot;
using ChargeSlot.Api.Models;
using ChargeSlot.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ChargeSlot.Api.Controllers
{
    [ApiController]
    [Route("api/stations/{stationId:int}/slots")]
    [Authorize(Roles = RoleConstants.Owner)]
    public class ChargingSlotController : ControllerBase
    {
        private readonly IChargingSlotService _slotService;
        private readonly Data.ChargeSlotDbContext _db;

        public ChargingSlotController(IChargingSlotService slotService, Data.ChargeSlotDbContext db)
        {
            _slotService = slotService;
            _db = db;
        }

        private int GetUserId()
        {
            var id = User.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? throw new InvalidOperationException("UserId missing in token");
            return int.Parse(id);
        }

        // ─────────────── SLOT CRUD ───────────────

        /// <summary>List all slots for a station.</summary>
        [HttpGet]
        public async Task<ActionResult<List<ChargingSlotDto>>> GetAll(int stationId)
        {
            var userId = GetUserId();
            try
            {
                var slots = await _slotService.GetAllByStationAsync(stationId, userId);
                return Ok(slots);
            }
            catch (KeyNotFoundException) { return NotFound(); }
            catch (UnauthorizedAccessException) { return Forbid(); }
        }

        /// <summary>Get a single slot by ID.</summary>
        [HttpGet("{id:int}")]
        public async Task<ActionResult<ChargingSlotDto>> GetById(int stationId, int id)
        {
            var userId = GetUserId();
            try
            {
                var slot = await _slotService.GetByIdAsync(stationId, id, userId);
                if (slot == null) return NotFound();
                return Ok(slot);
            }
            catch (KeyNotFoundException) { return NotFound(); }
            catch (UnauthorizedAccessException) { return Forbid(); }
        }

        /// <summary>Create a new slot for a station.</summary>
        [HttpPost]
        public async Task<ActionResult<ChargingSlotDto>> Create(int stationId, [FromBody] CreateChargingSlotDto dto)
        {
            var userId = GetUserId();
            try
            {
                var created = await _slotService.CreateAsync(stationId, userId, dto);
                return CreatedAtAction(nameof(GetById), new { stationId, id = created.Id }, created);
            }
            catch (KeyNotFoundException) { return NotFound(); }
            catch (UnauthorizedAccessException) { return Forbid(); }
            catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
        }

        /// <summary>Update a slot.</summary>
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int stationId, int id, [FromBody] UpdateChargingSlotDto dto)
        {
            var userId = GetUserId();
            try
            {
                await _slotService.UpdateAsync(stationId, id, userId, dto);
                return NoContent();
            }
            catch (KeyNotFoundException) { return NotFound(); }
            catch (UnauthorizedAccessException) { return Forbid(); }
            catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
        }

        /// <summary>Change slot status (Active, Inactive, Maintenance).</summary>
        [HttpPatch("{id:int}/status")]
        public async Task<IActionResult> UpdateStatus(int stationId, int id, [FromBody] UpdateSlotStatusDto dto)
        {
            var userId = GetUserId();
            try
            {
                await _slotService.UpdateStatusAsync(stationId, id, userId, dto);
                return NoContent();
            }
            catch (KeyNotFoundException) { return NotFound(); }
            catch (UnauthorizedAccessException) { return Forbid(); }
            catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
        }

        /// <summary>Delete a slot.</summary>
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int stationId, int id)
        {
            var userId = GetUserId();
            try
            {
                await _slotService.DeleteAsync(stationId, id, userId);
                return NoContent();
            }
            catch (KeyNotFoundException) { return NotFound(); }
            catch (UnauthorizedAccessException) { return Forbid(); }
            catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
        }

        // ─────────────── SLOT PRICING (giá theo khung giờ) ───────────────

        /// <summary>List all pricing rules for a slot.</summary>
        [HttpGet("{slotId:int}/pricing")]
        public async Task<IActionResult> GetPricing(int stationId, int slotId)
        {
            var userId = GetUserId();
            var station = await _db.ChargingStations.FindAsync(stationId);
            if (station == null) return NotFound();
            if (station.OwnerUserId != userId) return Forbid();

            var pricings = await _db.Set<SlotPricing>()
                .Where(p => p.SlotId == slotId && p.ChargingSlot.StationId == stationId)
                .OrderBy(p => p.StartTime)
                .ToListAsync();

            return Ok(pricings.Select(MapPricingDto));
        }

        /// <summary>Add a pricing rule. Ví dụ: 0h-8h = 10,000đ, 8h-24h = 18,000đ.</summary>
        [HttpPost("{slotId:int}/pricing")]
        public async Task<IActionResult> CreatePricing(int stationId, int slotId, [FromBody] CreateSlotPricingDto dto)
        {
            var userId = GetUserId();
            var station = await _db.ChargingStations.FindAsync(stationId);
            if (station == null) return NotFound();
            if (station.OwnerUserId != userId) return Forbid();

            var slot = await _db.ChargingSlots.FirstOrDefaultAsync(s => s.Id == slotId && s.StationId == stationId);
            if (slot == null) return NotFound(new { message = "Slot không tồn tại." });

            if (!TimeOnly.TryParse(dto.StartTime, out var startTime) || !TimeOnly.TryParse(dto.EndTime, out var endTime))
                return BadRequest(new { message = "StartTime/EndTime phải ở dạng HH:mm, ví dụ 08:00" });

            var pricing = new SlotPricing
            {
                SlotId = slotId,
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

            _db.Set<SlotPricing>().Add(pricing);
            await _db.SaveChangesAsync();

            return CreatedAtAction(nameof(GetPricing), new { stationId, slotId }, MapPricingDto(pricing));
        }

        /// <summary>Update a pricing rule.</summary>
        [HttpPut("{slotId:int}/pricing/{pricingId:int}")]
        public async Task<IActionResult> UpdatePricing(int stationId, int slotId, int pricingId, [FromBody] UpdateSlotPricingDto dto)
        {
            var userId = GetUserId();
            var station = await _db.ChargingStations.FindAsync(stationId);
            if (station == null) return NotFound();
            if (station.OwnerUserId != userId) return Forbid();

            var pricing = await _db.Set<SlotPricing>()
                .FirstOrDefaultAsync(p => p.Id == pricingId && p.SlotId == slotId && p.ChargingSlot.StationId == stationId);
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
        [HttpDelete("{slotId:int}/pricing/{pricingId:int}")]
        public async Task<IActionResult> DeletePricing(int stationId, int slotId, int pricingId)
        {
            var userId = GetUserId();
            var station = await _db.ChargingStations.FindAsync(stationId);
            if (station == null) return NotFound();
            if (station.OwnerUserId != userId) return Forbid();

            var pricing = await _db.Set<SlotPricing>()
                .FirstOrDefaultAsync(p => p.Id == pricingId && p.SlotId == slotId && p.ChargingSlot.StationId == stationId);
            if (pricing == null) return NotFound();

            _db.Set<SlotPricing>().Remove(pricing);
            await _db.SaveChangesAsync();
            return NoContent();
        }

        private static SlotPricingDto MapPricingDto(SlotPricing p)
        {
            return new SlotPricingDto
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
            };
        }
    }
}
