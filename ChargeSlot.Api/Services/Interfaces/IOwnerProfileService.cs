using ChargeSlot.Api.DTOs.Profile;

namespace ChargeSlot.Api.Services.Interfaces
{
    public interface IOwnerProfileService
    {
        Task<OwnerProfileDto?> GetByUserIdAsync(int userId);
        Task UpsertForUserAsync(int userId, OwnerProfileDto dto);
        Task DeleteForUserAsync(int userId);
        Task<string> UploadAvatarAsync(int userId, IFormFile file);
    }
}

