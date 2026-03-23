using ChargeSlot.Api.DTOs.Admin;

namespace ChargeSlot.Api.Services.Interfaces
{
    public interface IAdminRevenueService
    {
        Task<RevenueSummaryDto> GetSummaryAsync(string period);
        Task<List<MonthlyRevenueDto>> GetMonthlyRevenueAsync(string period);
        Task<List<TopStationDto>> GetTopStationsAsync(string period, int limit);
        Task<List<RecentTransactionDto>> GetRecentTransactionsAsync(int limit);
        Task<List<VatReportDto>> GetVatReportAsync(string period);
    }
}
