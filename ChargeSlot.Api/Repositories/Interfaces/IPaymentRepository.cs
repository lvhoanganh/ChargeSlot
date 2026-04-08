using ChargeSlot.Api.Models;

namespace ChargeSlot.Api.Repositories.Interfaces
{
    public interface IPaymentRepository
    {
        Task<Payment?> GetByBookingIdAsync(int bookingId);
        void Add(Payment payment);
        void Update(Payment payment);
    }
}

