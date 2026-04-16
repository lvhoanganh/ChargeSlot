using System.Security.Claims;
using ChargeSlot.Api.Constants;
using ChargeSlot.Api.DTOs.Contract;
using ChargeSlot.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChargeSlot.Api.Controllers
{
    [ApiController]
    [Route("api/owner/contract")]
    [Authorize(Roles = RoleConstants.Owner)]
    public class OwnerContractController : ControllerBase
    {
        private readonly IContractService _contractService;

        public OwnerContractController(IContractService contractService)
        {
            _contractService = contractService;
        }

        private int GetUserId() =>
            int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? throw new InvalidOperationException("UserId missing in token"));

        /// <summary>Xem preview hợp đồng (HTML) + trạng thái.</summary>
        [HttpGet]
        public async Task<ActionResult<ContractPreviewDto>> GetContractPreview()
        {
            var userId = GetUserId();
            var result = await _contractService.GetContractPreviewAsync(userId);
            return Ok(result);
        }

        /// <summary>Ký hợp đồng bằng chữ ký tay (base64).</summary>
        [HttpPost("sign")]
        public async Task<ActionResult<ContractPreviewDto>> SignContract([FromBody] SignContractDto dto)
        {
            var userId = GetUserId();
            var result = await _contractService.SignContractAsync(userId, dto);
            return Ok(result);
        }

        /// <summary>Download PDF hợp đồng đã ký.</summary>
        [HttpGet("download")]
        public async Task<IActionResult> DownloadContract()
        {
            var userId = GetUserId();
            var pdfBytes = await _contractService.DownloadContractPdfAsync(userId);
            return File(pdfBytes, "application/pdf", "HopDong_ChargeSlot.pdf");
        }

        /// <summary>Yêu cầu chấm dứt hợp đồng sớm (Điều 6.3). Yêu cầu không có booking đang hoạt động.</summary>
        [HttpPost("terminate")]
        public async Task<IActionResult> RequestTermination([FromBody] TerminateContractDto dto)
        {
            var userId = GetUserId();
            await _contractService.RequestTerminationAsync(userId, dto.Reason);
            return Ok(new { message = "Hợp đồng đã được chấm dứt thành công. Toàn bộ trạm sạc đã bị đình chỉ hoạt động." });
        }
    }
}
