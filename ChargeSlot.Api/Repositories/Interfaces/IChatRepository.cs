using ChargeSlot.Api.Models;

namespace ChargeSlot.Api.Repositories.Interfaces
{
    public interface IChatRepository
    {
        Task<ChatConversation?> GetConversationByIdAsync(int id);
        Task<List<ChatConversation>> GetConversationsAsync(int userId, int page, int pageSize);
        Task<ChatConversation?> GetConversationByBookingAsync(int bookingId);
        Task<Dictionary<int, ChatMessage>> GetLastMessagesAsync(List<int> conversationIds);
        Task<Dictionary<int, int>> GetUnreadCountsAsync(int userId, List<int> conversationIds);
        Task<List<ChatMessage>> GetMessagesAsync(int conversationId, int page, int pageSize);
        Task<int> GetMessagesCountAsync(int conversationId);
        Task MarkMessagesAsReadAsync(int conversationId, int currentUserId);
        void AddConversation(ChatConversation conversation);
        void AddMessage(ChatMessage message);
    }
}
