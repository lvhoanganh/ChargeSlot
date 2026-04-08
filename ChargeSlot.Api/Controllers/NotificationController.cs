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
        private readonly IUnitOfWork _unitOfWork;

        public NotificationController(INotificationRepository notificationRepo, IUnitOfWork unitOfWork)
        {
            _notificationRepo = notificationRepo;
            _unitOfWork = unitOfWork;
        }

        private int GetUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        /// <summary>
        /// Lấy danh sách notification của user hiện tại (phân trang)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetNotifications(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            var all = await _notificationRepo.GetByUserAsync(GetUserId());
            var total = all.Count;
            var items = all
                .OrderByDescending(n => n.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(n => new
                {
                    n.Id,
                    n.Title,
                    n.Content,
                    Type = n.Type.ToString(),
                    n.IsRead,
                    n.CreatedAt
                }).ToList();
            return Ok(new { total, page, pageSize, items });
        }

        /// <summary>
        /// Đếm số notification chưa đọc
        /// </summary>
        [HttpGet("unread-count")]
        public async Task<IActionResult> GetUnreadCount()
        {
            var all = await _notificationRepo.GetByUserAsync(GetUserId());
            var count = all.Count(n => !n.IsRead);
            return Ok(new { unreadCount = count });
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
            _notificationRepo.Update(notification);
            await _unitOfWork.CompleteAsync();
            return Ok(new { message = "Đã đánh dấu đã đọc." });
        }
    }
}


