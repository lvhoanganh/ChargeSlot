using ChargeSlot.Api.DTOs.Analytics;

namespace ChargeSlot.Api.Services.Interfaces
{
    public interface IAiInsightsService
    {
        Task<AiInsightResponseDto> GenerateAdminInsightAsync(AdminDashboardMetricsDto metrics);
        Task<AiInsightResponseDto> GenerateOwnerInsightAsync(OwnerDashboardMetricsDto metrics);
    }
}
