using ChargeSlot.Api.DTOs.Slot;
using ChargeSlot.Api.Enums;
using ChargeSlot.Api.Models;
using ChargeSlot.Api.Repositories.Interfaces;
using ChargeSlot.Api.Services.Interfaces;

namespace ChargeSlot.Api.Services.Implementation
{
    public class ChargingSlotService : IChargingSlotService
    {
        private readonly IChargingSlotRepository _slotRepo;
        private readonly IChargingStationRepository _stationRepo;

        public ChargingSlotService(
            IChargingSlotRepository slotRepo,
            IChargingStationRepository stationRepo)
        {
            _slotRepo = slotRepo;
            _stationRepo = stationRepo;
        }

        public async Task<ChargingSlotDto?> GetByIdAsync(int stationId, int slotId, int ownerUserId)
        {
            await ValidateStationOwnershipAsync(stationId, ownerUserId);

            var slot = await _slotRepo.GetByIdAsync(slotId);
            if (slot == null || slot.StationId != stationId)
                return null;

            return MapToDto(slot);
        }

        public async Task<List<ChargingSlotDto>> GetAllByStationAsync(int stationId, int ownerUserId)
        {
            await ValidateStationOwnershipAsync(stationId, ownerUserId);

            var slots = await _slotRepo.GetAllByStationAsync(stationId);
            return slots.Select(MapToDto).ToList();
        }

        public async Task<ChargingSlotDto> CreateAsync(int stationId, int ownerUserId, CreateChargingSlotDto dto)
        {
            await ValidateStationEditableAsync(stationId, ownerUserId);

            var slot = new ChargingSlot
            {
                StationId = stationId,
                SlotName = dto.SlotName,
                ConnectorType = dto.ConnectorType,
                PowerKw = dto.PowerKw,
                BasePricePerHour = dto.BasePricePerHour,
                PositionX = dto.PositionX,
                PositionY = dto.PositionY,
                Status = SlotStatus.Inactive,
                CreatedAt = DateTime.UtcNow
            };

            await _slotRepo.AddAsync(slot);
            await _slotRepo.SaveChangesAsync();

            return MapToDto(slot);
        }

        public async Task UpdateAsync(int stationId, int slotId, int ownerUserId, UpdateChargingSlotDto dto)
        {
            await ValidateStationEditableAsync(stationId, ownerUserId);

            var slot = await _slotRepo.GetByIdAsync(slotId, tracking: true);
            if (slot == null || slot.StationId != stationId)
                throw new KeyNotFoundException($"Slot {slotId} not found in station {stationId}.");

            slot.SlotName = dto.SlotName;
            slot.ConnectorType = dto.ConnectorType;
            slot.PowerKw = dto.PowerKw;
            slot.BasePricePerHour = dto.BasePricePerHour;
            slot.PositionX = dto.PositionX;
            slot.PositionY = dto.PositionY;
            slot.UpdatedAt = DateTime.UtcNow;

            await _slotRepo.SaveChangesAsync();
        }

        public async Task DeleteAsync(int stationId, int slotId, int ownerUserId)
        {
            await ValidateStationEditableAsync(stationId, ownerUserId);

            var slot = await _slotRepo.GetByIdAsync(slotId, tracking: true);
            if (slot == null || slot.StationId != stationId)
                throw new KeyNotFoundException($"Slot {slotId} not found in station {stationId}.");

            _slotRepo.Remove(slot);
            await _slotRepo.SaveChangesAsync();
        }

        // ─────────────── HELPERS ───────────────

        /// <summary>Check ownership only (for read operations).</summary>
        private async Task ValidateStationOwnershipAsync(int stationId, int ownerUserId)
        {
            var station = await _stationRepo.GetByIdAsync(stationId, includeDetails: false);
            if (station == null)
                throw new KeyNotFoundException($"Station {stationId} not found.");
            if (station.OwnerUserId != ownerUserId)
                throw new UnauthorizedAccessException("You do not own this station.");
        }

        /// <summary>Check ownership + station must be Draft or Rejected (for write operations).</summary>
        private async Task ValidateStationEditableAsync(int stationId, int ownerUserId)
        {
            var station = await _stationRepo.GetByIdAsync(stationId, includeDetails: false);
            if (station == null)
                throw new KeyNotFoundException($"Station {stationId} not found.");
            if (station.OwnerUserId != ownerUserId)
                throw new UnauthorizedAccessException("You do not own this station.");
            if (station.ApprovalStatus != ApprovalStatus.Draft &&
                station.ApprovalStatus != ApprovalStatus.Rejected)
            {
                throw new InvalidOperationException(
                    $"Cannot modify slots when station is in '{station.ApprovalStatus}' status. Only Draft or Rejected stations can be edited.");
            }
        }

        private static ChargingSlotDto MapToDto(ChargingSlot slot)
        {
            return new ChargingSlotDto
            {
                Id = slot.Id,
                StationId = slot.StationId,
                SlotName = slot.SlotName,
                ConnectorType = slot.ConnectorType,
                PowerKw = slot.PowerKw,
                BasePricePerHour = slot.BasePricePerHour,
                PositionX = slot.PositionX,
                PositionY = slot.PositionY,
                Status = slot.Status.ToString(),
                CreatedAt = slot.CreatedAt,
                UpdatedAt = slot.UpdatedAt
            };
        }
    }
}
