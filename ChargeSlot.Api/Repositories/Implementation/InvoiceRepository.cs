using ChargeSlot.Api.Data;
using ChargeSlot.Api.Models;
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

        public async Task<Invoice> CreateAsync(Invoice invoice)
        {
            _db.Invoices.Add(invoice);
            await _db.SaveChangesAsync();
            return invoice;
        }

        public async Task UpdateAsync(Invoice invoice)
        {
            invoice.UpdatedAt = DateTimeHelper.VietnamNow();
            _db.Invoices.Update(invoice);
            await _db.SaveChangesAsync();
        }
    }
}
