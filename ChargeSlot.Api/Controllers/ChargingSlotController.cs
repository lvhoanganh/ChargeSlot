using System.Security.Claims;
using ChargeSlot.Api.Constants;
using ChargeSlot.Api.DTOs.Slot;
using ChargeSlot.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChargeSlot.Api.Controllers
{
    [ApiController]
    [Route("api/stations/{stationId:int}/slots")]
    [Authorize(Roles = RoleConstants.Owner)]
    public class ChargingSlotController : ControllerBase
    {
        private readonly IChargingSlotService _slotService;

        public ChargingSlotController(IChargingSlotService slotService)
        {
            _slotService = slotService;
        }

        private int GetUserId()
        {
            var id = User.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? throw new InvalidOperationException("UserId missing in token");
            return int.Parse(id);
        }

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
    }
}
