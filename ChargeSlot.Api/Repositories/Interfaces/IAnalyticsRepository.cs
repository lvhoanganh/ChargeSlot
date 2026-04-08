using ChargeSlot.Api.DTOs.Admin;
using ChargeSlot.Api.DTOs.Analytics;

namespace ChargeSlot.Api.Repositories.Interfaces
{
    public interface IAnalyticsRepository
    {
        Task<AdminDashboardMetricsDto> GetAdminMetricsAsync(DateTime start, DateTime end);
        Task<OwnerDashboardMetricsDto> GetOwnerMetricsAsync(int ownerUserId, DateTime start, DateTime end);
        
        Task<RevenueSummaryDto> GetRevenueSummaryAsync(DateTime? since);
        Task<List<MonthlyRevenueDto>> GetMonthlyRevenueAsync(DateTime? since);
        Task<List<TopStationDto>> GetTopStationsAsync(DateTime? since, int limit);
        Task<List<RecentTransactionDto>> GetRecentTransactionsAsync(int limit);
        Task<List<VatReportDto>> GetVatReportAsync(DateTime? since);
    }
}
