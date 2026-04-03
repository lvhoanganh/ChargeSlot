using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ChargeSlot.Api.DTOs.Analytics
{
    public class ChatMessageDto
    {
        [Required]
        public string Role { get; set; } // "user" hoặc "model"
        
        [Required]
        public string Content { get; set; }
    }

    public class ChatbotRequestDto
    {
        // Tích hợp Sliding Window (Frontend chỉ nên gởi 5-10 dòng lịch sử)
        public List<ChatMessageDto> History { get; set; } = new List<ChatMessageDto>();

        [Required]
        [MaxLength(1000)]
        public string CurrentMessage { get; set; }
    }

    public class ChatbotResponseDto
    {
        public string ReplyMarkdown { get; set; }
    }
}
