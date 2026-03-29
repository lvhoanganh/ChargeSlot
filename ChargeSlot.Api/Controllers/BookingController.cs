using ChargeSlot.Api.DTOs.Booking;
using ChargeSlot.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ChargeSlot.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class BookingController : ControllerBase
    {
        private readonly IBookingService _bookingService;

        public BookingController(IBookingService bookingService)
        {
            _bookingService = bookingService;
        }

        private int GetUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        /// <summary>
        /// Driver tạo booking request (Step 4-9 trong diagram)
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "Driver")]
        public async Task<IActionResult> CreateBooking([FromBody] CreateBookingDto dto)
        {
            try
            {
                var result = await _bookingService.CreateBookingAsync(GetUserId(), dto);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Owner xem danh sách booking requests (Step 10)
        /// </summary>
        [HttpGet("owner")]
        [Authorize(Roles = "Owner")]
        public async Task<IActionResult> GetOwnerBookings()
        {
            var result = await _bookingService.GetByOwnerAsync(GetUserId());
            return Ok(result);
        }

        /// <summary>
        /// Owner chấp nhận booking (Step 14 → Notify → PendingPayment)
        /// </summary>
        [HttpPut("{id}/accept")]
        [Authorize(Roles = "Owner")]
        public async Task<IActionResult> AcceptBooking(int id)
        {
            try
            {
                var result = await _bookingService.AcceptBookingAsync(GetUserId(), id);
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

        /// <summary>
        /// Owner từ chối booking + lý do (Step 12-13 → Notify → END)
        /// </summary>
        [HttpPut("{id}/reject")]
        [Authorize(Roles = "Owner")]
        public async Task<IActionResult> RejectBooking(int id, [FromBody] RejectBookingDto dto)
        {
            try
            {
                var result = await _bookingService.RejectBookingAsync(GetUserId(), id, dto);
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

        /// <summary>
        /// Driver xem danh sách booking của mình (filter theo status nếu cần)
        /// </summary>
        [HttpGet("driver")]
        [Authorize(Roles = "Driver")]
        public async Task<IActionResult> GetDriverBookings([FromQuery] string? status = null)
        {
            var result = await _bookingService.GetByDriverAsync(GetUserId());
            if (!string.IsNullOrEmpty(status))
                result = result.Where(b => b.Status.Equals(status, StringComparison.OrdinalIgnoreCase)).ToList();
            return Ok(result);
        }

        /// <summary>
        /// Driver xem lịch sử booking đã hoàn thành
        /// </summary>
        [HttpGet("driver/history")]
        [Authorize(Roles = "Driver")]
        public async Task<IActionResult> GetDriverBookingHistory()
        {
            var result = await _bookingService.GetByDriverAsync(GetUserId());
            var history = result.Where(b =>
                b.Status.Equals("Completed", StringComparison.OrdinalIgnoreCase) ||
                b.Status.Equals("Cancelled", StringComparison.OrdinalIgnoreCase) ||
                b.Status.Equals("DriverCancelled", StringComparison.OrdinalIgnoreCase) ||
                b.Status.Equals("OwnerCancelled", StringComparison.OrdinalIgnoreCase) ||
                b.Status.Equals("Rejected", StringComparison.OrdinalIgnoreCase) ||
                b.Status.Equals("Expired", StringComparison.OrdinalIgnoreCase)
            ).ToList();
            return Ok(history);
        }

        /// <summary>
        /// Xem chi tiết booking
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetBooking(int id)
        {
            var result = await _bookingService.GetByIdAsync(id);
            if (result == null) return NotFound();
            return Ok(result);
        }

        /// <summary>
        /// Preview phí hủy trước khi Driver xác nhận hủy.
        /// FE gọi API này trước → hiện popup cảnh báo → Driver xác nhận → FE gọi driver-cancel.
        /// </summary>
        [HttpGet("{id}/cancel-preview")]
        [Authorize(Roles = "Driver")]
        public async Task<IActionResult> GetCancelPreview(int id)
        {
            try
            {
                var result = await _bookingService.GetCancelPreviewAsync(GetUserId(), id);
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

        /// <summary>
        /// Driver hủy booking (WaitingOwner / PendingPayment / Paid)
        /// Paid: hoàn tiền theo chính sách (≥2h=100%, 1-2h=50%, &lt;1h=0%)
        /// </summary>
        [HttpPut("{id}/driver-cancel")]
        [Authorize(Roles = "Driver")]
        public async Task<IActionResult> DriverCancelBooking(int id, [FromBody] CancelBookingDto? dto)
        {
            try
            {
                var result = await _bookingService.DriverCancelBookingAsync(GetUserId(), id, dto?.CancelReason);
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

        /// <summary>
        /// Owner hủy booking đã Paid → hoàn 100% cho Driver
        /// </summary>
        [HttpPut("{id}/owner-cancel")]
        [Authorize(Roles = "Owner")]
        public async Task<IActionResult> OwnerCancelBooking(int id, [FromBody] CancelBookingDto? dto)
        {
            try
            {
                var result = await _bookingService.OwnerCancelBookingAsync(GetUserId(), id, dto?.CancelReason);
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
