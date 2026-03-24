using ChargeSlot.Api.Repositories.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ChargeSlot.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class NotificationController : ControllerBase
    {
        private readonly INotificationRepository _notificationRepo;

        public NotificationController(INotificationRepository notificationRepo)
        {
            _notificationRepo = notificationRepo;
        }

        private int GetUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        /// <summary>
        /// Lấy danh sách notification của user hiện tại
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetNotifications()
        {
            var notifications = await _notificationRepo.GetByUserAsync(GetUserId());
            return Ok(notifications.Select(n => new
            {
                n.Id,
                n.Title,
                n.Content,
                Type = n.Type.ToString(),
                n.IsRead,
                n.CreatedAt
            }));
        }

        /// <summary>
        /// Đánh dấu notification đã đọc
        /// </summary>
        [HttpPut("{id}/read")]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            var notification = await _notificationRepo.GetByIdAsync(id);
            if (notification == null) return NotFound();
            if (notification.UserId != GetUserId()) return Forbid();

            notification.IsRead = true;
            await _notificationRepo.UpdateAsync(notification);
            return Ok(new { message = "Đã đánh dấu đã đọc." });
        }
    }
}
