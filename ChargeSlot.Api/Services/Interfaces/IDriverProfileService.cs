using ChargeSlot.Api.DTOs.Profile;

namespace ChargeSlot.Api.Services.Interfaces
{
    public interface IDriverProfileService
    {
        Task<DriverProfileDto?> GetByUserIdAsync(int userId);
        Task UpsertForUserAsync(int userId, DriverProfileDto dto);
        Task DeleteForUserAsync(int userId);
    }
}

