using System.Security.Claims;
using ChargeSlot.Api.DTOs.Chat;
using ChargeSlot.Api.Helpers;
using ChargeSlot.Api.Models;
using ChargeSlot.Api.Repositories.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace ChargeSlot.Api.Hubs
{
    /// <summary>
    /// SignalR Hub cho chat real-time giữa Driver và Owner.
    /// Client kết nối: /hubs/chat?access_token=xxx
    /// </summary>
    [Authorize]
    public class ChatHub : Hub
    {
        private readonly IChatRepository _chatRepo;
        private readonly IUserRepository _userRepo;
        private readonly IUnitOfWork _unitOfWork;

        public ChatHub(IChatRepository chatRepo, IUserRepository userRepo, IUnitOfWork unitOfWork)
        {
            _chatRepo = chatRepo;
            _userRepo = userRepo;
            _unitOfWork = unitOfWork;
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
            var conv = await _chatRepo.GetConversationByIdAsync(conversationId)
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
            var conv = await _chatRepo.GetConversationByIdAsync(conversationId)
                ?? throw new HubException("Conversation không tồn tại.");

            if (conv.DriverUserId != userId && conv.OwnerUserId != userId)
                throw new HubException("Bạn không có quyền gửi tin nhắn trong conversation này.");

            var senderName = await _userRepo.GetFullNameAsync(userId) ?? "Unknown";

            var message = new ChatMessage
            {
                ConversationId = conversationId,
                SenderUserId = userId,
                Content = content.Trim(),
                IsRead = false,
                CreatedAt = DateTimeHelper.VietnamNow()
            };

            _chatRepo.AddMessage(message);
            await _unitOfWork.CompleteAsync();

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
            var conv = await _chatRepo.GetConversationByIdAsync(conversationId)
                ?? throw new HubException("Conversation không tồn tại.");

            if (conv.DriverUserId != userId && conv.OwnerUserId != userId)
                throw new HubException("Bạn không có quyền.");

            // Đánh dấu tin nhắn của người kia là đã đọc
            await _chatRepo.MarkMessagesAsReadAsync(conversationId, userId);
            await _unitOfWork.CompleteAsync();

            // Notify group rằng tin nhắn đã đọc
            await Clients.Group($"chat_{conversationId}")
                .SendAsync("MessagesRead", conversationId, userId);
        }
    }
}
