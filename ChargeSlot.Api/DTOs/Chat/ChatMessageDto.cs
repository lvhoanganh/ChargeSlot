namespace ChargeSlot.Api.DTOs.Chat
{
    public class ChatMessageDto
    {
        public int Id { get; set; }
        public int SenderUserId { get; set; }
        public string SenderName { get; set; } = null!;
        public string Content { get; set; } = null!;
        public bool IsRead { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
