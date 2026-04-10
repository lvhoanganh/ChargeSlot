using ChargeSlot.Api.Models;

namespace ChargeSlot.Api.Repositories.Interfaces
{
    public interface IDisputeRepository
    {
        Task<Dispute?> GetByIdAsync(int id);
        Task<Dispute?> GetByIdWithDetailsAsync(int id);
        Task<Dispute?> GetByBookingIdAsync(int bookingId);
        Task<List<Dispute>> GetPendingAsync();
        Task<int> GetDisputeCountByDriverInMonthAsync(int driverUserId, DateTime monthStart);
        Task<int> GetDriverLoseCountInMonthAsync(int driverUserId, DateTime monthStart);
        Task<int> GetStationLoseCountInMonthAsync(int stationId, DateTime monthStart);
        Task<bool> HasDisputeForBookingAsync(int bookingId);
        Task<List<Dispute>> GetAllAsync(string? status = null);
        Task<List<Dispute>> GetByDriverAsync(int driverUserId);
        Task<List<Dispute>> GetByOwnerAsync(int ownerUserId);
        void Add(Dispute dispute);
        void Update(Dispute dispute);
        Task<List<int>> GetExpiredOwnerEvidenceIdsAsync(DateTime now);
        Task<List<int>> GetExpiredAdminReviewIdsAsync(DateTime now);
        Task<Dispute?> GetByIdWithBookingAndInvoiceDetailsAsync(int id);
        Task<List<Dispute>> GetOwnerEvidenceForReminderAsync(DateTime now, DateTime cutoff);
        Task<List<Dispute>> GetAdminReviewForReminderAsync(DateTime now, DateTime cutoff);
        Task MarkOwnerReminderSentAsync(int disputeId, DateTime sentAt);
        Task MarkAdminReminderSentAsync(int disputeId, DateTime sentAt);
    }
}
