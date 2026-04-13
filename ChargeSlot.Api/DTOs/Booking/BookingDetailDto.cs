using System;
using System.Collections.Generic;

namespace ChargeSlot.Api.DTOs.Booking
{
    public class BookingDetailDto : BookingDto
    {
        public BookingPaymentDetailDto? PaymentDetail { get; set; }
        public BookingSessionDetailDto? SessionDetail { get; set; }
        public BookingDisputeDetailDto? DisputeDetail { get; set; }
        // Invoice if available
        public BookingInvoiceDetailDto? InvoiceDetail { get; set; }
    }

    public class BookingPaymentDetailDto
    {
        public string Method { get; set; } = null!;
        public string Status { get; set; } = null!;
        public DateTime? PaidAt { get; set; }
        public string? GatewayTxnRef { get; set; }
        public decimal Amount { get; set; }
    }

    public class BookingSessionDetailDto
    {
        public DateTime? CheckinTime { get; set; }
        public DateTime? ActualStartTime { get; set; }
        public DateTime? ActualEndTime { get; set; }
        public decimal? ActualDurationHours { get; set; }

        /// <summary>Thời gian sạc thực tế (phút) — tính từ ActualStartTime đến ActualEndTime.</summary>
        public int? ActualDurationMinutes { get; set; }
    }

    public class BookingDisputeDetailDto
    {
        public int Id { get; set; }
        public string Reason { get; set; } = null!;
        public string Status { get; set; } = null!;
        public string? Description { get; set; }
        public DateTime? ResolvedAt { get; set; }
        public string? AdminNote { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class BookingInvoiceDetailDto
    {
        public int Id { get; set; }
        public decimal ChargingAmount { get; set; }
        public decimal ServiceAmount { get; set; }
        public decimal VatAmount { get; set; }
        public decimal PlatformFee { get; set; }
        public decimal TotalAmount { get; set; }
        public string Status { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
    }
}
