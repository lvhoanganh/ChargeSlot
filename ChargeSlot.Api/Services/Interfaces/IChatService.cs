using ChargeSlot.Api.DTOs.Chat;

namespace ChargeSlot.Api.Services.Interfaces
{
    public interface IChatService
    {
        Task<object> GetConversationsAsync(int userId, int page, int pageSize);
        Task<object> GetMessagesAsync(int userId, int bookingId, int page, int pageSize);
        Task<ChatMessageDto> SendMessageAsync(int userId, int bookingId, SendMessageDto dto);
    }
}
