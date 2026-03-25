using ChargeSlot.Api.Helpers;
using ChargeSlot.Api.Models.Identity;

namespace ChargeSlot.Api.Models
{
    /// <summary>Tin nhắn trong cuộc hội thoại chat.</summary>
    public class ChatMessage
    {
        public int Id { get; set; }

        public int ConversationId { get; set; }
        public ChatConversation Conversation { get; set; } = null!;

        public int SenderUserId { get; set; }
        public ApplicationUser Sender { get; set; } = null!;

        public string Content { get; set; } = null!;
        public bool IsRead { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTimeHelper.VietnamNow();
    }
}
