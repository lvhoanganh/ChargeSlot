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
        private readonly IAiInsightsService _aiInsightsService;

        public AdminAnalyticsController(IDashboardService dashboardService, IAiInsightsService aiInsightsService)
        {
            _dashboardService = dashboardService;
            _aiInsightsService = aiInsightsService;
        }

        /// <summary>Lấy số liệu thô cho Dashboard Admin</summary>
        [HttpGet("metrics")]
        public async Task<ActionResult<AdminDashboardMetricsDto>> GetMetrics([FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate)
        {
            var metrics = await _dashboardService.GetAdminMetricsAsync(fromDate, toDate);
            return Ok(metrics);
        }

        /// <summary>Gọi AI phân tích số liệu cho Admin</summary>
        [HttpGet("ai-insights")]
        public async Task<ActionResult<AiInsightResponseDto>> GetAiInsights([FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate)
        {
            var metrics = await _dashboardService.GetAdminMetricsAsync(fromDate, toDate);
            var insight = await _aiInsightsService.GenerateAdminInsightAsync(metrics);
            return Ok(insight);
        }
    }
}
