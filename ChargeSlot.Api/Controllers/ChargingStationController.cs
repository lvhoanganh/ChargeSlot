using System.Security.Claims;
using ChargeSlot.Api.Constants;
using ChargeSlot.Api.DTOs.Station;
using ChargeSlot.Api.Models;
using ChargeSlot.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using ChargeSlot.Api.Helpers;
namespace ChargeSlot.Api.Controllers
{
    // TODO: Refactor – move direct DB access (pricing, extra services, status) to ChargingStationService
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
            var userId = GetUserId();
            var station = await _db.ChargingStations.FindAsync(id);
            if (station == null) return NotFound();
            if (station.OwnerUserId != userId) return Forbid();

            if (station.ApprovalStatus != Enums.ApprovalStatus.Approved)
                return BadRequest(new { message = "Chỉ có thể thay đổi trạng thái hoạt động khi station đã được Approved." });

            if (!Enum.TryParse<Enums.OperationalStatus>(dto.OperationalStatus, true, out var newStatus))
                return BadRequest(new { message = "OperationalStatus không hợp lệ. Sử dụng: Active, Inactive." });

            if (newStatus == Enums.OperationalStatus.Inactive)
            {
                var activeStatuses = new[]
                {
                    Enums.BookingStatus.WaitingOwner, Enums.BookingStatus.PendingPayment,
                    Enums.BookingStatus.Paid, Enums.BookingStatus.CheckedIn, Enums.BookingStatus.InProgress
                };
                var now = DateTimeHelper.VietnamNow();
                
                var hasActiveBookings = await _db.Bookings
                    .AnyAsync(b => b.ChargingSlot != null 
                                && b.ChargingSlot.StationId == id 
                                && b.EndTime > now
                                && activeStatuses.Contains(b.Status));
                                
                if (hasActiveBookings)
                    return BadRequest(new { message = "Không thể tắt trạm (Inactive) vì đang có booking sắp tới hoặc đang sạc. Vui lòng hủy các booking này trước." });
            }

            station.OperationalStatus = newStatus;
            station.UpdatedAt = DateTimeHelper.VietnamNow();
            await _db.SaveChangesAsync();

            return Ok(new { message = $"Station đã chuyển sang {newStatus}.", operationalStatus = newStatus.ToString() });
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
                EffectiveFrom = dto.EffectiveFrom ?? DateTimeHelper.VietnamNow(),
                EffectiveTo = dto.EffectiveTo,
                IsActive = true,
                CreatedAt = DateTimeHelper.VietnamNow()
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

        // ─────────────── EXTRA SERVICES (dịch vụ bổ sung) ───────────────

        /// <summary>List all extra services for a station.</summary>
        [HttpGet("{stationId:int}/extra-services")]
        public async Task<IActionResult> GetExtraServices(int stationId)
        {
            var userId = GetUserId();
            var station = await _db.ChargingStations.FindAsync(stationId);
            if (station == null) return NotFound();
            if (station.OwnerUserId != userId) return Forbid();

            var services = await _db.Set<ExtraService>()
                .Where(s => s.StationId == stationId)
                .OrderBy(s => s.ServiceName)
                .ToListAsync();

            return Ok(services.Select(MapExtraServiceDto));
        }

        /// <summary>Create a new extra service for a station.</summary>
        [HttpPost("{stationId:int}/extra-services")]
        public async Task<IActionResult> CreateExtraService(int stationId, [FromBody] CreateExtraServiceDto dto)
        {
            var userId = GetUserId();
            var station = await _db.ChargingStations.FindAsync(stationId);
            if (station == null) return NotFound();
            if (station.OwnerUserId != userId) return Forbid();

            if (string.IsNullOrWhiteSpace(dto.ServiceName))
                return BadRequest(new { message = "Tên dịch vụ không được để trống." });

            if (dto.Price < 0)
                return BadRequest(new { message = "Giá dịch vụ không được âm." });

            var service = new ExtraService
            {
                StationId = stationId,
                ServiceName = dto.ServiceName.Trim(),
                Description = dto.Description?.Trim(),
                Price = dto.Price,
                TotalStock = dto.TotalStock,
                IsActive = true,
                CreatedAt = DateTimeHelper.VietnamNow()
            };

            _db.Set<ExtraService>().Add(service);
            await _db.SaveChangesAsync();

            return CreatedAtAction(nameof(GetExtraServices), new { stationId }, MapExtraServiceDto(service));
        }

        /// <summary>Update an extra service.</summary>
        [HttpPut("{stationId:int}/extra-services/{serviceId:int}")]
        public async Task<IActionResult> UpdateExtraService(int stationId, int serviceId, [FromBody] UpdateExtraServiceDto dto)
        {
            var userId = GetUserId();
            var station = await _db.ChargingStations.FindAsync(stationId);
            if (station == null) return NotFound();
            if (station.OwnerUserId != userId) return Forbid();

            var service = await _db.Set<ExtraService>()
                .FirstOrDefaultAsync(s => s.Id == serviceId && s.StationId == stationId);
            if (service == null) return NotFound(new { message = "Dịch vụ không tồn tại." });

            if (string.IsNullOrWhiteSpace(dto.ServiceName))
                return BadRequest(new { message = "Tên dịch vụ không được để trống." });

            if (dto.Price < 0)
                return BadRequest(new { message = "Giá dịch vụ không được âm." });

            service.ServiceName = dto.ServiceName.Trim();
            service.Description = dto.Description?.Trim();
            service.Price = dto.Price;
            service.TotalStock = dto.TotalStock;
            service.IsActive = dto.IsActive;

            await _db.SaveChangesAsync();
            return Ok(MapExtraServiceDto(service));
        }

        /// <summary>Delete an extra service (hard delete).</summary>
        [HttpDelete("{stationId:int}/extra-services/{serviceId:int}")]
        public async Task<IActionResult> DeleteExtraService(int stationId, int serviceId)
        {
            var userId = GetUserId();
            var station = await _db.ChargingStations.FindAsync(stationId);
            if (station == null) return NotFound();
            if (station.OwnerUserId != userId) return Forbid();

            var service = await _db.Set<ExtraService>()
                .FirstOrDefaultAsync(s => s.Id == serviceId && s.StationId == stationId);
            if (service == null) return NotFound();

            // Kiểm tra có booking nào đang dùng dịch vụ này không
            var hasBookings = await _db.Set<BookingExtraService>()
                .AnyAsync(bes => bes.ServiceId == serviceId);
            if (hasBookings)
                return BadRequest(new { message = "Không thể xóa dịch vụ đã có booking sử dụng. Hãy tắt (IsActive = false) thay vì xóa." });

            _db.Set<ExtraService>().Remove(service);
            await _db.SaveChangesAsync();
            return NoContent();
        }

        private static ExtraServiceDto MapExtraServiceDto(ExtraService s)
        {
            return new ExtraServiceDto
            {
                Id = s.Id,
                ServiceName = s.ServiceName,
                Description = s.Description,
                Price = s.Price,
                TotalStock = s.TotalStock,
                IsActive = s.IsActive
            };
        }
    }
}

