using System.Security.Claims;
using ChargeSlot.Api.Constants;
using ChargeSlot.Api.DTOs.Station;
using ChargeSlot.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChargeSlot.Api.Controllers
{
    [ApiController]
    [Route("api/stations")]
    [Authorize(Roles = RoleConstants.Owner)]
    public class ChargingStationController : ControllerBase
    {
        private readonly IChargingStationService _stationService;

        public ChargingStationController(IChargingStationService stationService)
        {
            _stationService = stationService;
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
    }
}
