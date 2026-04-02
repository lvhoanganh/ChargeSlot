using ChargeSlot.Api.DTOs.Analytics;

namespace ChargeSlot.Api.Services.Interfaces
{
    public interface IDashboardService
    {
        Task<AdminDashboardMetricsDto> GetAdminMetricsAsync(DateTime? fromDate = null, DateTime? toDate = null);
        Task<OwnerDashboardMetricsDto> GetOwnerMetricsAsync(int ownerUserId, DateTime? fromDate = null, DateTime? toDate = null);
    }
}
