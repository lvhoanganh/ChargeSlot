using ChargeSlot.Api.DTOs.Station;
using ChargeSlot.Api.DTOs.Admin;

namespace ChargeSlot.Api.Services.Interfaces
{
    public interface IChargingStationService
    {
        // CRUD
        Task<ChargingStationDto?> GetByIdAsync(int id, int ownerUserId);
        Task<List<ChargingStationDto>> GetAllByOwnerAsync(int ownerUserId);
        Task<ChargingStationDto> CreateAsync(int ownerUserId, CreateChargingStationDto dto);
        Task<ChargingStationDto> CreateFromFormAsync(int ownerUserId, CreateStationFormDto dto, HttpRequest request);
        Task<ChargingStationDto> UpdateFromFormAsync(int id, int ownerUserId, UpdateStationFormDto dto, HttpRequest request);
        Task DeleteAsync(int id, int ownerUserId);

        // Approval Flow
        Task SubmitForApprovalAsync(int id, int ownerUserId);

        // Admin
        Task<PagedResultDto<ChargingStationDto>> GetAdminStationsAsync(string? status, string? search, int page, int pageSize);
        Task<List<ChargingStationDto>> GetPendingStationsAsync();
        Task<ChargingStationDto?> GetStationDetailForAdminAsync(int id);
        Task ReviewStationAsync(int id, int adminUserId, ReviewStationDto dto);
        Task<string> ToggleBanStationAsync(int id, int adminUserId);
    }
}
