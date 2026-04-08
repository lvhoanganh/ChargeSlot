using System.Security.Claims;
using ChargeSlot.Api.Constants;
using ChargeSlot.Api.DTOs.Loyalty;
using ChargeSlot.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ChargeSlot.Api.Services.Interfaces;

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
        private readonly ILoyaltyService _loyaltyService;

        public LoyaltyController(ILoyaltyService loyaltyService)
        {
            _loyaltyService = loyaltyService;
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

            try
            {
                var info = await _loyaltyService.GetLoyaltyInfoAsync(userId);
                return Ok(info);
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }
    }
}
