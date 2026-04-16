using System.Security.Claims;
using ChargeSlot.Api.Constants;
using ChargeSlot.Api.DTOs.Station;
using ChargeSlot.Api.Models;
using ChargeSlot.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using ChargeSlot.Api.Helpers;
using ChargeSlot.Api.DTOs;
namespace ChargeSlot.Api.Controllers
{
    // TODO: Refactor – move direct DB access (pricing, extra services, status) to ChargingStationService
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
        public async Task<ActionResult<PagedResultDto<ChargingStationDto>>> GetMyStations(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            var userId = GetUserId();
            var result = await _stationService.GetAllByOwnerPagedAsync(userId, page, pageSize);
            return Ok(result);
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
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>Update station info (multipart/form-data, cho phép sửa mọi lúc).</summary>
        [HttpPut("{id:int}")]
        [Consumes("multipart/form-data")]
        public async Task<ActionResult<ChargingStationDto>> Update(int id, [FromForm] UpdateStationFormDto dto)
        {
            var userId = GetUserId();
            try
            {
                var updated = await _stationService.UpdateFromFormAsync(id, userId, dto, HttpContext.Request);
                return Ok(updated);
            }
            catch (KeyNotFoundException) { return NotFound(); }
            catch (UnauthorizedAccessException) { return Forbid(); }
            catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
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
            catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
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
            catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        }

        /// <summary>
        /// Owner bật/tắt station (Active ↔ Inactive). Chỉ cho station đã Approved.
        /// PATCH /api/stations/{id}/status { "operationalStatus": "Inactive" }
        /// </summary>
        [HttpPatch("{id:int}/status")]
        public async Task<IActionResult> UpdateOperationalStatus(int id, [FromBody] UpdateStationStatusDto dto)
        {
            try
            {
                var result = await _stationService.UpdateOperationalStatusAsync(id, GetUserId(), dto.OperationalStatus);
                return Ok(new { message = $"Station đã chuyển sang {dto.OperationalStatus}.", operationalStatus = dto.OperationalStatus });
            }
            catch (KeyNotFoundException) { return NotFound(); }
            catch (UnauthorizedAccessException) { return Forbid(); }
            catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        }

        // ─────────────── UNAVAILABLE DATES ───────────────

        /// <summary>Lấy danh sách các ngày trạm báo bận (không hoạt động).</summary>
        [HttpGet("{id:int}/unavailable-dates")]
        [AllowAnonymous]
        public async Task<IActionResult> GetUnavailableDates(int id)
        {
            var dates = await _stationService.GetUnavailableDatesAsync(id);
            return Ok(dates);
        }

        /// <summary>Thêm các ngày báo bận cho trạm.</summary>
        [HttpPost("{id:int}/unavailable-dates")]
        public async Task<IActionResult> AddUnavailableDates(int id, [FromBody] AddUnavailableDatesDto dto)
        {
            try
            {
                var result = await _stationService.AddUnavailableDatesAsync(id, GetUserId(), dto);
                return Ok(result);
            }
            catch (Exceptions.BookingConflictException ex)
            {
                return BadRequest(new { message = ex.Message, conflicts = ex.Conflicts });
            }
            catch (KeyNotFoundException) { return NotFound(); }
            catch (UnauthorizedAccessException) { return Forbid(); }
            catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        }

        /// <summary>Xóa các ngày báo bận đã được cài đặt.</summary>
        [HttpDelete("{id:int}/unavailable-dates")]
        public async Task<IActionResult> RemoveUnavailableDates(int id, [FromBody] RemoveUnavailableDatesDto dto)
        {
            try
            {
                await _stationService.RemoveUnavailableDatesAsync(id, GetUserId(), dto);
                return NoContent();
            }
            catch (KeyNotFoundException) { return NotFound(); }
            catch (UnauthorizedAccessException) { return Forbid(); }
            catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        }

        // ─────────────── STATION PRICING (giá theo khung giờ) ───────────────

        /// <summary>List all pricing rules for a station.</summary>
        [HttpGet("{stationId:int}/pricing")]
        public async Task<IActionResult> GetPricing(int stationId)
        {
            try
            {
                var pricings = await _stationService.GetPricingAsync(stationId, GetUserId());
                return Ok(pricings);
            }
            catch (KeyNotFoundException) { return NotFound(); }
            catch (UnauthorizedAccessException) { return Forbid(); }
        }

        /// <summary>Add a pricing rule. Ví dụ: 0h-8h = 10,000đ, 8h-24h = 18,000đ.</summary>
        [HttpPost("{stationId:int}/pricing")]
        public async Task<IActionResult> CreatePricing(int stationId, [FromBody] CreateStationPricingDto dto)
        {
            try
            {
                var pricing = await _stationService.CreatePricingAsync(stationId, GetUserId(), dto);
                return CreatedAtAction(nameof(GetPricing), new { stationId }, pricing);
            }
            catch (KeyNotFoundException) { return NotFound(); }
            catch (UnauthorizedAccessException) { return Forbid(); }
            catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        }

        /// <summary>Update a pricing rule.</summary>
        [HttpPut("{stationId:int}/pricing/{pricingId:int}")]
        public async Task<IActionResult> UpdatePricing(int stationId, int pricingId, [FromBody] UpdateStationPricingDto dto)
        {
            try
            {
                var pricing = await _stationService.UpdatePricingAsync(stationId, pricingId, GetUserId(), dto);
                return Ok(pricing);
            }
            catch (KeyNotFoundException) { return NotFound(new { message = "Không tìm thấy trạm hoặc rules tương ứng." }); }
            catch (UnauthorizedAccessException) { return Forbid(); }
            catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        }

        /// <summary>Delete a pricing rule.</summary>
        [HttpDelete("{stationId:int}/pricing/{pricingId:int}")]
        public async Task<IActionResult> DeletePricing(int stationId, int pricingId)
        {
            try
            {
                await _stationService.DeletePricingAsync(stationId, pricingId, GetUserId());
                return NoContent();
            }
            catch (KeyNotFoundException) { return NotFound(); }
            catch (UnauthorizedAccessException) { return Forbid(); }
        }

        // ─────────────── EXTRA SERVICES (dịch vụ bổ sung) ───────────────

        /// <summary>List all extra services for a station.</summary>
        [HttpGet("{stationId:int}/extra-services")]
        public async Task<IActionResult> GetExtraServices(int stationId)
        {
            try
            {
                var services = await _stationService.GetExtraServicesAsync(stationId, GetUserId());
                return Ok(services);
            }
            catch (KeyNotFoundException) { return NotFound(); }
            catch (UnauthorizedAccessException) { return Forbid(); }
        }

        /// <summary>Create a new extra service for a station.</summary>
        [HttpPost("{stationId:int}/extra-services")]
        public async Task<IActionResult> CreateExtraService(int stationId, [FromBody] CreateExtraServiceDto dto)
        {
            try
            {
                var service = await _stationService.CreateExtraServiceAsync(stationId, GetUserId(), dto);
                return CreatedAtAction(nameof(GetExtraServices), new { stationId }, service);
            }
            catch (KeyNotFoundException) { return NotFound(); }
            catch (UnauthorizedAccessException) { return Forbid(); }
            catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        }

        /// <summary>Update an extra service.</summary>
        [HttpPut("{stationId:int}/extra-services/{serviceId:int}")]
        public async Task<IActionResult> UpdateExtraService(int stationId, int serviceId, [FromBody] UpdateExtraServiceDto dto)
        {
            try
            {
                var service = await _stationService.UpdateExtraServiceAsync(stationId, serviceId, GetUserId(), dto);
                return Ok(service);
            }
            catch (KeyNotFoundException) { return NotFound(new { message = "Dịch vụ không tồn tại." }); }
            catch (UnauthorizedAccessException) { return Forbid(); }
            catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        }

        /// <summary>Delete an extra service (hard delete).</summary>
        [HttpDelete("{stationId:int}/extra-services/{serviceId:int}")]
        public async Task<IActionResult> DeleteExtraService(int stationId, int serviceId)
        {
            try
            {
                await _stationService.DeleteExtraServiceAsync(stationId, serviceId, GetUserId());
                return NoContent();
            }
            catch (KeyNotFoundException) { return NotFound(); }
            catch (UnauthorizedAccessException) { return Forbid(); }
            catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        }
    }
}

