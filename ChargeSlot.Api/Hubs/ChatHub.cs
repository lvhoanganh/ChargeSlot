using System.Security.Claims;
using ChargeSlot.Api.Data;
using ChargeSlot.Api.DTOs.Chat;
using ChargeSlot.Api.Helpers;
using ChargeSlot.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace ChargeSlot.Api.Hubs
{
    /// <summary>
    /// SignalR Hub cho chat real-time giữa Driver và Owner.
    /// Client kết nối: /hubs/chat?access_token=xxx
    /// </summary>
    [Authorize]
    public class ChatHub : Hub
    {
        private readonly ChargeSlotDbContext _db;

        public ChatHub(ChargeSlotDbContext db)
        {
            _db = db;
        }

        private int GetUserId()
        {
            var id = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? throw new HubException("Unauthorized");
            return int.Parse(id);
        }

        /// <summary>Join conversation group để nhận tin nhắn real-time.</summary>
        public async Task JoinConversation(int conversationId)
        {
            var userId = GetUserId();
            var conv = await _db.ChatConversations.FindAsync(conversationId)
                ?? throw new HubException("Conversation không tồn tại.");

            // Chỉ cho phép Driver hoặc Owner trong conversation
            if (conv.DriverUserId != userId && conv.OwnerUserId != userId)
                throw new HubException("Bạn không có quyền truy cập conversation này.");

            await Groups.AddToGroupAsync(Context.ConnectionId, $"chat_{conversationId}");
        }

        /// <summary>Rời conversation group.</summary>
        public async Task LeaveConversation(int conversationId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"chat_{conversationId}");
        }

        /// <summary>Gửi tin nhắn → lưu DB → broadcast to group.</summary>
        public async Task SendMessage(int conversationId, string content)
        {
            if (string.IsNullOrWhiteSpace(content) || content.Length > 1000)
                throw new HubException("Nội dung tin nhắn không hợp lệ (1-1000 ký tự).");

            var userId = GetUserId();
            var conv = await _db.ChatConversations.FindAsync(conversationId)
                ?? throw new HubException("Conversation không tồn tại.");

            if (conv.DriverUserId != userId && conv.OwnerUserId != userId)
                throw new HubException("Bạn không có quyền gửi tin nhắn trong conversation này.");

            var senderName = await _db.Users
                .Where(u => u.Id == userId)
                .Select(u => u.FullName)
                .FirstOrDefaultAsync() ?? "Unknown";

            var message = new ChatMessage
            {
                ConversationId = conversationId,
                SenderUserId = userId,
                Content = content.Trim(),
                IsRead = false,
                CreatedAt = DateTimeHelper.VietnamNow()
            };

            _db.ChatMessages.Add(message);
            await _db.SaveChangesAsync();

            var dto = new ChatMessageDto
            {
                Id = message.Id,
                SenderUserId = userId,
                SenderName = senderName,
                Content = message.Content,
                IsRead = false,
                CreatedAt = message.CreatedAt
            };

            await Clients.Group($"chat_{conversationId}").SendAsync("ReceiveMessage", dto);
        }

        /// <summary>Đánh dấu tất cả tin nhắn trong conversation là đã đọc.</summary>
        public async Task MarkAsRead(int conversationId)
        {
            var userId = GetUserId();
            var conv = await _db.ChatConversations.FindAsync(conversationId)
                ?? throw new HubException("Conversation không tồn tại.");

            if (conv.DriverUserId != userId && conv.OwnerUserId != userId)
                throw new HubException("Bạn không có quyền.");

            // Đánh dấu tin nhắn của người kia là đã đọc
            var unreadMessages = await _db.ChatMessages
                .Where(m => m.ConversationId == conversationId
                         && m.SenderUserId != userId
                         && !m.IsRead)
                .ToListAsync();

            foreach (var msg in unreadMessages)
                msg.IsRead = true;

            await _db.SaveChangesAsync();

            // Notify group rằng tin nhắn đã đọc
            await Clients.Group($"chat_{conversationId}")
                .SendAsync("MessagesRead", conversationId, userId);
        }
    }
}
