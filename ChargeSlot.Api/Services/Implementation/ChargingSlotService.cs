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
            var station = await _stationRepo.GetByIdAsync(stationId, includeDetails: false);
            if (station == null)
                throw new KeyNotFoundException($"Station {stationId} not found.");
            if (station.OwnerUserId != ownerUserId)
                throw new UnauthorizedAccessException("You do not own this station.");

            // If station is Approved, slot goes Active with QR token immediately
            var isApproved = station.ApprovalStatus == ApprovalStatus.Approved;

            var slot = new ChargingSlot
            {
                StationId = stationId,
                SlotName = dto.SlotName,
                BasePricePerHour = dto.BasePricePerHour,
                PositionX = dto.PositionX,
                PositionY = dto.PositionY,
                Status = isApproved ? SlotStatus.Active : SlotStatus.Inactive,
                QrCodeToken = isApproved ? Guid.NewGuid().ToString("N") : null,
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

        public async Task UpdateStatusAsync(int stationId, int slotId, int ownerUserId, UpdateSlotStatusDto dto)
        {
            // Validate ownership
            var station = await _stationRepo.GetByIdAsync(stationId, includeDetails: false);
            if (station == null)
                throw new KeyNotFoundException($"Station {stationId} not found.");
            if (station.OwnerUserId != ownerUserId)
                throw new UnauthorizedAccessException("You do not own this station.");

            // Station must be Approved for status changes
            if (station.ApprovalStatus != ApprovalStatus.Approved)
            {
                throw new InvalidOperationException(
                    $"Cannot change slot status when station is in '{station.ApprovalStatus}' status. Station must be Approved.");
            }

            // Owner can only set Active, Inactive, or Maintenance (Booked is system-managed)
            if (dto.Status == SlotStatus.Booked)
            {
                throw new InvalidOperationException(
                    "Cannot manually set slot to Booked. This status is managed by the booking system.");
            }

            var slot = await _slotRepo.GetByIdAsync(slotId, tracking: true);
            if (slot == null || slot.StationId != stationId)
                throw new KeyNotFoundException($"Slot {slotId} not found in station {stationId}.");

            // Cannot change status of a currently Booked slot
            if (slot.Status == SlotStatus.Booked)
            {
                throw new InvalidOperationException(
                    "Cannot change status of a slot that is currently Booked. Wait for the booking to complete or expire.");
            }

            slot.Status = dto.Status;
            slot.UpdatedAt = DateTime.UtcNow;

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
                BasePricePerHour = slot.BasePricePerHour,
                PositionX = slot.PositionX,
                PositionY = slot.PositionY,
                QrCodeToken = slot.QrCodeToken,
                Status = slot.Status.ToString(),
                CreatedAt = slot.CreatedAt,
                UpdatedAt = slot.UpdatedAt
            };
        }
    }
}
