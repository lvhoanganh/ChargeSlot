using ChargeSlot.Api.Models;

namespace ChargeSlot.Api.Repositories.Interfaces
{
    public interface IInvoiceRepository
    {
        Task<Invoice?> GetByIdAsync(int id);
        Task<Invoice?> GetByBookingIdAsync(int bookingId);
        void Add(Invoice invoice);
        void Update(Invoice invoice);
        Task<List<int>> GetExpiredPendingConfirmIdsAsync(DateTime deadline);
        Task<Invoice?> GetByIdWithFullBookingDetailsAsync(int id);
        Task<List<Invoice>> GetPendingConfirmForReminderAsync(DateTime reminderStart, DateTime reminderEnd);
        Task MarkReminderSentAsync(int invoiceId, DateTime sentAt);
        Task<(List<Invoice> Items, int TotalCount)> GetAdminAllInvoicesAsync(ChargeSlot.Api.DTOs.Admin.Overview.InvoiceFilterDto filter);
    }
}

