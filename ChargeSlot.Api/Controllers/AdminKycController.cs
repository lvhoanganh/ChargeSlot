using ChargeSlot.Api.Constants;
using ChargeSlot.Api.DTOs.Kyc;
using ChargeSlot.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ChargeSlot.Api.Controllers
{
    [ApiController]
    [Route("api/admin/kyc")]
    [Authorize(Roles = RoleConstants.Admin)]
    public class AdminKycController : ControllerBase
    {
        private readonly IKycService _kycService;

        public AdminKycController(IKycService kycService)
        {
            _kycService = kycService;
        }

        private int GetUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        /// <summary>
        /// Xem danh sách hồ sơ KYC đang chờ duyệt
        /// </summary>
        [HttpGet("pending")]
        public async Task<IActionResult> GetPendingKycs()
        {
            var result = await _kycService.GetPendingKycsAsync();
            return Ok(result);
        }

        /// <summary>
        /// Phê duyệt hoặc Từ chối hồ sơ KYC
        /// </summary>
        [HttpPut("{ownerUserId}/review")]
        public async Task<IActionResult> ReviewKyc(int ownerUserId, [FromBody] ReviewKycDto dto)
        {
            try
            {
                if (!dto.IsApproved && string.IsNullOrWhiteSpace(dto.RejectReason))
                    return BadRequest(new { message = "Vui lòng nhập lý do từ chối." });

                var result = await _kycService.ReviewKycAsync(GetUserId(), ownerUserId, dto);
                var statusStr = dto.IsApproved ? "Đã phê duyệt" : "Đã từ chối";
                return Ok(new { message = $"{statusStr} hồ sơ KYC thành công.", profile = result });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
