namespace ChargeSlot.Api.DTOs.Chat
{
    public class ChatConversationDto
    {
        public int Id { get; set; }
        public int BookingId { get; set; }
        public string StationName { get; set; } = null!;
        public string OtherUserName { get; set; } = null!;
        public int OtherUserId { get; set; }
        public string? LastMessage { get; set; }
        public DateTime? LastMessageAt { get; set; }
        public int UnreadCount { get; set; }
    }
}
