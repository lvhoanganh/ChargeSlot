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
        /// Xem danh sách yêu cầu rút tiền của mình
        /// </summary>
        [HttpGet("withdraw-requests")]
        public async Task<IActionResult> GetWithdrawRequests()
        {
            var result = await _walletService.GetUserWithdrawRequestsAsync(GetUserId());
            return Ok(result);
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
