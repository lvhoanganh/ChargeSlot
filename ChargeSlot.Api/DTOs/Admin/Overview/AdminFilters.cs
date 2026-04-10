using ChargeSlot.Api.Enums;
using System.Collections.Generic;

namespace ChargeSlot.Api.DTOs.Admin.Overview
{
    public class BookingFilterDto : PagedFilterDto
    {
        public string? Status { get; set; }
        public int? DriverUserId { get; set; }
        public int? OwnerUserId { get; set; }
        public int? StationId { get; set; }
    }

    public class SessionFilterDto : PagedFilterDto
    {
        public string? Status { get; set; }
        public int? BookingId { get; set; }
    }

    public class InvoiceFilterDto : PagedFilterDto
    {
        public string? Status { get; set; }
        public bool? IsPaid { get; set; }
    }

    public class WalletFilterDto : PagedFilterDto
    {
        public string? WalletType { get; set; }
        public int? UserId { get; set; }
        public string? SystemCode { get; set; }
    }

    public class TransactionFilterDto : PagedFilterDto
    {
        public string? TransactionType { get; set; } // Credit, Debit
    }

    public class PagedResultDto<T>
    {
        public List<T> Items { get; set; } = new List<T>();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages => PageSize > 0 ? (int)System.Math.Ceiling(TotalCount / (double)PageSize) : 0;
    }
}
