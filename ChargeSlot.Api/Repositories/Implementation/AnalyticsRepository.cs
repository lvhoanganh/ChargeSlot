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
            // Wallet balances - single queries
            var escrowBal = await _db.Wallets.Where(w => w.SystemCode == "ESCROW").Select(w => w.AvailableBalance).FirstOrDefaultAsync();
            var platformBal = await _db.Wallets.Where(w => w.SystemCode == "PLATFORM_REVENUE").Select(w => w.AvailableBalance).FirstOrDefaultAsync();
            
            // Counts - DB level
            var totalStations = await _db.ChargingStations.CountAsync();
            var activeStations = await _db.ChargingStations.CountAsync(s => s.OperationalStatus == OperationalStatus.Active);
            var totalUsers = await _db.Users.CountAsync();

            // Booking statistics - DB level aggregation (no ToListAsync)
            var bookingQuery = _db.Bookings.Where(b => b.CreatedAt >= start && b.CreatedAt <= end);
            var totalBookings = await bookingQuery.CountAsync();
            var cancelledBookings = await bookingQuery.CountAsync(b => b.Status == BookingStatus.Cancelled);
            var noShowBookings = await bookingQuery.CountAsync(b => b.Status == BookingStatus.NoShow);
            decimal cancelRate = totalBookings > 0 ? (decimal)cancelledBookings / totalBookings : 0m;

            // Disputes - count at DB level
            var disputesCount = await _db.Disputes
                .Where(d => d.CreatedAt >= start && d.CreatedAt <= end)
                .CountAsync();

            // Top disputed stations - DB level aggregation
            var topDisputed = await _db.Disputes
                .Where(d => d.CreatedAt >= start && d.CreatedAt <= end)
                .Where(d => d.Booking.ChargingSlot.ChargingStation != null)
                .GroupBy(d => new { d.Booking.ChargingSlot.StationId, d.Booking.ChargingSlot.ChargingStation.Name })
                .Select(g => new StationDisputeSummaryDto
                {
                    StationId = g.Key.StationId,
                    StationName = g.Key.Name,
                    DisputeCount = g.Count()
                })
                .OrderByDescending(x => x.DisputeCount)
                .Take(5)
                .ToListAsync();

            // High risk drivers - DB level aggregation
            var highRiskRaw = await _db.Bookings
                .Where(b => b.CreatedAt >= start && b.CreatedAt <= end)
                .GroupBy(b => b.DriverUserId)
                .Select(g => new
                {
                    DriverId = g.Key,
                    Total = g.Count(),
                    Cancelled = g.Count(b => b.Status == BookingStatus.Cancelled)
                })
                .Where(x => x.Total >= 5 && x.Cancelled >= x.Total / 2)
                .OrderByDescending(x => x.Cancelled)
                .Take(5)
                .ToListAsync();

            var driverNames = await _db.Users
                .Where(u => highRiskRaw.Select(h => h.DriverId).Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.FullName ?? "Unknown");

            var driverRiskDtos = highRiskRaw.Select(h => new DriverCollusionRiskDto
            {
                DriverUserId = h.DriverId,
                DriverName = driverNames.GetValueOrDefault(h.DriverId, "Unknown Driver"),
                TotalBookings = h.Total,
                CancelledBookings = h.Cancelled,
                SuspiciousNote = $"Tài khoản này đã hủy {h.Cancelled}/{h.Total} đơn đặt trong khoảng thời gian qua (Tỉ lệ {Math.Round((decimal)h.Cancelled/h.Total*100)}%)."
            }).ToList();

            // Withdraw statistics
            var pendingWithdrawQuery = _db.WithdrawRequests
                .Where(w => w.Status == WithdrawStatus.Pending || w.Status == WithdrawStatus.Approved);
            var pendingWithdrawCount = await pendingWithdrawQuery.CountAsync();
            var pendingWithdrawAmount = await pendingWithdrawQuery.SumAsync(w => w.Amount);

            var completedWithdrawAmount = await _db.WithdrawRequests
                .Where(w => w.Status == WithdrawStatus.Completed && w.RequestedAt >= start && w.RequestedAt <= end)
                .SumAsync(w => w.Amount);

            return new AdminDashboardMetricsDto
            {
                TotalEscrowBalance = escrowBal,
                TotalPlatformRevenue = platformBal,
                TotalStations = totalStations,
                TotalActiveStations = activeStations,
                TotalUsers = totalUsers,
                BookingsLast30Days = totalBookings,
                CancelRateLast30Days = cancelRate,
                DisputesLast30Days = disputesCount,
                NoShowLast30Days = noShowBookings,
                TopDisputedStations = topDisputed,
                HighRiskDrivers = driverRiskDtos,
                PendingWithdrawCount = pendingWithdrawCount,
                PendingWithdrawAmount = pendingWithdrawAmount,
                CompletedWithdrawAmount = completedWithdrawAmount
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
                .AsNoTracking()
                .ToListAsync();

            var stationIds = stations.Select(s => s.Id).ToList();

            // DB-level aggregation for booking stats
            var bookingQuery = _db.Bookings
                .Where(b => stationIds.Contains(b.ChargingSlot.StationId) && b.CreatedAt >= start && b.CreatedAt <= end);

            var totalBookings = await bookingQuery.CountAsync();
            var cancelledBookings = await bookingQuery.CountAsync(b => b.Status == BookingStatus.Cancelled);
            var completedBookings = await bookingQuery.CountAsync(b => b.Status == BookingStatus.Completed);
            var noShowBookings = await bookingQuery.CountAsync(b => b.Status == BookingStatus.NoShow);
            decimal cancelRate = totalBookings > 0 ? (decimal)cancelledBookings / totalBookings : 0m;

            // Revenue from confirmed/resolved invoices (accurate, not from booking amounts)
            var totalRevenue = await _db.Invoices
                .Where(i => (i.Status == InvoiceStatus.Confirmed || i.Status == InvoiceStatus.Resolved)
                         && stationIds.Contains(i.Booking.ChargingSlot.StationId)
                         && i.CreatedAt >= start && i.CreatedAt <= end)
                .SumAsync(i => i.TotalAmount - i.PlatformFee - i.VatAmount);

            // Station performances - DB level
            var performances = new List<StationPerformanceDto>();
            foreach (var s in stations)
            {
                var sBookingCount = await _db.Bookings
                    .Where(b => b.ChargingSlot.StationId == s.Id && b.CreatedAt >= start && b.CreatedAt <= end)
                    .CountAsync();

                var sRevenue = await _db.Invoices
                    .Where(i => (i.Status == InvoiceStatus.Confirmed || i.Status == InvoiceStatus.Resolved)
                             && i.Booking.ChargingSlot.StationId == s.Id
                             && i.CreatedAt >= start && i.CreatedAt <= end)
                    .SumAsync(i => i.TotalAmount - i.PlatformFee - i.VatAmount);

                performances.Add(new StationPerformanceDto
                {
                    StationId = s.Id,
                    StationName = s.Name,
                    TotalBookings = sBookingCount,
                    TotalRevenue = sRevenue,
                    AverageRating = s.AverageRating
                });
            }

            // Top selling extra services
            var extraServicesSold = await _db.BookingExtraServices
                .Include(es => es.ExtraService)
                .Where(es => stationIds.Contains(es.Booking.ChargingSlot.StationId)
                          && (es.Booking.Status == BookingStatus.Completed || es.Booking.Status == BookingStatus.Paid)
                          && es.Booking.CreatedAt >= start && es.Booking.CreatedAt <= end)
                .GroupBy(es => es.ExtraService.ServiceName)
                .Select(g => new ServiceSalesDto
                {
                    ServiceName = g.Key ?? "Dịch vụ khác",
                    QuantitySold = g.Sum(es => es.Quantity),
                    Revenue = g.Sum(es => es.TotalPrice)
                })
                .OrderByDescending(s => s.Revenue)
                .Take(5)
                .ToListAsync();

            return new OwnerDashboardMetricsDto
            {
                OwnerUserId = ownerUserId,
                WalletBalance = walletBalance,
                RevenueLast30Days = totalRevenue,
                TotalStations = stations.Count,
                BookingsLast30Days = totalBookings,
                CancelRateLast30Days = Math.Round(cancelRate, 2),
                CompletedBookingsLast30Days = completedBookings,
                NoShowLast30Days = noShowBookings,
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
