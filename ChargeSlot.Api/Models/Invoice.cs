using ChargeSlot.Api.Enums;
using System;

namespace ChargeSlot.Api.Models
{
    public class Invoice
    {
        public Guid Id { get; set; }

        public Guid BookingId { get; set; }
        public Booking Booking { get; set; } = null!;

        public decimal TotalAmount { get; set; }
        public InvoiceStatus Status { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
