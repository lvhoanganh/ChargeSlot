using ChargeSlot.Api.Data;
using ChargeSlot.Api.Models;
using ChargeSlot.Api.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ChargeSlot.Api.Repositories.Implementation
{
    public class PaymentRepository : IPaymentRepository
    {
        private readonly ChargeSlotDbContext _db;

        public PaymentRepository(ChargeSlotDbContext db)
        {
            _db = db;
        }

        public async Task<Payment?> GetByBookingIdAsync(int bookingId)
        {
            return await _db.Payments
                .Include(p => p.Booking)
                .FirstOrDefaultAsync(p => p.BookingId == bookingId);
        }

        public void Add(Payment payment)
        {
            _db.Payments.Add(payment);
        }

        public void Update(Payment payment)
        {
            _db.Payments.Update(payment);
        }
    }
}

