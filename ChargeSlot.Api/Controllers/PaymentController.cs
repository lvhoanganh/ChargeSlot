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
            var authHeader = Request.Headers["Authorization"].FirstOrDefault();
            var providedToken = authHeader?.Replace("Bearer ", "", StringComparison.OrdinalIgnoreCase)
                                          .Replace("Apikey ", "", StringComparison.OrdinalIgnoreCase)
                                          .Trim();
            var expectedToken = configuration["SePay:WebhookToken"];

            if (string.IsNullOrEmpty(expectedToken) || providedToken != expectedToken)
            {
                return Unauthorized(new { success = false, message = "Invalid SePay Integration Token" });
            }

            await _paymentService.ProcessSePayWebhookAsync(request);

            // SePay yêu cầu: HTTP 200 + { "success": true } = webhook đã nhận thành công
            return Ok(new SePayWebhookResponse
            {
                success = true,
                message = "Webhook received"
            });
        }
    }
}
