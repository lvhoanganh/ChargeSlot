using ChargeSlot.Api.Enums;
using ChargeSlot.Api.Models;

namespace ChargeSlot.Api.Repositories.Interfaces
{
    public interface IChargingStationRepository
    {
        Task<ChargingStation?> GetByIdAsync(int id, bool tracking = false, bool includeDetails = true);
        Task<List<ChargingStation>> GetAllByOwnerAsync(int ownerUserId);
        Task<List<ChargingStation>> GetByApprovalStatusAsync(ApprovalStatus status);
        Task<List<ChargingStation>> GetPublicStationsAsync(string? keyword, decimal? minRating);
        Task<List<ChargingStation>> GetPublicStationsWithCoordinatesAsync();
        Task AddAsync(ChargingStation station);
        void Update(ChargingStation station);
        void Remove(ChargingStation station);
        void RemoveOperatingHours(IEnumerable<StationOperatingHours> hours);
        void RemoveImages(IEnumerable<StationImage> images);
        void RemoveSlots(IEnumerable<ChargingSlot> slots);
        Task<List<ChargingStation>> GetTopRatedStationsAsync(int limit);
        Task<List<ChargingStation>> GetAllByOwnerTrackingAsync(int ownerUserId);
        Task<(List<ChargingStation> Items, int Total)> GetAdminStationsPagedAsync(string? status, string? search, string? ownerName, int page, int pageSize);
        Task<List<ChargingStation>> GetBannedExpiredAsync(DateTime now);
    }
}

