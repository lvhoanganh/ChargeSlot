using System.Security.Claims;
using ChargeSlot.Api.Constants;
using ChargeSlot.Api.DTOs.Slot;
using ChargeSlot.Api.DTOs.Station;
using ChargeSlot.Api.Models;
using ChargeSlot.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using ChargeSlot.Api.Helpers;
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

        /// <summary>
        /// Xem lịch đặt slot và thời gian trống tiếp theo.
        /// GET /api/stations/{stationId}/slots/{slotId}/availability?date=2026-03-24
        /// </summary>
        [HttpGet("{slotId:int}/availability")]
        [AllowAnonymous]
        public async Task<IActionResult> GetAvailability(int stationId, int slotId, [FromQuery] DateTime? date)
        {
            try
            {
                var targetDate = date ?? DateTimeHelper.VietnamNow().Date;
                var availability = await _slotService.GetSlotAvailabilityAsync(slotId, targetDate);
                return Ok(availability);
            }
            catch (KeyNotFoundException) { return NotFound(); }
        }
    }
}
