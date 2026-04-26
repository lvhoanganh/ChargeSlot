using ChargeSlot.Api.Data;
using ChargeSlot.Api.DTOs.Station;
using ChargeSlot.Api.Enums;
using ChargeSlot.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using ChargeSlot.Api.Services.Interfaces;

using ChargeSlot.Api.Helpers;
namespace ChargeSlot.Api.Controllers
{
    /// <summary>
    /// Driver quản lý trạm yêu thích (kiểu Be).
    /// </summary>
    // TODO: Refactor – move business logic to a dedicated FavoriteService
    [ApiController]
    [Route("api/favorites")]
    [Authorize(Roles = "Driver")]
    public class FavoriteController : ControllerBase
    {
        private readonly IFavoriteService _favoriteService;

        public FavoriteController(IFavoriteService favoriteService)
        {
            _favoriteService = favoriteService;
        }

        private int GetUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        /// <summary>Thêm trạm yêu thích.</summary>
        [HttpPost("{stationId}")]
        public async Task<IActionResult> AddFavorite(int stationId)
        {
            var userId = GetUserId();

            try
            {
                await _favoriteService.AddFavoriteAsync(userId, stationId);
                return Ok(new { message = "Đã thêm vào yêu thích." });
            }
            catch (InvalidOperationException ex)
            {
                if (ex.Message == "Trạm sạc không tồn tại.")
                    return NotFound(new { message = ex.Message });
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>Xóa trạm khỏi yêu thích.</summary>
        [HttpDelete("{stationId}")]
        public async Task<IActionResult> RemoveFavorite(int stationId)
        {
            var userId = GetUserId();

            try
            {
                await _favoriteService.RemoveFavoriteAsync(userId, stationId);
                return Ok(new { message = "Đã xóa khỏi yêu thích." });
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }

        /// <summary>Danh sách trạm yêu thích của Driver.</summary>
        [HttpGet]
        public async Task<IActionResult> GetMyFavorites()
        {
            var userId = GetUserId();

            var favorites = await _favoriteService.GetMyFavoritesAsync(userId);
            return Ok(favorites);
        }

        /// <summary>
        /// Top trạm được yêu thích nhiều nhất (public, kiểu Be top quán).
        /// </summary>
        [HttpGet("top")]
        [AllowAnonymous]
        public async Task<IActionResult> GetTopFavorites([FromQuery] int limit = 10)
        {
            var result = await _favoriteService.GetTopFavoritesAsync(limit);
            return Ok(result);
        }

        /// <summary>Check xem Driver đã yêu thích trạm này chưa.</summary>
        [HttpGet("{stationId}/check")]
        public async Task<IActionResult> CheckFavorite(int stationId)
        {
            var userId = GetUserId();
            var isFavorite = await _favoriteService.CheckFavoriteAsync(userId, stationId);
            return Ok(new { isFavorite });
        }
    }
}
