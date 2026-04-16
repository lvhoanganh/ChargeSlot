using ChargeSlot.Api.Models;

namespace ChargeSlot.Api.Repositories.Interfaces
{
    public interface INotificationRepository
    {
        Task<List<Notification>> GetByUserAsync(int userId);
        Task<(List<Notification> Items, int TotalCount)> GetByUserPagedAsync(int userId, int page, int pageSize);
        Task<Notification?> GetByIdAsync(int id);
        void Add(Notification notification);
        void Update(Notification notification);
    }
}

