using ChargeSlot.Api.Constants;
using ChargeSlot.Api.DTOs.Analytics;
using ChargeSlot.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChargeSlot.Api.Controllers
{
    [ApiController]
    [Route("api/admin/analytics")]
    [Authorize(Roles = RoleConstants.Admin)]
    public class AdminAnalyticsController : ControllerBase
    {
        private readonly IDashboardService _dashboardService;

        public AdminAnalyticsController(IDashboardService dashboardService)
        {
            _dashboardService = dashboardService;
        }

        /// <summary>Lấy số liệu thô cho Dashboard Admin</summary>
        [HttpGet("metrics")]
        public async Task<ActionResult<AdminDashboardMetricsDto>> GetMetrics([FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate)
        {
            var metrics = await _dashboardService.GetAdminMetricsAsync(fromDate, toDate);
            return Ok(metrics);
        }
    }
}
