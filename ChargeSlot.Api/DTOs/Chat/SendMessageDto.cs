using System.ComponentModel.DataAnnotations;

namespace ChargeSlot.Api.DTOs.Chat
{
    public class SendMessageDto
    {
        [Required]
        [MaxLength(1000)]
        public string Content { get; set; } = null!;
    }
}
