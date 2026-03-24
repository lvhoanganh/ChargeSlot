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

        public async Task<Notification?> GetByIdAsync(int id)
        {
            return await _db.Notifications.FindAsync(id);
        }

        public async Task CreateAsync(Notification notification)
        {
            _db.Notifications.Add(notification);
            await _db.SaveChangesAsync();
        }

        public async Task UpdateAsync(Notification notification)
        {
            _db.Notifications.Update(notification);
            await _db.SaveChangesAsync();
        }
    }
}
