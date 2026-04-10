using System;

namespace ChargeSlot.Api.DTOs.Admin.Overview
{
    public class PagedFilterDto
    {
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
    }
}
