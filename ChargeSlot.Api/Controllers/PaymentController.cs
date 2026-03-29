using ChargeSlot.Api.DTOs.Payment;
using ChargeSlot.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.Extensions.Configuration;

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
        /// Driver tạo mã QR thanh toán (SePay/VietQR)
        /// </summary>
        [HttpGet("{bookingId}/sepay-qr")]
        [Authorize(Roles = "Driver")]
        public async Task<IActionResult> CreateSePayQr(int bookingId)
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
                var qrUrl = await _paymentService.CreateSePayQrUrlAsync(bookingId, userId);
                return Ok(new { qrUrl });
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
        /// Webhook nhận thông báo kết quả chuyển khoản từ SePay
        /// </summary>
        [HttpPost("sepay-webhook")]
        [AllowAnonymous]
        public async Task<IActionResult> SePayWebhook([FromBody] SePayWebhookRequest request, [FromServices] IConfiguration configuration)
        {
            // Kiểm tra bảo mật Webhook (chống fake API call)
            var providedToken = Request.Headers["Authorization"].FirstOrDefault()?.Replace("Bearer ", "").Trim();
            var expectedToken = configuration["SePay:WebhookToken"];

            if (string.IsNullOrEmpty(expectedToken) || providedToken != expectedToken)
            {
                return Unauthorized(new { success = false, message = "Invalid SePay Integration Token" });
            }

            var success = await _paymentService.ProcessSePayWebhookAsync(request);

            // Chú ý: SePay document yêu cầu trả về HTTP 200 + { "success": true } để xác nhận đã nhận webhook thành công.
            return Ok(new SePayWebhookResponse
            {
                success = success,
                message = success ? "Processed successfully" : "Warning: Failed to process or idempotency skipped. Check logs."
            });
        }
    }
}
