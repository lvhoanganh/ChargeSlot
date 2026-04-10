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

        // ─────────────── UNAVAILABLE DATES ───────────────
        Task<List<UnavailableDateDto>> GetUnavailableDatesAsync(int stationId);
        Task<List<UnavailableDateDto>> AddUnavailableDatesAsync(int stationId, int ownerUserId, AddUnavailableDatesDto dto);
        Task RemoveUnavailableDatesAsync(int stationId, int ownerUserId, RemoveUnavailableDatesDto dto);

        // ─────────────── STATUS ───────────────
        Task<ChargingStationDto> UpdateOperationalStatusAsync(int id, int ownerUserId, string operationalStatus);

        // ─────────────── PRICING ───────────────
        Task<List<StationPricingDto>> GetPricingAsync(int stationId, int ownerUserId);
        Task<StationPricingDto> CreatePricingAsync(int stationId, int ownerUserId, CreateStationPricingDto dto);
        Task<StationPricingDto> UpdatePricingAsync(int stationId, int pricingId, int ownerUserId, UpdateStationPricingDto dto);
        Task DeletePricingAsync(int stationId, int pricingId, int ownerUserId);

        // ─────────────── EXTRA SERVICES ───────────────
        Task<List<ExtraServiceDto>> GetExtraServicesAsync(int stationId, int ownerUserId);
        Task<ExtraServiceDto> CreateExtraServiceAsync(int stationId, int ownerUserId, CreateExtraServiceDto dto);
        Task<ExtraServiceDto> UpdateExtraServiceAsync(int stationId, int serviceId, int ownerUserId, UpdateExtraServiceDto dto);
        Task DeleteExtraServiceAsync(int stationId, int serviceId, int ownerUserId);

        // Admin
        Task<PagedResultDto<ChargingStationDto>> GetAdminStationsAsync(string? status, string? search, int page, int pageSize);
        Task<List<ChargingStationDto>> GetPendingStationsAsync();
        Task<ChargingStationDto?> GetStationDetailForAdminAsync(int id);
        Task ReviewStationAsync(int id, int adminUserId, ReviewStationDto dto);
        Task<string> ToggleBanStationAsync(int id, int adminUserId);
    }
}
