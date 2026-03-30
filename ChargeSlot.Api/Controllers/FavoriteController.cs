using ChargeSlot.Api.Data;
using ChargeSlot.Api.DTOs.Station;
using ChargeSlot.Api.Enums;
using ChargeSlot.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

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
        private readonly ChargeSlotDbContext _db;

        public FavoriteController(ChargeSlotDbContext db)
        {
            _db = db;
        }

        private int GetUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        /// <summary>Thêm trạm yêu thích.</summary>
        [HttpPost("{stationId}")]
        public async Task<IActionResult> AddFavorite(int stationId)
        {
            var userId = GetUserId();

            var station = await _db.ChargingStations.FindAsync(stationId);
            if (station == null)
                return NotFound(new { message = "Trạm sạc không tồn tại." });

            var exists = await _db.FavoriteStations
                .AnyAsync(f => f.DriverUserId == userId && f.StationId == stationId);
            if (exists)
                return BadRequest(new { message = "Đã có trong danh sách yêu thích." });

            _db.FavoriteStations.Add(new FavoriteStation
            {
                DriverUserId = userId,
                StationId = stationId,
                CreatedAt = DateTimeHelper.VietnamNow()
            });
            await _db.SaveChangesAsync();

            return Ok(new { message = "Đã thêm vào yêu thích." });
        }

        /// <summary>Xóa trạm khỏi yêu thích.</summary>
        [HttpDelete("{stationId}")]
        public async Task<IActionResult> RemoveFavorite(int stationId)
        {
            var userId = GetUserId();

            var fav = await _db.FavoriteStations
                .FirstOrDefaultAsync(f => f.DriverUserId == userId && f.StationId == stationId);

            if (fav == null)
                return NotFound(new { message = "Trạm chưa có trong yêu thích." });

            _db.FavoriteStations.Remove(fav);
            await _db.SaveChangesAsync();

            return Ok(new { message = "Đã xóa khỏi yêu thích." });
        }

        /// <summary>Danh sách trạm yêu thích của Driver.</summary>
        [HttpGet]
        public async Task<IActionResult> GetMyFavorites()
        {
            var userId = GetUserId();

            var favorites = await _db.FavoriteStations
                .Where(f => f.DriverUserId == userId)
                .OrderByDescending(f => f.CreatedAt)
                .Select(f => new FavoriteStationDto
                {
                    StationId = f.Station.Id,
                    Name = f.Station.Name,
                    Address = f.Station.Address,
                    ImageUrl = f.Station.Images.FirstOrDefault() != null
                        ? f.Station.Images.First().ImageUrl : null,
                    AverageRating = f.Station.AverageRating,
                    TotalReviews = f.Station.TotalReviews,
                    IsFavorite = true,
                    FavoritedAt = f.CreatedAt
                })
                .ToListAsync();

            return Ok(favorites);
        }

        /// <summary>
        /// Top trạm được yêu thích nhiều nhất (public, kiểu Be top quán).
        /// </summary>
        [HttpGet("top")]
        [AllowAnonymous]
        public async Task<IActionResult> GetTopFavorites([FromQuery] int limit = 10)
        {
            var topStations = await _db.FavoriteStations
                .Where(f => f.Station.ApprovalStatus == ApprovalStatus.Approved
                    && f.Station.OperationalStatus == OperationalStatus.Active)
                .GroupBy(f => f.StationId)
                .Select(g => new
                {
                    StationId = g.Key,
                    FavoriteCount = g.Count()
                })
                .OrderByDescending(x => x.FavoriteCount)
                .Take(limit)
                .ToListAsync();

            var stationIds = topStations.Select(t => t.StationId).ToList();

            var stations = await _db.ChargingStations
                .Include(s => s.Images)
                .Where(s => stationIds.Contains(s.Id))
                .ToListAsync();

            var result = topStations.Select((t, index) =>
            {
                var station = stations.First(s => s.Id == t.StationId);
                return new TopFavoriteStationDto
                {
                    Rank = index + 1,
                    StationId = station.Id,
                    Name = station.Name,
                    Address = station.Address,
                    ImageUrl = station.Images.FirstOrDefault()?.ImageUrl,
                    AverageRating = station.AverageRating,
                    TotalReviews = station.TotalReviews,
                    FavoriteCount = t.FavoriteCount
                };
            }).ToList();

            return Ok(result);
        }

        /// <summary>Check xem Driver đã yêu thích trạm này chưa.</summary>
        [HttpGet("{stationId}/check")]
        public async Task<IActionResult> CheckFavorite(int stationId)
        {
            var userId = GetUserId();
            var isFavorite = await _db.FavoriteStations
                .AnyAsync(f => f.DriverUserId == userId && f.StationId == stationId);
            return Ok(new { isFavorite });
        }
    }
}
