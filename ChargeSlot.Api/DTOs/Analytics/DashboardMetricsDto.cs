using System.Text.Json.Serialization;

namespace ChargeSlot.Api.DTOs.Analytics
{
    public class AdminDashboardMetricsDto
    {
        public decimal TotalEscrowBalance { get; set; }
        public decimal TotalPlatformRevenue { get; set; }
        public int TotalActiveStations { get; set; }
        public int TotalStations { get; set; }
        public int TotalUsers { get; set; }

        public int BookingsLast30Days { get; set; }
        public decimal CancelRateLast30Days { get; set; } // 0.0 to 1.0
        public int DisputesLast30Days { get; set; }

        public List<StationDisputeSummaryDto> TopDisputedStations { get; set; } = new();
        public List<DriverCollusionRiskDto> HighRiskDrivers { get; set; } = new();
    }

    public class OwnerDashboardMetricsDto
    {
        public int OwnerUserId { get; set; }
        public decimal RevenueLast30Days { get; set; }
        public decimal WalletBalance { get; set; }
        public int TotalStations { get; set; }

        public int BookingsLast30Days { get; set; }
        public decimal CancelRateLast30Days { get; set; }
        public decimal ActiveTimeUtilizationRate { get; set; } // e.g., 0.45 = 45% used during open hours

        public List<StationPerformanceDto> StationPerformances { get; set; } = new();
        public List<ServiceSalesDto> TopServicesSold { get; set; } = new();
    }

    public class StationDisputeSummaryDto
    {
        public int StationId { get; set; }
        public string StationName { get; set; } = string.Empty;
        public int DisputeCount { get; set; }
    }

    public class DriverCollusionRiskDto
    {
        public int DriverUserId { get; set; }
        public string DriverName { get; set; } = string.Empty;
        public int CancelledBookings { get; set; }
        public int TotalBookings { get; set; }
        public string SuspiciousNote { get; set; } = string.Empty;
    }

    public class StationPerformanceDto
    {
        public int StationId { get; set; }
        public string StationName { get; set; } = string.Empty;
        public int TotalBookings { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal AverageRating { get; set; }
    }

    public class ServiceSalesDto
    {
        public string ServiceName { get; set; } = string.Empty;
        public int QuantitySold { get; set; }
        public decimal Revenue { get; set; }
    }

    public class AiInsightResponseDto
    {
        public string InsightMarkdown { get; set; } = string.Empty;
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    }
}
