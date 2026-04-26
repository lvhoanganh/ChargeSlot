using ChargeSlot.Api.Helpers;
using ChargeSlot.Api.Models.Identity;

namespace ChargeSlot.Api.Models
{
    /// <summary>Cuộc hội thoại chat giữa Driver và Owner theo booking.</summary>
    public class ChatConversation
    {
        public int Id { get; set; }

        public int BookingId { get; set; }
        public Booking Booking { get; set; } = null!;

        public int DriverUserId { get; set; }
        public ApplicationUser Driver { get; set; } = null!;

        public int OwnerUserId { get; set; }
        public ApplicationUser Owner { get; set; } = null!;

        public DateTime CreatedAt { get; set; } = DateTimeHelper.VietnamNow();

        public ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
    }
}
