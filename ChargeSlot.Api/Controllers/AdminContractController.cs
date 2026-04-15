using ChargeSlot.Api.Constants;
using ChargeSlot.Api.DTOs.Contract;
using ChargeSlot.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChargeSlot.Api.Controllers
{
    [ApiController]
    [Route("api/admin/contracts")]
    [Authorize(Roles = RoleConstants.Admin)]
    public class AdminContractController : ControllerBase
    {
        private readonly IContractService _contractService;

        public AdminContractController(IContractService contractService)
        {
            _contractService = contractService;
        }

        /// <summary>Danh sách tất cả hợp đồng (hỗ trợ lọc theo trạng thái: Pending, Signed, Expired, Terminated).</summary>
        [HttpGet]
        public async Task<ActionResult<ChargeSlot.Api.DTOs.PagedResultDto<ContractPreviewDto>>> GetAllContracts(
            [FromQuery] string? status = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            var result = await _contractService.GetAllContractsPagedAsync(status, page, pageSize);
            return Ok(result);
        }

        /// <summary>Xem hợp đồng của một Owner.</summary>
        [HttpGet("{ownerUserId:int}")]
        public async Task<ActionResult<ContractPreviewDto>> GetContractByOwner(int ownerUserId)
        {
            var result = await _contractService.GetContractByOwnerForAdminAsync(ownerUserId);
            if (result == null) return NotFound(new { message = "Owner chưa có hợp đồng." });
            return Ok(result);
        }

        /// <summary>Download PDF hợp đồng của Owner.</summary>
        [HttpGet("{ownerUserId:int}/download")]
        public async Task<IActionResult> DownloadContract(int ownerUserId)
        {
            var pdfBytes = await _contractService.DownloadContractPdfForAdminAsync(ownerUserId);
            if (pdfBytes == null) return NotFound(new { message = "Owner chưa có hợp đồng." });
            return File(pdfBytes, "application/pdf", $"HopDong_Owner_{ownerUserId}.pdf");
        }

        /// <summary>Chấm dứt hợp đồng của Owner.</summary>
        [HttpPut("{ownerUserId:int}/terminate")]
        public async Task<IActionResult> TerminateContract(int ownerUserId, [FromBody] TerminateContractDto dto)
        {
            await _contractService.TerminateContractAsync(ownerUserId, dto.Reason);
            return Ok(new { message = "Hợp đồng đã được chấm dứt." });
        }
    }
}
