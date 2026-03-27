using ChargeSlot.Api.Constants;
using ChargeSlot.Api.DTOs.Wallet;
using ChargeSlot.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ChargeSlot.Api.Controllers
{
    [ApiController]
    [Route("api/admin/withdraws")]
    [Authorize(Roles = RoleConstants.Admin)]
    public class AdminWithdrawController : ControllerBase
    {
        private readonly IWalletService _walletService;

        public AdminWithdrawController(IWalletService walletService)
        {
            _walletService = walletService;
        }

        private int GetUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        /// <summary>
        /// Xem danh sách yêu cầu rút tiền đang chờ duyệt
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetPendingWithdraws()
        {
            var result = await _walletService.GetAllPendingWithdrawsAsync();
            return Ok(result);
        }

        /// <summary>
        /// Duyệt / từ chối yêu cầu rút tiền
        /// </summary>
        [HttpPut("{id}/process")]
        public async Task<IActionResult> ProcessWithdraw(int id, [FromBody] ProcessWithdrawDto dto)
        {
            try
            {
                var result = await _walletService.ProcessWithdrawAsync(GetUserId(), id, dto);
                return Ok(new
                {
                    message = dto.Approve ? "Đã duyệt yêu cầu rút tiền" : "Đã từ chối yêu cầu rút tiền",
                    withdrawRequest = result
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
