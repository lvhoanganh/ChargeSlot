using ChargeSlot.Api.DTOs.Kyc;
using ChargeSlot.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ChargeSlot.Api.Controllers
{
    [ApiController]
    [Route("api/owner/kyc")]
    [Authorize(Roles = "Owner")]
    public class OwnerKycController : ControllerBase
    {
        private readonly IKycService _kycService;

        public OwnerKycController(IKycService kycService)
        {
            _kycService = kycService;
        }

        private int GetUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        /// <summary>
        /// Xem trạng thái và hồ sơ KYC hiện tại
        /// </summary>
        [HttpGet("status")]
        public async Task<IActionResult> GetStatus()
        {
            try
            {
                var result = await _kycService.GetKycProfileAsync(GetUserId());
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Nộp hồ sơ KYC (CCCD, GPKD)
        /// </summary>
        [HttpPost("submit")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> SubmitKyc([FromForm] SubmitKycDto dto)
        {
            try
            {
                // Verify DTO fields are completed implicitly by [Required]
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                var result = await _kycService.SubmitKycAsync(GetUserId(), dto);
                return Ok(new { message = "Đã nộp hồ sơ KYC, vui lòng chờ Admin duyệt.", profile = result });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
