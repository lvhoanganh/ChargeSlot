using ChargeSlot.Api.DTOs.Analytics;
using System.Threading.Tasks;

namespace ChargeSlot.Api.Services.Interfaces
{
    public interface IAiChatbotService
    {
        Task<ChatbotResponseDto> ProcessDriverChatAsync(int userId, ChatbotRequestDto request);
        Task<ChatbotResponseDto> ProcessOwnerChatAsync(int ownerId, ChatbotRequestDto request);
        Task<ChatbotResponseDto> ProcessAdminChatAsync(ChatbotRequestDto request);
    }
}
