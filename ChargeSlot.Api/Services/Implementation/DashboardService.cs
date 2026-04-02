using ChargeSlot.Api.Data;
using ChargeSlot.Api.DTOs.Analytics;
using ChargeSlot.Api.Enums;
using ChargeSlot.Api.Helpers;
using ChargeSlot.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ChargeSlot.Api.Services.Implementation
{
    public class DashboardService : IDashboardService
    {
        private readonly ChargeSlotDbContext _db;

        public DashboardService(ChargeSlotDbContext db)
        {
            _db = db;
        }

        public async Task<AdminDashboardMetricsDto> GetAdminMetricsAsync(DateTime? fromDate = null, DateTime? toDate = null)
        {
            var end = toDate ?? DateTimeHelper.VietnamNow();
            var start = fromDate ?? end.AddDays(-30);

            var escrowBal = await _db.Wallets.Where(w => w.SystemCode == "ESCROW").Select(w => w.AvailableBalance).FirstOrDefaultAsync();
            var platformBal = await _db.Wallets.Where(w => w.SystemCode == "PLATFORM_REVENUE").Select(w => w.AvailableBalance).FirstOrDefaultAsync();
            
            var totalStations = await _db.ChargingStations.CountAsync();
            var activeStations = await _db.ChargingStations.CountAsync(s => s.OperationalStatus == OperationalStatus.Active);
            var totalUsers = await _db.Users.CountAsync();

            var bookingsLast30 = await _db.Bookings
                .Where(b => b.CreatedAt >= start && b.CreatedAt <= end)
                .ToListAsync();

            var disputesLast30 = await _db.Disputes
                .Where(d => d.CreatedAt >= start && d.CreatedAt <= end)
                .Include(d => d.Booking).ThenInclude(b => b.ChargingSlot).ThenInclude(s => s.ChargingStation)
                .ToListAsync();

            int totalBookings = bookingsLast30.Count;
            int cancelledBookings = bookingsLast30.Count(b => b.Status == BookingStatus.Cancelled);
            decimal cancelRate = totalBookings > 0 ? (decimal)cancelledBookings / totalBookings : 0m;

            var topDisputed = disputesLast30
                .Where(d => d.Booking?.ChargingSlot?.ChargingStation != null)
                .GroupBy(d => d.Booking.ChargingSlot.StationId)
                .Select(g => new StationDisputeSummaryDto
                {
                    StationId = g.Key,
                    StationName = g.First().Booking.ChargingSlot.ChargingStation.Name,
                    DisputeCount = g.Count()
                })
                .OrderByDescending(x => x.DisputeCount)
                .Take(5)
                .ToList();

            // Simple collusion check: high cancellation rate by user
            var highRiskDrivers = bookingsLast30
                .GroupBy(b => b.DriverUserId)
                .Select(g => new
                {
                    DriverId = g.Key,
                    Total = g.Count(),
                    Cancelled = g.Count(b => b.Status == BookingStatus.Cancelled)
                })
                .Where(x => x.Total >= 5 && x.Cancelled >= x.Total * 0.5m)
                .OrderByDescending(x => x.Cancelled)
                .Take(5)
                .ToList();

            var driverNames = await _db.Users
                .Where(u => highRiskDrivers.Select(h => h.DriverId).Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.FullName ?? "Unknown");

            var driverRiskDtos = highRiskDrivers.Select(h => new DriverCollusionRiskDto
            {
                DriverUserId = h.DriverId,
                DriverName = driverNames.GetValueOrDefault(h.DriverId, "Unknown Driver"),
                TotalBookings = h.Total,
                CancelledBookings = h.Cancelled,
                SuspiciousNote = $"Tài khoản này đã hủy {h.Cancelled}/{h.Total} đơn đặt trong 30 ngày qua (Tỉ lệ {Math.Round((decimal)h.Cancelled/h.Total*100)}%)."
            }).ToList();

            return new AdminDashboardMetricsDto
            {
                TotalEscrowBalance = escrowBal,
                TotalPlatformRevenue = platformBal,
                TotalStations = totalStations,
                TotalActiveStations = activeStations,
                TotalUsers = totalUsers,
                BookingsLast30Days = totalBookings,
                CancelRateLast30Days = cancelRate,
                DisputesLast30Days = disputesLast30.Count,
                TopDisputedStations = topDisputed,
                HighRiskDrivers = driverRiskDtos
            };
        }

        public async Task<OwnerDashboardMetricsDto> GetOwnerMetricsAsync(int ownerUserId, DateTime? fromDate = null, DateTime? toDate = null)
        {
            var end = toDate ?? DateTimeHelper.VietnamNow();
            var start = fromDate ?? end.AddDays(-30);

            var walletBalance = await _db.Wallets
                .Where(w => w.UserId == ownerUserId && w.SystemCode == null)
                .Select(w => w.AvailableBalance)
                .FirstOrDefaultAsync();

            var stations = await _db.ChargingStations
                .Where(s => s.OwnerUserId == ownerUserId)
                .ToListAsync();

            var stationIds = stations.Select(s => s.Id).ToList();

            var bookingsLast30 = await _db.Bookings
                .Include(b => b.ChargingSlot)
                .Include(b => b.BookingExtraServices).ThenInclude(es => es.ExtraService)
                .Where(b => stationIds.Contains(b.ChargingSlot.StationId) && b.CreatedAt >= start && b.CreatedAt <= end)
                .ToListAsync();

            decimal totalRevenue = bookingsLast30
                .Where(b => b.Status == BookingStatus.Completed || b.Status == BookingStatus.Paid)
                .Sum(b => b.TotalAmount); // Simplified gross revenue

            int totalBookings = bookingsLast30.Count;
            int cancelledBookings = bookingsLast30.Count(b => b.Status == BookingStatus.Cancelled);
            decimal cancelRate = totalBookings > 0 ? (decimal)cancelledBookings / totalBookings : 0m;

            var performances = new List<StationPerformanceDto>();
            foreach (var s in stations)
            {
                var sBookings = bookingsLast30.Where(b => b.ChargingSlot.StationId == s.Id).ToList();
                performances.Add(new StationPerformanceDto
                {
                    StationId = s.Id,
                    StationName = s.Name,
                    TotalBookings = sBookings.Count,
                    TotalRevenue = sBookings.Where(b => b.Status == BookingStatus.Completed || b.Status == BookingStatus.Paid).Sum(b => b.TotalAmount),
                    AverageRating = s.AverageRating
                });
            }

            var extraServicesSold = bookingsLast30
                .Where(b => b.Status == BookingStatus.Completed || b.Status == BookingStatus.Paid)
                .SelectMany(b => b.BookingExtraServices ?? new List<Models.BookingExtraService>())
                .GroupBy(es => es.ExtraService?.ServiceName ?? "Dịch vụ khác")
                .Select(g => new ServiceSalesDto
                {
                    ServiceName = g.Key,
                    QuantitySold = g.Sum(es => es.Quantity),
                    Revenue = g.Sum(es => es.TotalPrice)
                })
                .OrderByDescending(s => s.Revenue)
                .Take(5)
                .ToList();

            return new OwnerDashboardMetricsDto
            {
                OwnerUserId = ownerUserId,
                WalletBalance = walletBalance,
                RevenueLast30Days = totalRevenue,
                TotalStations = stations.Count,
                BookingsLast30Days = totalBookings,
                CancelRateLast30Days = Math.Round(cancelRate, 2),
                ActiveTimeUtilizationRate = 0.35m, // Mock for demonstration
                StationPerformances = performances.OrderByDescending(p => p.TotalRevenue).ToList(),
                TopServicesSold = extraServicesSold
            };
        }
    }
}
