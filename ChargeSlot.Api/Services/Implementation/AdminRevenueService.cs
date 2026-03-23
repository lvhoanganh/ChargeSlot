using ChargeSlot.Api.Data;
using ChargeSlot.Api.DTOs.Admin;
using ChargeSlot.Api.Enums;
using ChargeSlot.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ChargeSlot.Api.Services.Implementation
{
    public class AdminRevenueService : IAdminRevenueService
    {
        private readonly ChargeSlotDbContext _db;

        public AdminRevenueService(ChargeSlotDbContext db)
        {
            _db = db;
        }

        public async Task<RevenueSummaryDto> GetSummaryAsync(string period)
        {
            var since = GetSinceDate(period);

            // Invoice-based metrics (only confirmed invoices)
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

            // Booking counts
            var bookingQuery = _db.Bookings.AsQueryable();
            if (since.HasValue)
                bookingQuery = bookingQuery.Where(b => b.CreatedAt >= since.Value);

            var totalBookings = await bookingQuery.CountAsync();
            var completedBookings = await bookingQuery.CountAsync(b => b.Status == BookingStatus.Completed);
            var disputedBookings = await bookingQuery.CountAsync(b => b.Status == BookingStatus.Disputed);

            // Active stations (Approved + Active)
            var totalStations = await _db.ChargingStations
                .CountAsync(s => s.ApprovalStatus == ApprovalStatus.Approved
                              && s.OperationalStatus == OperationalStatus.Active);

            // Total drivers
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

        public async Task<List<MonthlyRevenueDto>> GetMonthlyRevenueAsync(string period)
        {
            var since = GetSinceDate(period);

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

            var monthly = rawMonthly.Select(m => new MonthlyRevenueDto
            {
                Month = $"{m.Month:D2}/{m.Year}",
                Revenue = m.Revenue,
                Bookings = m.Bookings,
                PlatformFee = m.PlatformFee
            }).ToList();

            return monthly;
        }

        public async Task<List<TopStationDto>> GetTopStationsAsync(string period, int limit)
        {
            var since = GetSinceDate(period);

            var query = _db.Invoices
                .Include(i => i.Booking)
                    .ThenInclude(b => b.ChargingSlot)
                        .ThenInclude(s => s.ChargingStation)
                            .ThenInclude(st => st.Owner)
                                .ThenInclude(o => o.User)
                .Where(i => i.Status == InvoiceStatus.Confirmed || i.Status == InvoiceStatus.Resolved);

            if (since.HasValue)
                query = query.Where(i => i.CreatedAt >= since.Value);

            var topStations = await query
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

            return topStations;
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
                // Amount = Credit entry amount (positive), negative for refunds
                var creditEntry = t.Entries.FirstOrDefault(e => e.Direction == LedgerDirection.Credit);
                var amount = creditEntry?.Amount ?? 0;

                // If refund type, show as negative
                if (t.ReferenceType.Contains("Refund", StringComparison.OrdinalIgnoreCase))
                    amount = -amount;

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

        public async Task<List<VatReportDto>> GetVatReportAsync(string period)
        {
            var since = GetSinceDate(period);

            var query = _db.Invoices
                .Include(i => i.Booking)
                    .ThenInclude(b => b.ChargingSlot)
                        .ThenInclude(s => s.ChargingStation)
                            .ThenInclude(st => st.Owner)
                                .ThenInclude(o => o.User)
                .Where(i => i.Status == InvoiceStatus.Confirmed || i.Status == InvoiceStatus.Resolved);

            if (since.HasValue)
                query = query.Where(i => i.CreatedAt >= since.Value);

            var vatReport = await query
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

            return vatReport;
        }

        /// <summary>
        /// Convert period string to DateTime filter.
        /// month = last 30 days, quarter = last 90 days, year = last 365 days, all = null.
        /// </summary>
        private static DateTime? GetSinceDate(string period)
        {
            var now = DateTime.UtcNow;
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
