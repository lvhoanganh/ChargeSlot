using System.Security.Claims;
using ChargeSlot.Api.Constants;
using ChargeSlot.Api.DTOs.Station;
using ChargeSlot.Api.DTOs.Admin;
using ChargeSlot.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChargeSlot.Api.Controllers
{
    [ApiController]
    [Route("api/admin/stations")]
    [Authorize(Roles = RoleConstants.Admin)]
    public class AdminStationController : ControllerBase
    {
        private readonly IChargingStationService _stationService;

        public AdminStationController(IChargingStationService stationService)
        {
            _stationService = stationService;
        }

        private int GetUserId()
        {
            var id = User.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? throw new InvalidOperationException("UserId missing in token");
            return int.Parse(id);
        }

        /// <summary>List all stations for Admin Management with pagination and filters.</summary>
        [HttpGet]
        public async Task<ActionResult<PagedResultDto<ChargingStationDto>>> GetStations(
            [FromQuery] string? status, 
            [FromQuery] string? search,
            [FromQuery] string? ownerName,
            [FromQuery] int page = 1, 
            [FromQuery] int pageSize = 10)
        {
            var result = await _stationService.GetAdminStationsAsync(status, search, ownerName, page, pageSize);
            return Ok(result);
        }

        /// <summary>List all stations pending approval.</summary>
        [HttpGet("pending")]
        public async Task<ActionResult<List<ChargingStationDto>>> GetPendingStations()
        {
            var stations = await _stationService.GetPendingStationsAsync();
            return Ok(stations);
        }

        /// <summary>Get station detail for review.</summary>
        [HttpGet("{id:int}")]
        public async Task<ActionResult<ChargingStationDto>> GetById(int id)
        {
            var station = await _stationService.GetStationDetailForAdminAsync(id);
            if (station == null) return NotFound();
            return Ok(station);
        }

        /// <summary>Approve or reject a pending station.</summary>
        [HttpPost("{id:int}/review")]
        public async Task<IActionResult> ReviewStation(int id, [FromBody] ReviewStationDto dto)
        {
            var adminUserId = GetUserId();
            try
            {
                await _stationService.ReviewStationAsync(id, adminUserId, dto);
                var action = dto.IsApproved ? "approved" : "rejected";
                return Ok(new { message = $"Station {action} successfully." });
            }
            catch (KeyNotFoundException) { return NotFound(); }
            catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        }

        /// <summary>Toggle ban status of a station (Manual Ban/Unban).</summary>
        [HttpPost("{id:int}/toggle-ban")]
        public async Task<IActionResult> ToggleBan(int id, [FromBody] ToggleBanDto dto)
        {
            var adminUserId = GetUserId();
            try
            {
                var newStatus = await _stationService.ToggleBanStationAsync(id, adminUserId, dto.Reason);
                return Ok(new { stationId = id, status = newStatus });
            }
            catch (KeyNotFoundException) { return NotFound(); }
            catch (Exception ex) { return StatusCode(500, new { message = "Lỗi hệ thống.", details = ex.Message }); }
        }
    }
}
