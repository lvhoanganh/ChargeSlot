using ChargeSlot.Api.Enums;
using ChargeSlot.Api.DTOs.Notification;

namespace ChargeSlot.Api.Services.Interfaces
{
    public interface INotificationService
    {
        Task SendAsync(int userId, string title, string content, NotificationType type);
        Task<ChargeSlot.Api.DTOs.PagedResultDto<NotificationDto>> GetByUserAsync(int userId, int page, int pageSize);
        Task<int> GetTotalCountAsync(int userId);
        Task<int> GetUnreadCountAsync(int userId);
        Task MarkAsReadAsync(int userId, int notificationId);
    }
}
