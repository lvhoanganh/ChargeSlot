using ChargeSlot.Api.DTOs.Wallet;
using ChargeSlot.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ChargeSlot.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class WalletController : ControllerBase
    {
        private readonly IWalletService _walletService;

        public WalletController(IWalletService walletService)
        {
            _walletService = walletService;
        }

        private int GetUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        /// <summary>
        /// Xem thông tin ví (tự tạo nếu chưa có)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetWallet()
        {
            var result = await _walletService.GetOrCreateWalletAsync(GetUserId());
            return Ok(result);
        }

        /// <summary>
        /// Nạp tiền vào ví qua mã VietQR (SePay)
        /// </summary>
        [HttpPost("top-up")]
        public async Task<IActionResult> TopUp([FromBody] TopUpDto dto)
        {
            try
            {
                var qrUrl = await _walletService.GetSePayTopUpQrUrlAsync(GetUserId(), dto.Amount);
                return Ok(new { qrUrl });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }


        /// <summary>
        /// Thanh toán booking bằng số dư ví
        /// </summary>
        [HttpPost("pay-booking/{bookingId}")]
        [Authorize(Roles = "Driver")]
        public async Task<IActionResult> PayBookingByWallet(int bookingId)
        {
            try
            {
                var result = await _walletService.PayBookingByWalletAsync(GetUserId(), bookingId);
                return Ok(new { message = "Thanh toán thành công", wallet = result });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi thanh toán." });
            }
        }

        /// <summary>
        /// Rút tiền từ ví (freeze → chờ Admin duyệt)
        /// </summary>
        [HttpPost("withdraw")]
        public async Task<IActionResult> Withdraw([FromBody] WithdrawDto dto)
        {
            try
            {
                var result = await _walletService.WithdrawAsync(GetUserId(), dto);
                return Ok(new { message = "Yêu cầu rút tiền đã được gửi", withdrawRequest = result });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Xem danh sách yêu cầu rút tiền của mình (phân trang, lọc ngày)
        /// </summary>
        [HttpGet("withdraw-requests")]
        public async Task<IActionResult> GetWithdrawRequests(
            [FromQuery] DateTime? fromDate = null,
            [FromQuery] DateTime? toDate = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            var result = await _walletService.GetUserWithdrawRequestsAsync(GetUserId());
            if (fromDate.HasValue)
                result = result.Where(w => w.RequestedAt >= fromDate.Value.Date).ToList();
            if (toDate.HasValue)
                result = result.Where(w => w.RequestedAt < toDate.Value.Date.AddDays(1)).ToList();
            var total = result.Count;
            var items = result.Skip((page - 1) * pageSize).Take(pageSize).ToList();
            return Ok(new { total, page, pageSize, items });
        }

        /// <summary>
        /// Lịch sử giao dịch ví (phân trang, lọc ngày)
        /// </summary>
        [HttpGet("transactions")]
        public async Task<IActionResult> GetTransactions(
            [FromQuery] DateTime? fromDate = null,
            [FromQuery] DateTime? toDate = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            var result = await _walletService.GetTransactionHistoryAsync(GetUserId());
            if (fromDate.HasValue)
                result = result.Where(t => t.CreatedAt >= fromDate.Value.Date).ToList();
            if (toDate.HasValue)
                result = result.Where(t => t.CreatedAt < toDate.Value.Date.AddDays(1)).ToList();
            var total = result.Count;
            var items = result.Skip((page - 1) * pageSize).Take(pageSize).ToList();
            return Ok(new { total, page, pageSize, items });
        }

        /// <summary>
        /// User xác nhận đã nhận tiền rút (TransferCompleted → Completed)
        /// </summary>
        [HttpPut("withdraw-requests/{id}/confirm")]
        public async Task<IActionResult> ConfirmWithdrawReceived(int id)
        {
            try
            {
                var result = await _walletService.UserConfirmReceivedAsync(GetUserId(), id);
                return Ok(new { message = "Đã xác nhận nhận tiền thành công", withdrawRequest = result });
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// User báo chưa nhận được tiền (TransferCompleted → IssueReported)
        /// </summary>
        [HttpPut("withdraw-requests/{id}/report-issue")]
        public async Task<IActionResult> ReportWithdrawIssue(int id, [FromBody] ReportWithdrawIssueDto dto)
        {
            try
            {
                var result = await _walletService.UserReportIssueAsync(GetUserId(), id, dto.IssueNote);
                return Ok(new { message = "Đã gửi báo cáo, Admin sẽ xử lý sớm nhất", withdrawRequest = result });
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
