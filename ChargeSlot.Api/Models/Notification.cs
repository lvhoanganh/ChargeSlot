using ChargeSlot.Api.Enums;
using ChargeSlot.Api.Models.Identity;

namespace ChargeSlot.Api.Models
{
    /// <summary>SRS 1.5 Notification - booking, payment, charging, admin alerts.</summary>
    public class Notification
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public ApplicationUser User { get; set; } = null!;

        public string Title { get; set; } = null!;
        public string Content { get; set; } = null!;
        public NotificationType Type { get; set; } = NotificationType.System;
        public bool IsRead { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
