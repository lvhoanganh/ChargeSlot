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
        /// Driver xem danh sách booking của mình
        /// </summary>
        [HttpGet("driver")]
        [Authorize(Roles = "Driver")]
        public async Task<IActionResult> GetDriverBookings()
        {
            var result = await _bookingService.GetByDriverAsync(GetUserId());
            return Ok(result);
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
    }
}
