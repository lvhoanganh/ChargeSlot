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
        /// Nạp tiền vào ví qua VNPay → trả về URL redirect
        /// </summary>
        [HttpPost("top-up")]
        public async Task<IActionResult> TopUp([FromBody] TopUpDto dto)
        {
            try
            {
                var paymentUrl = await _walletService.TopUpViaVnPayAsync(GetUserId(), dto.Amount, HttpContext);
                return Ok(new { paymentUrl });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// VNPay callback cho nạp tiền ví
        /// </summary>
        [HttpGet("top-up/vnpay-return")]
        [AllowAnonymous]
        public async Task<IActionResult> TopUpVnPayReturn()
        {
            await _walletService.ProcessTopUpCallbackAsync(Request.Query);
            var responseCode = Request.Query["vnp_ResponseCode"].ToString();
            var success = responseCode == "00";
            return Ok(new { success, message = success ? "Nạp tiền thành công" : "Nạp tiền thất bại" });
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
        }

        /// <summary>
        /// Rút tiền từ ví (freeze → chờ Admin duyệt)
        /// </summary>
        [HttpPost("withdraw")]
        public async Task<IActionResult> Withdraw([FromBody] WithdrawDto dto)
        {
            try
            {
                var result = await _walletService.WithdrawAsync(GetUserId(), dto.Amount);
                return Ok(new { message = "Yêu cầu rút tiền đã được gửi", wallet = result });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Lịch sử giao dịch ví
        /// </summary>
        [HttpGet("transactions")]
        public async Task<IActionResult> GetTransactions()
        {
            var result = await _walletService.GetTransactionHistoryAsync(GetUserId());
            return Ok(result);
        }
    }
}
