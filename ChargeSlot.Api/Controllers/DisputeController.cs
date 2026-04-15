using ChargeSlot.Api.Constants;
using ChargeSlot.Api.DTOs.Dispute;
using ChargeSlot.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ChargeSlot.Api.Controllers
{
    [ApiController]
    [Route("api/dispute")]
    [Authorize]
    public class DisputeController : ControllerBase
    {
        private readonly IDisputeService _disputeService;

        public DisputeController(IDisputeService disputeService)
        {
            _disputeService = disputeService;
        }

        private int GetUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        /// <summary>Driver tạo khiếu nại + upload bằng chứng (multipart/form-data).</summary>
        [HttpPost]
        [Authorize(Roles = "Driver")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> SubmitDispute([FromForm] CreateDisputeDto dto)
        {
            try
            {
                var result = await _disputeService.SubmitDisputeAsync(GetUserId(), dto);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>Owner phản hồi + nộp bằng chứng (multipart/form-data).</summary>
        [HttpPut("{disputeId:int}/owner-evidence")]
        [Authorize(Roles = "Owner")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> SubmitOwnerEvidence(int disputeId, [FromForm] OwnerEvidenceDto dto)
        {
            try
            {
                var result = await _disputeService.SubmitOwnerEvidenceAsync(GetUserId(), disputeId, dto);
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

        /// <summary>Admin phán quyết khiếu nại.</summary>
        [HttpPost("{disputeId:int}/resolve")]
        [Authorize(Roles = RoleConstants.Admin)]
        public async Task<IActionResult> ResolveDispute(int disputeId, [FromBody] ResolveDisputeDto dto)
        {
            try
            {
                var result = await _disputeService.ResolveDisputeAsync(GetUserId(), disputeId, dto);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>Danh sách dispute chờ xử lý (Admin, phân trang).</summary>
        [HttpGet("pending")]
        [Authorize(Roles = RoleConstants.Admin)]
        public async Task<IActionResult> GetPending(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            var result = await _disputeService.GetPendingPagedAsync(page, pageSize);
            return Ok(result);
        }

        /// <summary>Tất cả dispute (Admin, phân trang), filter theo status + ngày.</summary>
        [HttpGet("all")]
        [Authorize(Roles = RoleConstants.Admin)]
        public async Task<IActionResult> GetAll(
            [FromQuery] string? status = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            var result = await _disputeService.GetAllPagedAsync(status, page, pageSize);
            return Ok(result);
        }

        /// <summary>Driver xem danh sách khiếu nại của mình (phân trang).</summary>
        [HttpGet("my")]
        [Authorize(Roles = "Driver")]
        public async Task<IActionResult> GetMyDisputes(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            var result = await _disputeService.GetMyDisputesPagedAsync(GetUserId(), page, pageSize);
            return Ok(result);
        }

        /// <summary>Owner xem danh sách khiếu nại liên quan (phân trang).</summary>
        [HttpGet("owner")]
        [Authorize(Roles = "Owner")]
        public async Task<IActionResult> GetOwnerDisputes(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            var result = await _disputeService.GetOwnerDisputesPagedAsync(GetUserId(), page, pageSize);
            return Ok(result);
        }

        /// <summary>Chi tiết dispute.</summary>
        [HttpGet("{disputeId:int}")]
        public async Task<IActionResult> GetById(int disputeId)
        {
            var role = User.FindFirstValue(ClaimTypes.Role) ?? "";
            try
            {
                var result = await _disputeService.GetByIdAsync(disputeId, GetUserId(), role);
                if (result == null) return NotFound();
                return Ok(result);
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
        }

        /// <summary>Dispute theo booking.</summary>
        [HttpGet("booking/{bookingId:int}")]
        public async Task<IActionResult> GetByBookingId(int bookingId)
        {
            var role = User.FindFirstValue(ClaimTypes.Role) ?? "";
            try
            {
                var result = await _disputeService.GetByBookingIdAsync(bookingId, GetUserId(), role);
                if (result == null) return NotFound();
                return Ok(result);
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
        }

        /// <summary>Driver xem số lượt thua dispute trong tháng / còn bao nhiêu lượt trước khi bị ban.</summary>
        [HttpGet("strike-status/driver")]
        [Authorize(Roles = "Driver")]
        public async Task<IActionResult> GetDriverStrikeStatus()
        {
            var result = await _disputeService.GetDriverStrikeStatusAsync(GetUserId());
            return Ok(result);
        }

        /// <summary>Owner xem số lượt thua dispute trong tháng của trạm / còn bao nhiêu lượt trước khi bị đình chỉ.</summary>
        [HttpGet("strike-status/station/{stationId:int}")]
        [Authorize(Roles = "Owner")]
        public async Task<IActionResult> GetStationStrikeStatus(int stationId)
        {
            try
            {
                var result = await _disputeService.GetStationStrikeStatusAsync(stationId, GetUserId());
                return Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
        }
    }
}
