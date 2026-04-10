using ChargeSlot.Api.DTOs.Admin;
using ChargeSlot.Api.Helpers;
using ChargeSlot.Api.Repositories.Interfaces;
using ChargeSlot.Api.Services.Interfaces;

namespace ChargeSlot.Api.Services.Implementation
{
    public class AdminRevenueService : IAdminRevenueService
    {
        private readonly IAnalyticsRepository _analyticsRepo;

        public AdminRevenueService(IAnalyticsRepository analyticsRepo)
        {
            _analyticsRepo = analyticsRepo;
        }

        public async Task<RevenueSummaryDto> GetSummaryAsync(string period)
        {
            var since = GetSinceDate(period);
            return await _analyticsRepo.GetRevenueSummaryAsync(since);
        }

        public async Task<List<MonthlyRevenueDto>> GetMonthlyRevenueAsync(string period)
        {
            var since = GetSinceDate(period);
            return await _analyticsRepo.GetMonthlyRevenueAsync(since);
        }

        public async Task<List<TopStationDto>> GetTopStationsAsync(string period, int limit)
        {
            var since = GetSinceDate(period);
            return await _analyticsRepo.GetTopStationsAsync(since, limit);
        }

        public async Task<List<RecentTransactionDto>> GetRecentTransactionsAsync(int limit)
        {
            return await _analyticsRepo.GetRecentTransactionsAsync(limit);
        }

        public async Task<List<VatReportDto>> GetVatReportAsync(string period)
        {
            var since = GetSinceDate(period);
            return await _analyticsRepo.GetVatReportAsync(since);
        }

        /// <summary>
        /// Convert period string to DateTime filter.
        /// month = last 30 days, quarter = last 90 days, year = last 365 days, all = null.
        /// </summary>
        private static DateTime? GetSinceDate(string period)
        {
            var now = DateTimeHelper.VietnamNow();
            return period.ToLower() switch
            {
                "month" => now.AddDays(-30),
                "quarter" => now.AddDays(-90),
                "year" => now.AddDays(-365),
                _ => null // "all" or any other value
            };
        }
    }
}
