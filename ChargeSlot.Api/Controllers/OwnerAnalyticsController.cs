using ChargeSlot.Api.Constants;
using ChargeSlot.Api.DTOs.Analytics;
using ChargeSlot.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ChargeSlot.Api.Controllers
{
    [ApiController]
    [Route("api/owner/analytics")]
    [Authorize(Roles = RoleConstants.Owner)]
    public class OwnerAnalyticsController : ControllerBase
    {
        private readonly IDashboardService _dashboardService;

        public OwnerAnalyticsController(IDashboardService dashboardService)
        {
            _dashboardService = dashboardService;
        }

        private int GetUserId()
        {
            var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(idClaim)) throw new UnauthorizedAccessException("Không tìm thấy thông tin xác thực.");
            return int.Parse(idClaim);
        }

        /// <summary>Lấy số liệu thô cho Dashboard Chủ Trạm</summary>
        [HttpGet("metrics")]
        public async Task<ActionResult<OwnerDashboardMetricsDto>> GetMetrics([FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate)
        {
            var ownerId = GetUserId();
            var metrics = await _dashboardService.GetOwnerMetricsAsync(ownerId, fromDate, toDate);
            return Ok(metrics);
        }
    }
}
