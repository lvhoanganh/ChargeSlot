using ChargeSlot.Api.Enums;

namespace ChargeSlot.Api.Services.Interfaces
{
    public interface INotificationService
    {
        Task SendAsync(int userId, string title, string content, NotificationType type);
    }
}
