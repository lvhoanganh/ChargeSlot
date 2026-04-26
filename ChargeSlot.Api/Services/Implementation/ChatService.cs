using ChargeSlot.Api.DTOs.Chat;
using ChargeSlot.Api.Enums;
using ChargeSlot.Api.Models;
using ChargeSlot.Api.Repositories.Interfaces;
using ChargeSlot.Api.Services.Interfaces;
using ChargeSlot.Api.Helpers;
using Microsoft.AspNetCore.SignalR;

namespace ChargeSlot.Api.Services.Implementation
{
    public class ChatService : IChatService
    {
        private readonly IChatRepository _chatRepo;
        private readonly IBookingRepository _bookingRepo;
        private readonly IUserRepository _userRepo;
        private readonly IUnitOfWork _unitOfWork;
        private readonly Microsoft.AspNetCore.SignalR.IHubContext<ChargeSlot.Api.Hubs.ChatHub> _hubContext;

        public ChatService(
            IChatRepository chatRepo,
            IBookingRepository bookingRepo,
            IUserRepository userRepo,
            IUnitOfWork unitOfWork,
            Microsoft.AspNetCore.SignalR.IHubContext<ChargeSlot.Api.Hubs.ChatHub> hubContext)
        {
            _chatRepo = chatRepo;
            _bookingRepo = bookingRepo;
            _userRepo = userRepo;
            _unitOfWork = unitOfWork;
            _hubContext = hubContext;
        }

        public async Task<object> GetConversationsAsync(int userId, int page, int pageSize)
        {
            var conversations = await _chatRepo.GetConversationsAsync(userId, page, pageSize);

            if (!conversations.Any())
                return new { total = 0, page, pageSize, items = new List<ChatConversationDto>() };

            var convIds = conversations.Select(c => c.Id).ToList();

            var lastMessages = await _chatRepo.GetLastMessagesAsync(convIds);
            var unreadCounts = await _chatRepo.GetUnreadCountsAsync(userId, convIds);

            var allItems = conversations.Select(conv =>
            {
                var isDriver = conv.DriverUserId == userId;
                var otherUser = isDriver ? conv.Owner : conv.Driver;
                lastMessages.TryGetValue(conv.Id, out var lastMsg);
                unreadCounts.TryGetValue(conv.Id, out var unread);

                return new ChatConversationDto
                {
                    Id = conv.Id,
                    BookingId = conv.BookingId,
                    StationName = conv.Booking?.ChargingSlot?.ChargingStation?.Name ?? "",
                    OtherUserName = otherUser?.FullName ?? "",
                    OtherUserId = isDriver ? conv.OwnerUserId : conv.DriverUserId,
                    LastMessage = lastMsg?.Content,
                    LastMessageAt = lastMsg?.CreatedAt,
                    UnreadCount = unread
                };
            }).ToList();

            var total = allItems.Count; // This doesn't represent true total out of all pages, but matches legacy behavior
            return new { total, page, pageSize, items = allItems };
        }

        public async Task<object> GetMessagesAsync(int userId, int bookingId, int page, int pageSize)
        {
            var conv = await _chatRepo.GetConversationByBookingAsync(bookingId);

            if (conv == null)
                return new { conversationId = (int?)null, total = 0, page, pageSize, messages = new List<ChatMessageDto>() };

            if (conv.DriverUserId != userId && conv.OwnerUserId != userId)
                throw new UnauthorizedAccessException();

            var total = await _chatRepo.GetMessagesCountAsync(conv.Id);
            var messages = await _chatRepo.GetMessagesAsync(conv.Id, page, pageSize);

            var messageDtos = messages.Select(m => new ChatMessageDto
            {
                Id = m.Id,
                SenderUserId = m.SenderUserId,
                SenderName = m.Sender?.FullName ?? "",
                Content = m.Content ?? "",
                IsRead = m.IsRead,
                CreatedAt = m.CreatedAt
            }).ToList();

            messageDtos.Reverse(); // Match legacy logic to show oldest first within the page block

            return new { conversationId = conv.Id, total, page, pageSize, messages = messageDtos };
        }

        public async Task<ChatMessageDto> SendMessageAsync(int userId, int bookingId, SendMessageDto dto)
        {
            var booking = await _bookingRepo.GetByIdWithDetailsAsync(bookingId);

            if (booking == null)
                throw new KeyNotFoundException("Booking không tồn tại.");

            var ownerUserId = booking.ChargingSlot!.ChargingStation!.OwnerUserId;
            var driverUserId = booking.DriverUserId;

            if (userId != driverUserId && userId != ownerUserId)
                throw new UnauthorizedAccessException();

            // Chỉ cho phép chat khi booking đang trong giai đoạn hoạt động
            var chattableStatuses = new[]
            {
                BookingStatus.Paid,
                BookingStatus.CheckedIn,
                BookingStatus.CompletedPendingInvoice,
                BookingStatus.Disputed
            };

            if (!chattableStatuses.Contains(booking.Status))
                throw new InvalidOperationException(
                    "Không thể gửi tin nhắn. Chat chỉ khả dụng khi booking đã thanh toán và chưa hoàn tất.");

            var conv = await _chatRepo.GetConversationByBookingAsync(bookingId);

            if (conv == null)
            {
                conv = new ChatConversation
                {
                    BookingId = bookingId,
                    DriverUserId = driverUserId,
                    OwnerUserId = ownerUserId,
                    CreatedAt = DateTimeHelper.VietnamNow()
                };
                _chatRepo.AddConversation(conv);
                await _unitOfWork.CompleteAsync(); // Save to generate ID
            }

            var senderName = await _userRepo.GetFullNameAsync(userId) ?? "Unknown";

            var message = new ChatMessage
            {
                ConversationId = conv.Id,
                SenderUserId = userId,
                Content = dto.Content?.Trim() ?? "",
                IsRead = false,
                CreatedAt = DateTimeHelper.VietnamNow()
            };

            _chatRepo.AddMessage(message);
            await _unitOfWork.CompleteAsync();

            var messageDto = new ChatMessageDto
            {
                Id = message.Id,
                SenderUserId = userId,
                SenderName = senderName,
                Content = message.Content ?? "",
                IsRead = false,
                CreatedAt = message.CreatedAt
            };

            await _hubContext.Clients.Group($"chat_{conv.Id}").SendAsync("ReceiveMessage", messageDto);

            return messageDto;
        }
    }
}
