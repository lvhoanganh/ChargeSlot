using ChargeSlot.Api.DTOs.Analytics;
using ChargeSlot.Api.Helpers;
using ChargeSlot.Api.Repositories.Interfaces;
using ChargeSlot.Api.Services.Interfaces;

namespace ChargeSlot.Api.Services.Implementation
{
    public class DashboardService : IDashboardService
    {
        private readonly IAnalyticsRepository _analyticsRepo;

        public DashboardService(IAnalyticsRepository analyticsRepo)
        {
            _analyticsRepo = analyticsRepo;
        }

        public async Task<AdminDashboardMetricsDto> GetAdminMetricsAsync(DateTime? fromDate = null, DateTime? toDate = null)
        {
            var end = toDate ?? DateTimeHelper.VietnamNow();
            var start = fromDate ?? end.AddDays(-30);

            return await _analyticsRepo.GetAdminMetricsAsync(start, end);
        }

        public async Task<OwnerDashboardMetricsDto> GetOwnerMetricsAsync(int ownerUserId, DateTime? fromDate = null, DateTime? toDate = null)
        {
            var end = toDate ?? DateTimeHelper.VietnamNow();
            var start = fromDate ?? end.AddDays(-30);

            return await _analyticsRepo.GetOwnerMetricsAsync(ownerUserId, start, end);
        }
    }
}
