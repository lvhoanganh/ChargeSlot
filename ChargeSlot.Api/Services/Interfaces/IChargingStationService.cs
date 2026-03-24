using ChargeSlot.Api.DTOs.Station;

namespace ChargeSlot.Api.Services.Interfaces
{
    public interface IChargingStationService
    {
        // CRUD
        Task<ChargingStationDto?> GetByIdAsync(int id, int ownerUserId);
        Task<List<ChargingStationDto>> GetAllByOwnerAsync(int ownerUserId);
        Task<ChargingStationDto> CreateAsync(int ownerUserId, CreateChargingStationDto dto);
        Task<ChargingStationDto> CreateFromFormAsync(int ownerUserId, CreateStationFormDto dto, HttpRequest request);
        Task UpdateAsync(int id, int ownerUserId, UpdateChargingStationDto dto);
        Task DeleteAsync(int id, int ownerUserId);

        // Approval Flow
        Task SubmitForApprovalAsync(int id, int ownerUserId);

        // Admin
        Task<List<ChargingStationDto>> GetPendingStationsAsync();
        Task<ChargingStationDto?> GetStationDetailForAdminAsync(int id);
        Task ReviewStationAsync(int id, int adminUserId, ReviewStationDto dto);
    }
}
