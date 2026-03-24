using ChargeSlot.Api.Models;

namespace ChargeSlot.Api.Repositories.Interfaces
{
    public interface IDisputeRepository
    {
        Task<Dispute?> GetByIdAsync(int id);
        Task<Dispute?> GetByIdWithDetailsAsync(int id);
        Task<Dispute?> GetByBookingIdAsync(int bookingId);
        Task<List<Dispute>> GetPendingAsync();
        Task<Dispute> CreateAsync(Dispute dispute);
        Task UpdateAsync(Dispute dispute);
    }
}
