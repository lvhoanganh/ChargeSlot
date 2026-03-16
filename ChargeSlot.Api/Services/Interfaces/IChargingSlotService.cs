using ChargeSlot.Api.DTOs.Slot;

namespace ChargeSlot.Api.Services.Interfaces
{
    public interface IChargingSlotService
    {
        Task<ChargingSlotDto?> GetByIdAsync(int stationId, int slotId, int ownerUserId);
        Task<List<ChargingSlotDto>> GetAllByStationAsync(int stationId, int ownerUserId);
        Task<ChargingSlotDto> CreateAsync(int stationId, int ownerUserId, CreateChargingSlotDto dto);
        Task UpdateAsync(int stationId, int slotId, int ownerUserId, UpdateChargingSlotDto dto);
        Task DeleteAsync(int stationId, int slotId, int ownerUserId);
        Task UpdateStatusAsync(int stationId, int slotId, int ownerUserId, UpdateSlotStatusDto dto);
    }
}
