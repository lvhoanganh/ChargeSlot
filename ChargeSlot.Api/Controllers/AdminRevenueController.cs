using ChargeSlot.Api.Constants;
using ChargeSlot.Api.DTOs.Admin;
using ChargeSlot.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChargeSlot.Api.Controllers
{
    [ApiController]
    [Route("api/admin/revenue")]
    [Authorize(Roles = RoleConstants.Admin)]
    public class AdminRevenueController : ControllerBase
    {
        private readonly IAdminRevenueService _revenueService;

        public AdminRevenueController(IAdminRevenueService revenueService)
        {
            _revenueService = revenueService;
        }

        /// <summary>Tổng quan doanh thu: revenue, fees, VAT, bookings, stations, drivers.</summary>
        [HttpGet("summary")]
        public async Task<ActionResult<RevenueSummaryDto>> GetSummary([FromQuery] string period = "all")
        {
            var result = await _revenueService.GetSummaryAsync(period);
            return Ok(result);
        }

        /// <summary>Doanh thu theo tháng.</summary>
        [HttpGet("monthly")]
        public async Task<ActionResult<List<MonthlyRevenueDto>>> GetMonthlyRevenue([FromQuery] string period = "all")
        {
            var result = await _revenueService.GetMonthlyRevenueAsync(period);
            return Ok(result);
        }

        /// <summary>Top trạm doanh thu cao nhất.</summary>
        [HttpGet("top-stations")]
        public async Task<ActionResult<List<TopStationDto>>> GetTopStations(
            [FromQuery] string period = "all",
            [FromQuery] int limit = 5)
        {
            if (limit < 1 || limit > 50) limit = 5;
            var result = await _revenueService.GetTopStationsAsync(period, limit);
            return Ok(result);
        }

        /// <summary>Giao dịch gần đây (ledger).</summary>
        [HttpGet("recent-transactions")]
        public async Task<ActionResult<List<RecentTransactionDto>>> GetRecentTransactions(
            [FromQuery] int limit = 10)
        {
            if (limit < 1 || limit > 100) limit = 10;
            var result = await _revenueService.GetRecentTransactionsAsync(limit);
            return Ok(result);
        }

        /// <summary>Báo cáo VAT đã thu theo từng Owner — để admin nộp thuế cho cơ quan thuế.</summary>
        [HttpGet("vat-report")]
        public async Task<ActionResult<List<VatReportDto>>> GetVatReport([FromQuery] string period = "all")
        {
            var result = await _revenueService.GetVatReportAsync(period);
            return Ok(result);
        }
    }
}
