using System.Security.Claims;
using ChargeSlot.Api.Constants;
using ChargeSlot.Api.DTOs.Loyalty;
using ChargeSlot.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ChargeSlot.Api.Controllers
{
    /// <summary>
    /// Loyalty points endpoints cho Driver.
    /// </summary>
    [ApiController]
    [Route("api/loyalty")]
    [Authorize(Roles = RoleConstants.Driver)]
    public class LoyaltyController : ControllerBase
    {
        private readonly ChargeSlotDbContext _db;

        public LoyaltyController(ChargeSlotDbContext db)
        {
            _db = db;
        }

        private int GetUserId()
        {
            var id = User.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? throw new InvalidOperationException("UserId missing in token");
            return int.Parse(id);
        }

        /// <summary>Xem điểm tích lũy + lịch sử gần nhất.</summary>
        [HttpGet]
        public async Task<IActionResult> GetLoyaltyInfo()
        {
            var userId = GetUserId();

            var driver = await _db.Driver.FirstOrDefaultAsync(d => d.UserId == userId);
            if (driver == null) return NotFound(new { message = "Driver profile không tồn tại." });

            // Load config
            var earnRateConfig = await _db.SystemConfigs.FindAsync("LoyaltyEarnRate");
            var maxRedeemConfig = await _db.SystemConfigs.FindAsync("LoyaltyMaxRedeemRate");
            var earnRate = decimal.TryParse(earnRateConfig?.Value, out var er) ? er : 0.05m;
            var maxRedeemRate = decimal.TryParse(maxRedeemConfig?.Value, out var mr) ? mr : 0.5m;

            // Lịch sử 20 giao dịch gần nhất
            var history = await _db.LoyaltyTransactions
                .Where(t => t.DriverUserId == userId)
                .OrderByDescending(t => t.CreatedAt)
                .Take(20)
                .Select(t => new LoyaltyTransactionDto
                {
                    Id = t.Id,
                    BookingId = t.BookingId,
                    Type = t.Type,
                    Points = t.Points,
                    Description = t.Description,
                    CreatedAt = t.CreatedAt
                })
                .ToListAsync();

            return Ok(new LoyaltyInfoDto
            {
                CurrentPoints = driver.LoyaltyPoints,
                EarnRate = earnRate,
                MaxRedeemRate = maxRedeemRate,
                RecentHistory = history
            });
        }
    }
}
