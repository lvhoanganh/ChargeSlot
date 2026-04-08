using ChargeSlot.Api.Data;
using ChargeSlot.Api.Models;
using ChargeSlot.Api.Enums;
using ChargeSlot.Api.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using ChargeSlot.Api.Helpers;
namespace ChargeSlot.Api.Repositories.Implementation
{
    public class InvoiceRepository : IInvoiceRepository
    {
        private readonly ChargeSlotDbContext _db;

        public InvoiceRepository(ChargeSlotDbContext db)
        {
            _db = db;
        }

        public async Task<Invoice?> GetByIdAsync(int id)
        {
            return await _db.Invoices
                .Include(i => i.Booking)
                .FirstOrDefaultAsync(i => i.Id == id);
        }

        public async Task<Invoice?> GetByBookingIdAsync(int bookingId)
        {
            return await _db.Invoices
                .Include(i => i.Booking)
                .FirstOrDefaultAsync(i => i.BookingId == bookingId);
        }

        public void Add(Invoice invoice)
        {
            _db.Invoices.Add(invoice);
        }

        public void Update(Invoice invoice)
        {
            invoice.UpdatedAt = DateTimeHelper.VietnamNow();
            _db.Invoices.Update(invoice);
        }


        public async Task<List<int>> GetExpiredPendingConfirmIdsAsync(DateTime deadline)
        {
            return await _db.Invoices
                .Where(i => i.Status == InvoiceStatus.PendingConfirm && i.CreatedAt <= deadline)
                .Select(i => i.Id)
                .ToListAsync();
        }

        public async Task<Invoice?> GetByIdWithFullBookingDetailsAsync(int id)
        {
            return await _db.Invoices
                .Include(i => i.Booking)
                    .ThenInclude(b => b.ChargingSlot).ThenInclude(s => s.ChargingStation)
                .Include(i => i.Booking)
                    .ThenInclude(b => b.Driver)
                .FirstOrDefaultAsync(i => i.Id == id);
        }

        public async Task<List<Invoice>> GetPendingConfirmForReminderAsync(DateTime reminderStart, DateTime reminderEnd)
        {
            return await _db.Invoices
                .Include(i => i.Booking).ThenInclude(b => b.ChargingSlot).ThenInclude(s => s.ChargingStation)
                .Where(i => i.Status == InvoiceStatus.PendingConfirm
                         && i.CreatedAt > reminderStart
                         && i.CreatedAt <= reminderEnd
                         && !i.ReminderSentAt.HasValue)
                .ToListAsync();
        }

        public async Task MarkReminderSentAsync(int invoiceId, DateTime sentAt)
        {
            var invoice = await _db.Invoices.FindAsync(invoiceId);
            if (invoice != null)
            {
                invoice.ReminderSentAt = sentAt;
            }
        }

        public async Task<(List<Invoice> Items, int TotalCount)> GetAdminAllInvoicesAsync(ChargeSlot.Api.DTOs.Admin.Overview.InvoiceFilterDto filter)
        {
            IQueryable<Invoice> query = _db.Invoices.AsNoTracking();

            if (!string.IsNullOrEmpty(filter.Status))
            {
                if (Enum.TryParse<InvoiceStatus>(filter.Status, true, out var statusEnum))
                {
                    query = query.Where(i => i.Status == statusEnum);
                }
            }

            if (filter.IsPaid.HasValue)
            {
                if (filter.IsPaid.Value)
                    query = query.Where(i => i.Status == InvoiceStatus.Confirmed);
                else
                    query = query.Where(i => i.Status != InvoiceStatus.Confirmed);
            }

            if (filter.FromDate.HasValue)
            {
                query = query.Where(i => i.CreatedAt >= filter.FromDate.Value);
            }
            if (filter.ToDate.HasValue)
            {
                query = query.Where(i => i.CreatedAt <= filter.ToDate.Value);
            }

            int totalCount = await query.CountAsync();

            var items = await query
                .OrderByDescending(i => i.CreatedAt)
                .Skip((filter.Page - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .ToListAsync();

            return (items, totalCount);
        }
    }
}

