using ChargeSlot.Api.Constants;
using ChargeSlot.Api.DTOs.Admin.Overview;
using ChargeSlot.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace ChargeSlot.Api.Controllers
{
    [ApiController]
    [Route("api/admin/operations")]
    [Authorize(Roles = RoleConstants.Admin)]
    public class AdminOperationsController : ControllerBase
    {
        private readonly IBookingService _bookingService;
        private readonly IChargingSessionService _sessionService;

        public AdminOperationsController(IBookingService bookingService, IChargingSessionService sessionService)
        {
            _bookingService = bookingService;
            _sessionService = sessionService;
        }

        /// <summary>
        /// Xem tất cả các Bookings (Lọc động + Phân trang)
        /// </summary>
        [HttpGet("bookings")]
        public async Task<IActionResult> GetAllBookings([FromQuery] BookingFilterDto filter)
        {
            var result = await _bookingService.GetAdminAllBookingsAsync(filter);
            return Ok(result);
        }

        /// <summary>
        /// Xem tất cả các Phiên sạc vật lý (Lọc động + Phân trang)
        /// </summary>
        [HttpGet("sessions")]
        public async Task<IActionResult> GetAllSessions([FromQuery] SessionFilterDto filter)
        {
            var result = await _sessionService.GetAdminAllSessionsAsync(filter);
            return Ok(result);
        }

        /// <summary>
        /// Xem tất cả các Hóa đơn (Lọc động + Phân trang)
        /// </summary>
        [HttpGet("invoices")]
        public async Task<IActionResult> GetAllInvoices([FromQuery] InvoiceFilterDto filter)
        {
            var result = await _sessionService.GetAdminAllInvoicesAsync(filter);
            return Ok(result);
        }
    }
}
