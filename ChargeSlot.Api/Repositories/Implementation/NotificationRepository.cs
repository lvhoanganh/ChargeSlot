using ChargeSlot.Api.Data;
using ChargeSlot.Api.Models;
using ChargeSlot.Api.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ChargeSlot.Api.Repositories.Implementation
{
    public class NotificationRepository : INotificationRepository
    {
        private readonly ChargeSlotDbContext _db;

        public NotificationRepository(ChargeSlotDbContext db)
        {
            _db = db;
        }

        public async Task<List<Notification>> GetByUserAsync(int userId)
        {
            return await _db.Notifications
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync();
        }

        public async Task<(List<Notification> Items, int TotalCount)> GetByUserPagedAsync(int userId, int page, int pageSize)
        {
            var query = _db.Notifications.Where(n => n.UserId == userId);
            int totalCount = await query.CountAsync();
            var items = await query.OrderByDescending(n => n.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
            return (items, totalCount);
        }

        public async Task<Notification?> GetByIdAsync(int id)
        {
            return await _db.Notifications.FindAsync(id);
        }

        public void Add(Notification notification)
        {
            _db.Notifications.Add(notification);
        }

        public void Update(Notification notification)
        {
            _db.Notifications.Update(notification);
        }
    }
}

