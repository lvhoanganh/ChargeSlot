using ChargeSlot.Api.Constants;
using ChargeSlot.Api.DTOs.Payout;
using ChargeSlot.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ChargeSlot.Api.Controllers
{
    [ApiController]
    [Route("api/admin/payouts")]
    [Authorize(Roles = RoleConstants.Admin)]
    public class AdminPayoutController : ControllerBase
    {
        private readonly IPayoutService _payoutService;

        public AdminPayoutController(IPayoutService payoutService)
        {
            _payoutService = payoutService;
        }

        private int GetUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        /// <summary>Xem danh sách yêu cầu rút tiền đang chờ duyệt.</summary>
        [HttpGet]
        public async Task<IActionResult> GetPendingPayouts()
        {
            var result = await _payoutService.GetAllPendingAsync();
            return Ok(result);
        }

        /// <summary>Duyệt / từ chối yêu cầu rút tiền.</summary>
        [HttpPut("{id}/process")]
        public async Task<IActionResult> ProcessPayout(int id, [FromBody] ProcessPayoutDto dto)
        {
            try
            {
                var result = await _payoutService.ProcessPayoutAsync(GetUserId(), id, dto);
                return Ok(new
                {
                    message = dto.Approve ? "Đã duyệt yêu cầu rút tiền" : "Đã từ chối yêu cầu rút tiền",
                    payoutRequest = result
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
