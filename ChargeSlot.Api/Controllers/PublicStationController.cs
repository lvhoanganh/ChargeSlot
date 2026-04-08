using ChargeSlot.Api.Helpers;
using ChargeSlot.Api.DTOs.Slot;
using ChargeSlot.Api.DTOs.Station;
using ChargeSlot.Api.Enums;
using ChargeSlot.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ChargeSlot.Api.Constants;

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
        private readonly ChargeSlot.Api.Services.Interfaces.IPublicStationService _publicStationService;

        public PublicStationController(ChargeSlot.Api.Services.Interfaces.IPublicStationService publicStationService)
        {
            _publicStationService = publicStationService;
        }

        /// <summary>
        /// List trạm Approved + Active cho Driver tìm kiếm.
        /// Filter: keyword, minRating, lat/lng/radiusKm (tìm gần).
        /// Sort: name (default), rating, reviews, distance (cần truyền lat/lng).
        /// Pagination: page, pageSize.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAll(
            [FromQuery] string? keyword = null,
            [FromQuery] decimal? minRating = null,
            [FromQuery] double? lat = null,
            [FromQuery] double? lng = null,
            [FromQuery] double radiusKm = 50,
            [FromQuery] DateTime? startTime = null,
            [FromQuery] DateTime? endTime = null,
            [FromQuery] string? sortBy = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            var result = await _publicStationService.GetAllAsync(
                keyword, minRating, lat, lng, radiusKm, startTime, endTime, sortBy, page, pageSize);

            return Ok(result);
        }

        /// <summary>
        /// Tìm trạm gần nhất (shortcut cho FE map view).
        /// Trả top N trạm gần nhất theo tọa độ.
        /// </summary>
        [HttpGet("nearby")]
        public async Task<IActionResult> GetNearby(
            [FromQuery] double lat,
            [FromQuery] double lng,
            [FromQuery] double radiusKm = 10,
            [FromQuery] int top = 10)
        {
            var nearby = await _publicStationService.GetNearbyAsync(lat, lng, radiusKm, top);
            return Ok(nearby);
        }

        /// <summary>Chi tiết 1 trạm (chỉ trả về nếu Approved + Active).</summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var result = await _publicStationService.GetByIdAsync(id);
            if (result == null) return NotFound(new { message = "Trạm sạc không tồn tại hoặc chưa hoạt động." });

            return Ok(result);
        }
    }
}
