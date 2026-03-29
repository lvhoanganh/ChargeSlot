using System.Security.Claims;
using ChargeSlot.Api.Data;
using ChargeSlot.Api.DTOs.Chat;
using ChargeSlot.Api.Helpers;
using ChargeSlot.Api.Hubs;
using ChargeSlot.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace ChargeSlot.Api.Controllers
{
    /// <summary>
    /// REST API cho chat — lịch sử, danh sách, và fallback gửi tin nhắn.
    /// </summary>
    [ApiController]
    [Route("api/chat")]
    [Authorize]
    public class ChatController : ControllerBase
    {
        private readonly ChargeSlotDbContext _db;
        private readonly IHubContext<ChatHub> _hubContext;

        public ChatController(ChargeSlotDbContext db, IHubContext<ChatHub> hubContext)
        {
            _db = db;
            _hubContext = hubContext;
        }

        private int GetUserId()
        {
            var id = User.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? throw new InvalidOperationException("UserId missing in token");
            return int.Parse(id);
        }

        /// <summary>Danh sách conversations của user hiện tại.</summary>
        [HttpGet]
        public async Task<IActionResult> GetConversations()
        {
            var userId = GetUserId();

            var conversations = await _db.ChatConversations
                .Include(c => c.Booking).ThenInclude(b => b.ChargingSlot).ThenInclude(s => s.ChargingStation)
                .Include(c => c.Driver)
                .Include(c => c.Owner)
                .Where(c => c.DriverUserId == userId || c.OwnerUserId == userId)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();

            var result = new List<ChatConversationDto>();

            foreach (var conv in conversations)
            {
                var isDriver = conv.DriverUserId == userId;
                var otherUser = isDriver ? conv.Owner : conv.Driver;

                var lastMsg = await _db.ChatMessages
                    .Where(m => m.ConversationId == conv.Id)
                    .OrderByDescending(m => m.CreatedAt)
                    .FirstOrDefaultAsync();

                var unread = await _db.ChatMessages
                    .CountAsync(m => m.ConversationId == conv.Id
                                  && m.SenderUserId != userId
                                  && !m.IsRead);

                result.Add(new ChatConversationDto
                {
                    Id = conv.Id,
                    BookingId = conv.BookingId,
                    StationName = conv.Booking?.ChargingSlot?.ChargingStation?.Name ?? "",
                    OtherUserName = otherUser?.FullName ?? "",
                    OtherUserId = isDriver ? conv.OwnerUserId : conv.DriverUserId,
                    LastMessage = lastMsg?.Content,
                    LastMessageAt = lastMsg?.CreatedAt,
                    UnreadCount = unread
                });
            }

            return Ok(result);
        }

        /// <summary>Lấy lịch sử chat theo bookingId.</summary>
        [HttpGet("{bookingId:int}")]
        public async Task<IActionResult> GetMessages(int bookingId)
        {
            var userId = GetUserId();

            var conv = await _db.ChatConversations
                .FirstOrDefaultAsync(c => c.BookingId == bookingId);

            if (conv == null)
                return Ok(new { conversationId = (int?)null, messages = new List<ChatMessageDto>() });

            if (conv.DriverUserId != userId && conv.OwnerUserId != userId)
                return Forbid();

            var messages = await _db.ChatMessages
                .Where(m => m.ConversationId == conv.Id)
                .OrderBy(m => m.CreatedAt)
                .Select(m => new ChatMessageDto
                {
                    Id = m.Id,
                    SenderUserId = m.SenderUserId,
                    SenderName = m.Sender.FullName ?? "",
                    Content = m.Content,
                    IsRead = m.IsRead,
                    CreatedAt = m.CreatedAt
                })
                .ToListAsync();

            return Ok(new { conversationId = conv.Id, messages });
        }

        /// <summary>Gửi tin nhắn via REST (fallback khi không dùng SignalR). Tự tạo conversation nếu chưa có.</summary>
        [HttpPost("{bookingId:int}")]
        public async Task<IActionResult> SendMessage(int bookingId, [FromBody] SendMessageDto dto)
        {
            var userId = GetUserId();

            // Tìm booking + validate quyền
            var booking = await _db.Bookings
                .Include(b => b.ChargingSlot).ThenInclude(s => s.ChargingStation)
                .FirstOrDefaultAsync(b => b.Id == bookingId);

            if (booking == null)
                return NotFound(new { message = "Booking không tồn tại." });

            var ownerUserId = booking.ChargingSlot.ChargingStation.OwnerUserId;
            var driverUserId = booking.DriverUserId;

            if (userId != driverUserId && userId != ownerUserId)
                return Forbid();

            // Tìm hoặc tạo conversation
            var conv = await _db.ChatConversations
                .FirstOrDefaultAsync(c => c.BookingId == bookingId);

            if (conv == null)
            {
                conv = new ChatConversation
                {
                    BookingId = bookingId,
                    DriverUserId = driverUserId,
                    OwnerUserId = ownerUserId,
                    CreatedAt = DateTimeHelper.VietnamNow()
                };
                _db.ChatConversations.Add(conv);
                await _db.SaveChangesAsync();
            }

            // Tạo message
            var senderName = await _db.Users
                .Where(u => u.Id == userId)
                .Select(u => u.FullName)
                .FirstOrDefaultAsync() ?? "Unknown";

            var message = new ChatMessage
            {
                ConversationId = conv.Id,
                SenderUserId = userId,
                Content = dto.Content.Trim(),
                IsRead = false,
                CreatedAt = DateTimeHelper.VietnamNow()
            };

            _db.ChatMessages.Add(message);
            await _db.SaveChangesAsync();

            var messageDto = new ChatMessageDto
            {
                Id = message.Id,
                SenderUserId = userId,
                SenderName = senderName,
                Content = message.Content,
                IsRead = false,
                CreatedAt = message.CreatedAt
            };

            // Broadcast qua SignalR nếu có client đang listen
            await _hubContext.Clients.Group($"chat_{conv.Id}")
                .SendAsync("ReceiveMessage", messageDto);

            return Ok(messageDto);
        }
    }
}
