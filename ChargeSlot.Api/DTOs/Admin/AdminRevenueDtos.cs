namespace ChargeSlot.Api.DTOs.Admin
{
    public class RevenueSummaryDto
    {
        public decimal TotalRevenue { get; set; }
        public decimal PlatformFee { get; set; }
        public decimal VatCollected { get; set; }
        public int TotalBookings { get; set; }
        public int CompletedBookings { get; set; }
        public int DisputedBookings { get; set; }
        public int TotalStations { get; set; }
        public int TotalDrivers { get; set; }
    }

    public class MonthlyRevenueDto
    {
        public string Month { get; set; } = null!;
        public decimal Revenue { get; set; }
        public int Bookings { get; set; }
        public decimal PlatformFee { get; set; }
    }

    public class TopStationDto
    {
        public string Name { get; set; } = null!;
        public string Owner { get; set; } = null!;
        public decimal Revenue { get; set; }
        public int Bookings { get; set; }
    }

    public class RecentTransactionDto
    {
        public long Id { get; set; }
        public string Type { get; set; } = null!;
        public string? Memo { get; set; }
        public decimal Amount { get; set; }
        public DateTime Date { get; set; }
    }

    public class VatReportDto
    {
        public int OwnerUserId { get; set; }
        public string OwnerName { get; set; } = null!;
        public string BusinessName { get; set; } = null!;
        public string TaxCode { get; set; } = null!;
        public decimal TotalVat { get; set; }
        public int InvoiceCount { get; set; }
    }
}
