using ChargeSlot.Api.Data;
using ChargeSlot.Api.Models;
using ChargeSlot.Api.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ChargeSlot.Api.Repositories.Implementation
{
    public class ChatRepository : IChatRepository
    {
        private readonly ChargeSlotDbContext _context;

        public ChatRepository(ChargeSlotDbContext context)
        {
            _context = context;
        }

        public async Task<ChatConversation?> GetConversationByIdAsync(int id)
        {
            return await _context.ChatConversations.FindAsync(id);
        }

        public async Task<List<ChatConversation>> GetConversationsAsync(int userId, int page, int pageSize)
        {
            return await _context.ChatConversations
                .Include(c => c.Booking).ThenInclude(b => b.ChargingSlot).ThenInclude(s => s.ChargingStation)
                .Include(c => c.Driver)
                .Include(c => c.Owner)
                .Where(c => c.DriverUserId == userId || c.OwnerUserId == userId)
                .OrderByDescending(c => c.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<ChatConversation?> GetConversationByBookingAsync(int bookingId)
        {
            return await _context.ChatConversations
                .FirstOrDefaultAsync(c => c.BookingId == bookingId);
        }

        public async Task<Dictionary<int, ChatMessage>> GetLastMessagesAsync(List<int> conversationIds)
        {
            return await _context.ChatMessages
                .Where(m => conversationIds.Contains(m.ConversationId))
                .GroupBy(m => m.ConversationId)
                .Select(g => g.OrderByDescending(m => m.CreatedAt).First())
                .ToDictionaryAsync(m => m.ConversationId);
        }

        public async Task<Dictionary<int, int>> GetUnreadCountsAsync(int userId, List<int> conversationIds)
        {
            return await _context.ChatMessages
                .Where(m => conversationIds.Contains(m.ConversationId)
                         && m.SenderUserId != userId
                         && !m.IsRead)
                .GroupBy(m => m.ConversationId)
                .Select(g => new { ConversationId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.ConversationId, x => x.Count);
        }

        public async Task<List<ChatMessage>> GetMessagesAsync(int conversationId, int page, int pageSize)
        {
            return await _context.ChatMessages
                .Include(m => m.Sender)
                .Where(m => m.ConversationId == conversationId)
                .OrderByDescending(m => m.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<int> GetMessagesCountAsync(int conversationId)
        {
            return await _context.ChatMessages
                .Where(m => m.ConversationId == conversationId)
                .CountAsync();
        }

        public async Task MarkMessagesAsReadAsync(int conversationId, int currentUserId)
        {
            var unreadMessages = await _context.ChatMessages
                .Where(m => m.ConversationId == conversationId
                         && m.SenderUserId != currentUserId
                         && !m.IsRead)
                .ToListAsync();

            foreach (var msg in unreadMessages)
                msg.IsRead = true;
        }

        public void AddConversation(ChatConversation conversation)
        {
            _context.ChatConversations.Add(conversation);
        }

        public void AddMessage(ChatMessage message)
        {
            _context.ChatMessages.Add(message);
        }
    }
}
