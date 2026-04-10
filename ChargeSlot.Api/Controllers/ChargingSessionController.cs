using ChargeSlot.Api.DTOs.ChargingSession;
using ChargeSlot.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ChargeSlot.Api.Controllers
{
    [ApiController]
    [Route("api/charging")]
    [Authorize]
    public class ChargingSessionController : ControllerBase
    {
        private readonly IChargingSessionService _sessionService;

        public ChargingSessionController(IChargingSessionService sessionService)
        {
            _sessionService = sessionService;
        }

        private int GetUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        /// <summary>Driver quét QR code trên slot để check-in.</summary>
        [HttpPost("check-in")]
        [Authorize(Roles = "Driver")]
        public async Task<IActionResult> CheckIn([FromBody] CheckInDto dto)
        {
            try
            {
                var result = await _sessionService.CheckInAsync(GetUserId(), dto.QrCodeToken);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>Owner dừng phiên sạc và tạo hóa đơn.</summary>
        [HttpPut("{sessionId}/stop")]
        [Authorize(Roles = "Owner")]
        public async Task<IActionResult> StopCharging(int sessionId)
        {
            try
            {
                var result = await _sessionService.StopChargingAsync(GetUserId(), sessionId);
                return Ok(result);
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

        /// <summary>Driver yêu cầu kết thúc sạc sớm → Owner mới được dừng phiên.</summary>
        [HttpPut("{sessionId}/request-early-end")]
        [Authorize(Roles = "Driver")]
        public async Task<IActionResult> RequestEarlyEnd(int sessionId)
        {
            try
            {
                var result = await _sessionService.RequestEarlyEndAsync(GetUserId(), sessionId);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>Driver xác nhận hóa đơn → hoàn thành booking.</summary>
        [HttpPut("{sessionId}/confirm")]
        [Authorize(Roles = "Driver")]
        public async Task<IActionResult> ConfirmCompletion(int sessionId)
        {
            try
            {
                var result = await _sessionService.ConfirmCompletionAsync(GetUserId(), sessionId);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>Owner xem các phiên sạc đang diễn ra.</summary>
        [HttpGet("active")]
        [Authorize(Roles = "Owner")]
        public async Task<IActionResult> GetActiveSessions()
        {
            var result = await _sessionService.GetActiveByOwnerAsync(GetUserId());
            return Ok(result);
        }

        /// <summary>Xem phiên sạc theo booking ID.</summary>
        [HttpGet("booking/{bookingId}")]
        public async Task<IActionResult> GetByBookingId(int bookingId)
        {
            var result = await _sessionService.GetByBookingIdAsync(bookingId);
            if (result == null) return NotFound();
            return Ok(result);
        }

        /// <summary>Xem hóa đơn theo booking ID.</summary>
        [HttpGet("invoice/{bookingId}")]
        public async Task<IActionResult> GetInvoiceByBookingId(int bookingId)
        {
            var result = await _sessionService.GetInvoiceByBookingIdAsync(bookingId);
            if (result == null) return NotFound();
            return Ok(result);
        }

        /// <summary>Driver gửi yêu cầu xác nhận thủ công khi không check-in được (lỗi mạng/app).</summary>
        [HttpPost("manual-checkin-request/{bookingId}")]
        [Authorize(Roles = "Driver")]
        public async Task<IActionResult> RequestManualCheckin(int bookingId)
        {
            try
            {
                var result = await _sessionService.RequestManualCheckinAsync(GetUserId(), bookingId);
                return Ok(result);
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

        /// <summary>Owner xác nhận manual check-in → hoàn thành booking.</summary>
        [HttpPut("manual-checkin-confirm/{bookingId}")]
        [Authorize(Roles = "Owner")]
        public async Task<IActionResult> ConfirmManualCheckin(int bookingId)
        {
            try
            {
                var result = await _sessionService.ConfirmManualCheckinAsync(GetUserId(), bookingId);
                return Ok(result);
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
    }
}
