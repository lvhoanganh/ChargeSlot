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
        [HttpPut("{disputeId}/owner-evidence")]
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
        [HttpPost("{disputeId}/resolve")]
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

        /// <summary>Danh sách dispute chờ xử lý (Admin).</summary>
        [HttpGet("pending")]
        [Authorize(Roles = RoleConstants.Admin)]
        public async Task<IActionResult> GetPending()
        {
            var result = await _disputeService.GetPendingAsync();
            return Ok(result);
        }

        /// <summary>Tất cả dispute (Admin), filter theo status nếu cần.</summary>
        [HttpGet("all")]
        [Authorize(Roles = RoleConstants.Admin)]
        public async Task<IActionResult> GetAll([FromQuery] string? status = null)
        {
            var result = await _disputeService.GetAllAsync(status);
            return Ok(result);
        }

        /// <summary>Chi tiết dispute.</summary>
        [HttpGet("{disputeId}")]
        public async Task<IActionResult> GetById(int disputeId)
        {
            var result = await _disputeService.GetByIdAsync(disputeId);
            if (result == null) return NotFound();
            return Ok(result);
        }

        /// <summary>Dispute theo booking.</summary>
        [HttpGet("booking/{bookingId}")]
        public async Task<IActionResult> GetByBookingId(int bookingId)
        {
            var result = await _disputeService.GetByBookingIdAsync(bookingId);
            if (result == null) return NotFound();
            return Ok(result);
        }
    }
}
