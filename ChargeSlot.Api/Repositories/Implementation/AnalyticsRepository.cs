using ChargeSlot.Api.Data;
using ChargeSlot.Api.DTOs.Admin;
using ChargeSlot.Api.DTOs.Analytics;
using ChargeSlot.Api.Enums;
using ChargeSlot.Api.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ChargeSlot.Api.Repositories.Implementation
{
    public class AnalyticsRepository : IAnalyticsRepository
    {
        private readonly ChargeSlotDbContext _db;

        public AnalyticsRepository(ChargeSlotDbContext db)
        {
            _db = db;
        }

        public async Task<AdminDashboardMetricsDto> GetAdminMetricsAsync(DateTime start, DateTime end)
        {
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
                SuspiciousNote = $"Tài khoản này đã hủy {h.Cancelled}/{h.Total} đơn đặt trong khoảng thời gian qua (Tỉ lệ {Math.Round((decimal)h.Cancelled/h.Total*100)}%)."
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

        public async Task<OwnerDashboardMetricsDto> GetOwnerMetricsAsync(int ownerUserId, DateTime start, DateTime end)
        {
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
                .Sum(b => b.TotalAmount);

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

        public async Task<RevenueSummaryDto> GetRevenueSummaryAsync(DateTime? since)
        {
            var invoiceQuery = _db.Invoices
                .Include(i => i.Booking)
                .Where(i => i.Status == InvoiceStatus.Confirmed || i.Status == InvoiceStatus.Resolved);

            if (since.HasValue)
                invoiceQuery = invoiceQuery.Where(i => i.CreatedAt >= since.Value);

            var invoiceAgg = await invoiceQuery
                .GroupBy(_ => 1)
                .Select(g => new
                {
                    TotalRevenue = g.Sum(i => i.TotalAmount),
                    PlatformFee = g.Sum(i => i.PlatformFee),
                    VatCollected = g.Sum(i => i.VatAmount)
                })
                .FirstOrDefaultAsync();

            var bookingQuery = _db.Bookings.AsQueryable();
            if (since.HasValue)
                bookingQuery = bookingQuery.Where(b => b.CreatedAt >= since.Value);

            var totalBookings = await bookingQuery.CountAsync();
            var completedBookings = await bookingQuery.CountAsync(b => b.Status == BookingStatus.Completed);
            var disputedBookings = await bookingQuery.CountAsync(b => b.Status == BookingStatus.Disputed);

            var totalStations = await _db.ChargingStations
                .CountAsync(s => s.ApprovalStatus == ApprovalStatus.Approved
                              && s.OperationalStatus == OperationalStatus.Active);

            var totalDrivers = await _db.Driver.CountAsync();

            return new RevenueSummaryDto
            {
                TotalRevenue = invoiceAgg?.TotalRevenue ?? 0,
                PlatformFee = invoiceAgg?.PlatformFee ?? 0,
                VatCollected = invoiceAgg?.VatCollected ?? 0,
                TotalBookings = totalBookings,
                CompletedBookings = completedBookings,
                DisputedBookings = disputedBookings,
                TotalStations = totalStations,
                TotalDrivers = totalDrivers
            };
        }

        public async Task<List<MonthlyRevenueDto>> GetMonthlyRevenueAsync(DateTime? since)
        {
            var query = _db.Invoices
                .Where(i => i.Status == InvoiceStatus.Confirmed || i.Status == InvoiceStatus.Resolved);

            if (since.HasValue)
                query = query.Where(i => i.CreatedAt >= since.Value);

            var rawMonthly = await query
                .GroupBy(i => new { i.CreatedAt.Year, i.CreatedAt.Month })
                .Select(g => new
                {
                    g.Key.Year,
                    g.Key.Month,
                    Revenue = g.Sum(i => i.TotalAmount),
                    Bookings = g.Count(),
                    PlatformFee = g.Sum(i => i.PlatformFee)
                })
                .OrderByDescending(m => m.Year).ThenByDescending(m => m.Month)
                .ToListAsync();

            return rawMonthly.Select(m => new MonthlyRevenueDto
            {
                Month = $"{m.Month:D2}/{m.Year}",
                Revenue = m.Revenue,
                Bookings = m.Bookings,
                PlatformFee = m.PlatformFee
            }).ToList();
        }

        public async Task<List<TopStationDto>> GetTopStationsAsync(DateTime? since, int limit)
        {
            var query = _db.Invoices
                .Include(i => i.Booking)
                    .ThenInclude(b => b.ChargingSlot)
                        .ThenInclude(s => s.ChargingStation)
                            .ThenInclude(st => st.Owner)
                                .ThenInclude(o => o.User)
                .Where(i => i.Status == InvoiceStatus.Confirmed || i.Status == InvoiceStatus.Resolved);

            if (since.HasValue)
                query = query.Where(i => i.CreatedAt >= since.Value);

            return await query
                .GroupBy(i => new
                {
                    StationId = i.Booking.ChargingSlot.ChargingStation.Id,
                    StationName = i.Booking.ChargingSlot.ChargingStation.Name,
                    OwnerName = i.Booking.ChargingSlot.ChargingStation.Owner.User.FullName
                })
                .Select(g => new TopStationDto
                {
                    Name = g.Key.StationName,
                    Owner = g.Key.OwnerName,
                    Revenue = g.Sum(i => i.TotalAmount),
                    Bookings = g.Count()
                })
                .OrderByDescending(t => t.Revenue)
                .Take(limit)
                .ToListAsync();
        }

        public async Task<List<RecentTransactionDto>> GetRecentTransactionsAsync(int limit)
        {
            var transactions = await _db.LedgerTransactions
                .Include(t => t.Entries)
                .OrderByDescending(t => t.CreatedAt)
                .Take(limit)
                .ToListAsync();

            return transactions.Select(t =>
            {
                var entry = t.Entries.FirstOrDefault();
                var amount = entry?.Amount ?? 0;

                if (t.ReferenceType.Contains("Refund", StringComparison.OrdinalIgnoreCase) || 
                    t.ReferenceType.Equals("Withdrawal", StringComparison.OrdinalIgnoreCase))
                {
                    amount = -amount;
                }

                return new RecentTransactionDto
                {
                    Id = t.Id,
                    Type = t.ReferenceType,
                    Memo = t.Memo,
                    Amount = amount,
                    Date = t.CreatedAt
                };
            }).ToList();
        }

        public async Task<List<VatReportDto>> GetVatReportAsync(DateTime? since)
        {
            var query = _db.Invoices
                .Include(i => i.Booking)
                    .ThenInclude(b => b.ChargingSlot)
                        .ThenInclude(s => s.ChargingStation)
                            .ThenInclude(st => st.Owner)
                                .ThenInclude(o => o.User)
                .Where(i => i.Status == InvoiceStatus.Confirmed || i.Status == InvoiceStatus.Resolved);

            if (since.HasValue)
                query = query.Where(i => i.CreatedAt >= since.Value);

            return await query
                .GroupBy(i => new
                {
                    OwnerUserId = i.Booking.ChargingSlot.ChargingStation.OwnerUserId,
                    OwnerName = i.Booking.ChargingSlot.ChargingStation.Owner.User.FullName,
                    BusinessName = i.Booking.ChargingSlot.ChargingStation.Owner.BusinessName,
                    TaxCode = i.Booking.ChargingSlot.ChargingStation.Owner.TaxCode
                })
                .Select(g => new VatReportDto
                {
                    OwnerUserId = g.Key.OwnerUserId,
                    OwnerName = g.Key.OwnerName,
                    BusinessName = g.Key.BusinessName,
                    TaxCode = g.Key.TaxCode,
                    TotalVat = g.Sum(i => i.VatAmount),
                    InvoiceCount = g.Count()
                })
                .OrderByDescending(v => v.TotalVat)
                .ToListAsync();
        }
    }
}
