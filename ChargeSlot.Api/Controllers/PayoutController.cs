using ChargeSlot.Api.Constants;
using ChargeSlot.Api.DTOs.Payout;
using ChargeSlot.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ChargeSlot.Api.Controllers
{
    [ApiController]
    [Route("api/payouts")]
    [Authorize(Roles = RoleConstants.Owner)]
    public class PayoutController : ControllerBase
    {
        private readonly IPayoutService _payoutService;

        public PayoutController(IPayoutService payoutService)
        {
            _payoutService = payoutService;
        }

        private int GetUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        /// <summary>Owner tạo yêu cầu rút tiền (chọn bank account đã lưu).</summary>
        [HttpPost]
        public async Task<IActionResult> CreatePayout([FromBody] CreatePayoutDto dto)
        {
            try
            {
                var result = await _payoutService.CreatePayoutAsync(GetUserId(), dto);
                return Ok(new { message = "Yêu cầu rút tiền đã được gửi", payoutRequest = result });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>Owner xem lịch sử yêu cầu rút tiền.</summary>
        [HttpGet]
        public async Task<IActionResult> GetMyPayouts()
        {
            var result = await _payoutService.GetByOwnerAsync(GetUserId());
            return Ok(result);
        }
    }
}
