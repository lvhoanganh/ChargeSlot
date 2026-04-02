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

        /// <summary>Xem danh sách yêu cầu rút tiền đang chờ duyệt</summary>
        [HttpGet("pending")]
        public async Task<IActionResult> GetPendingWithdraws()
        {
            var result = await _walletService.GetAllPendingWithdrawsAsync();
            return Ok(result);
        }

        /// <summary>Xem tất cả yêu cầu rút tiền (mọi trạng thái)</summary>
        [HttpGet]
        public async Task<IActionResult> GetAllWithdraws()
        {
            var result = await _walletService.GetAllWithdrawsAsync();
            return Ok(result);
        }

        /// <summary>Duyệt / từ chối yêu cầu rút tiền (Pending → Approved/Rejected)</summary>
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

        /// <summary>
        /// Admin đã chuyển khoản thật → upload ảnh biên lai (Approved → TransferCompleted).
        /// </summary>
        [HttpPut("{id}/confirm-transfer")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> ConfirmTransfer(int id, [FromForm] IFormFile receiptImage)
        {
            try
            {
                if (receiptImage == null || receiptImage.Length == 0)
                    return BadRequest(new { message = "Vui lòng upload ảnh biên lai chuyển khoản." });

                var ext = Path.GetExtension(receiptImage.FileName).ToLowerInvariant();
                if (ext is not (".jpg" or ".jpeg" or ".png" or ".webp"))
                    return BadRequest(new { message = "Chỉ chấp nhận file ảnh (jpg, png, webp)." });

                if (receiptImage.Length > 5 * 1024 * 1024)
                    return BadRequest(new { message = "File quá lớn. Tối đa 5MB." });

                var result = await _walletService.ConfirmTransferAsync(GetUserId(), id, receiptImage);
                return Ok(new
                {
                    message = "Đã xác nhận chuyển khoản và upload biên lai",
                    withdrawRequest = result
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Admin xử lý issue rút tiền (IssueReported → Rejected hoặc TransferCompleted).
        /// </summary>
        [HttpPut("{id}/resolve-issue")]
        public async Task<IActionResult> ResolveIssue(int id, [FromBody] ResolveWithdrawIssueDto dto)
        {
            try
            {
                var result = await _walletService.AdminResolveIssueAsync(GetUserId(), id, dto.Refund, dto.AdminNote);
                return Ok(new
                {
                    message = dto.Refund ? "Đã hoàn tiền về ví" : "Đã chuyển khoản lại, chờ user xác nhận",
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
