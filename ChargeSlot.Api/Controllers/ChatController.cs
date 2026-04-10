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
using ChargeSlot.Api.Services.Interfaces;

namespace ChargeSlot.Api.Controllers
{
    /// <summary>
    /// REST API cho chat — lịch sử, danh sách, và fallback gửi tin nhắn.
    /// </summary>
    // TODO: Refactor – move business logic to a dedicated ChatService
    [ApiController]
    [Route("api/chat")]
    [Authorize]
    public class ChatController : ControllerBase
    {
        private readonly IChatService _chatService;
        public ChatController(IChatService chatService)
        {
            _chatService = chatService;
        }

        private int GetUserId()
        {
            var id = User.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? throw new InvalidOperationException("UserId missing in token");
            return int.Parse(id);
        }

        /// <summary>Danh sách conversations của user hiện tại (phân trang).</summary>
        [HttpGet]
        public async Task<IActionResult> GetConversations(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            var userId = GetUserId();

            var result = await _chatService.GetConversationsAsync(userId, page, pageSize);
            return Ok(result);
        }

        /// <summary>Lấy lịch sử chat theo bookingId (phân trang).</summary>
        [HttpGet("{bookingId:int}")]
        public async Task<IActionResult> GetMessages(
            int bookingId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            var userId = GetUserId();

            try
            {
                var result = await _chatService.GetMessagesAsync(userId, bookingId, page, pageSize);
                return Ok(result);
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
        }

        /// <summary>Gửi tin nhắn via REST (fallback khi không dùng SignalR). Tự tạo conversation nếu chưa có.</summary>
        [HttpPost("{bookingId:int}")]
        public async Task<IActionResult> SendMessage(int bookingId, [FromBody] SendMessageDto dto)
        {
            var userId = GetUserId();

            ChatMessageDto messageDto;
            try
            {
                messageDto = await _chatService.SendMessageAsync(userId, bookingId, dto);
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { message = "Booking không tồn tại." });
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }

            // ChatService is now handling the SignalR broadcast internally as well
            return Ok(messageDto);
        }
    }
}
