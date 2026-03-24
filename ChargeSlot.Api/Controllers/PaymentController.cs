using ChargeSlot.Api.DTOs.Payment;
using ChargeSlot.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ChargeSlot.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PaymentController : ControllerBase
    {
        private readonly IPaymentService _paymentService;

        public PaymentController(IPaymentService paymentService)
        {
            _paymentService = paymentService;
        }

        /// <summary>
        /// Driver tạo payment URL để redirect sang VNPay (Step 17, 21)
        /// </summary>
        [HttpPost("{bookingId}/create-payment-url")]
        [Authorize(Roles = "Driver")]
        public async Task<IActionResult> CreatePaymentUrl(int bookingId)
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
                var paymentUrl = await _paymentService.CreatePaymentUrlAsync(bookingId, userId, HttpContext);
                return Ok(new { paymentUrl });
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
        /// VNPay redirect về frontend sau khi thanh toán — frontend gọi API này để verify
        /// </summary>
        [HttpGet("vnpay-return")]
        [AllowAnonymous]
        public async Task<IActionResult> VnPayReturn()
        {
            var success = await _paymentService.ProcessVnPayCallbackAsync(Request.Query);
            var responseCode = Request.Query["vnp_ResponseCode"].ToString();

            // Redirect về frontend với kết quả
            var frontendUrl = $"http://localhost:5173/payment/result?success={success}&responseCode={responseCode}";
            return Redirect(frontendUrl);
        }

        /// <summary>
        /// VNPay IPN (server-to-server callback) — VNPay gọi trực tiếp
        /// </summary>
        [HttpGet("vnpay-ipn")]
        [AllowAnonymous]
        public async Task<IActionResult> VnPayIpn()
        {
            var success = await _paymentService.ProcessVnPayCallbackAsync(Request.Query);

            if (success)
            {
                return Ok(new { RspCode = "00", Message = "Confirm Success" });
            }
            return Ok(new { RspCode = "99", Message = "Confirm Fail" });
        }
    }
}
