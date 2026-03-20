using ChargeSlot.Api.Data;
using ChargeSlot.Api.DTOs.Slot;
using ChargeSlot.Api.Enums;
using ChargeSlot.Api.Models;
using ChargeSlot.Api.Repositories.Interfaces;
using ChargeSlot.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ChargeSlot.Api.Services.Implementation
{
    public class ChargingSlotService : IChargingSlotService
    {
        private readonly IChargingSlotRepository _slotRepo;
        private readonly IChargingStationRepository _stationRepo;
        private readonly ChargeSlotDbContext _db;

        public ChargingSlotService(
            IChargingSlotRepository slotRepo,
            IChargingStationRepository stationRepo,
            ChargeSlotDbContext db)
        {
            _slotRepo = slotRepo;
            _stationRepo = stationRepo;
            _db = db;
        }

        public async Task<ChargingSlotDto?> GetByIdAsync(int stationId, int slotId, int ownerUserId)
        {
            await ValidateStationOwnershipAsync(stationId, ownerUserId);

            var slot = await _slotRepo.GetByIdAsync(slotId);
            if (slot == null || slot.StationId != stationId)
                return null;

            var pricings = await _db.Set<SlotPricing>()
                .Where(p => p.SlotId == slotId && p.IsActive)
                .OrderBy(p => p.StartTime)
                .ToListAsync();

            return MapToDto(slot, pricings);
        }

        public async Task<List<ChargingSlotDto>> GetAllByStationAsync(int stationId, int ownerUserId)
        {
            await ValidateStationOwnershipAsync(stationId, ownerUserId);

            var slots = await _slotRepo.GetAllByStationAsync(stationId);
            var slotIds = slots.Select(s => s.Id).ToList();

            var pricings = await _db.Set<SlotPricing>()
                .Where(p => slotIds.Contains(p.SlotId) && p.IsActive)
                .OrderBy(p => p.StartTime)
                .ToListAsync();

            return slots.Select(s => MapToDto(s, pricings.Where(p => p.SlotId == s.Id).ToList())).ToList();
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
                PositionX = dto.PositionX,
                PositionY = dto.PositionY,
                Status = isApproved ? SlotStatus.Active : SlotStatus.Inactive,
                QrCodeToken = isApproved ? Guid.NewGuid().ToString("N") : null,
                CreatedAt = DateTime.UtcNow
            };

            await _slotRepo.AddAsync(slot);
            await _slotRepo.SaveChangesAsync();

            // Save pricing tiers
            var savedPricings = new List<SlotPricing>();
            if (dto.PricingTiers?.Count > 0)
            {
                savedPricings = SavePricingTiers(slot.Id, dto.PricingTiers);
                await _db.SaveChangesAsync();
            }

            return MapToDto(slot, savedPricings);
        }

        public async Task UpdateAsync(int stationId, int slotId, int ownerUserId, UpdateChargingSlotDto dto)
        {
            await ValidateStationOwnershipAsync(stationId, ownerUserId);

            var slot = await _slotRepo.GetByIdAsync(slotId, tracking: true);
            if (slot == null || slot.StationId != stationId)
                throw new KeyNotFoundException($"Slot {slotId} not found in station {stationId}.");

            slot.SlotName = dto.SlotName;
            slot.PositionX = dto.PositionX;
            slot.PositionY = dto.PositionY;
            slot.UpdatedAt = DateTime.UtcNow;

            // Replace all pricing tiers
            if (dto.PricingTiers != null)
            {
                // Remove old pricing
                var oldPricings = await _db.Set<SlotPricing>()
                    .Where(p => p.SlotId == slotId)
                    .ToListAsync();
                _db.Set<SlotPricing>().RemoveRange(oldPricings);

                // Add new pricing
                if (dto.PricingTiers.Count > 0)
                {
                    SavePricingTiers(slotId, dto.PricingTiers);
                }
            }

            await _slotRepo.SaveChangesAsync();
            await _db.SaveChangesAsync();
        }

        public async Task DeleteAsync(int stationId, int slotId, int ownerUserId)
        {
            await ValidateStationOwnershipAsync(stationId, ownerUserId);

            var slot = await _slotRepo.GetByIdAsync(slotId, tracking: true);
            if (slot == null || slot.StationId != stationId)
                throw new KeyNotFoundException($"Slot {slotId} not found in station {stationId}.");

            // Also delete pricing
            var pricings = await _db.Set<SlotPricing>()
                .Where(p => p.SlotId == slotId)
                .ToListAsync();
            _db.Set<SlotPricing>().RemoveRange(pricings);

            _slotRepo.Remove(slot);
            await _slotRepo.SaveChangesAsync();
        }

        public async Task UpdateStatusAsync(int stationId, int slotId, int ownerUserId, UpdateSlotStatusDto dto)
        {
            var station = await _stationRepo.GetByIdAsync(stationId, includeDetails: false);
            if (station == null)
                throw new KeyNotFoundException($"Station {stationId} not found.");
            if (station.OwnerUserId != ownerUserId)
                throw new UnauthorizedAccessException("You do not own this station.");

            if (station.ApprovalStatus != ApprovalStatus.Approved)
            {
                throw new InvalidOperationException(
                    $"Cannot change slot status when station is in '{station.ApprovalStatus}' status. Station must be Approved.");
            }

            if (dto.Status == SlotStatus.Booked)
            {
                throw new InvalidOperationException(
                    "Cannot manually set slot to Booked. This status is managed by the booking system.");
            }

            var slot = await _slotRepo.GetByIdAsync(slotId, tracking: true);
            if (slot == null || slot.StationId != stationId)
                throw new KeyNotFoundException($"Slot {slotId} not found in station {stationId}.");

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

        private async Task ValidateStationOwnershipAsync(int stationId, int ownerUserId)
        {
            var station = await _stationRepo.GetByIdAsync(stationId, includeDetails: false);
            if (station == null)
                throw new KeyNotFoundException($"Station {stationId} not found.");
            if (station.OwnerUserId != ownerUserId)
                throw new UnauthorizedAccessException("You do not own this station.");
        }

        /// <summary>Parse and save pricing tiers to DB.</summary>
        private List<SlotPricing> SavePricingTiers(int slotId, List<PricingTierItem> tiers)
        {
            var now = DateTime.UtcNow;
            var result = new List<SlotPricing>();

            foreach (var tier in tiers)
            {
                if (!TimeOnly.TryParse(tier.StartTime, out var startTime) ||
                    !TimeOnly.TryParse(tier.EndTime, out var endTime))
                    continue;

                var pricing = new SlotPricing
                {
                    SlotId = slotId,
                    StartTime = startTime,
                    EndTime = endTime,
                    PricePerHour = tier.PricePerHour,
                    Priority = 1,
                    EffectiveFrom = now,
                    IsActive = true,
                    CreatedAt = now
                };
                _db.Set<SlotPricing>().Add(pricing);
                result.Add(pricing);
            }

            return result;
        }

        private static ChargingSlotDto MapToDto(ChargingSlot slot, List<SlotPricing>? pricings = null)
        {
            return new ChargingSlotDto
            {
                Id = slot.Id,
                StationId = slot.StationId,
                SlotName = slot.SlotName,
                PositionX = slot.PositionX,
                PositionY = slot.PositionY,
                QrCodeToken = slot.QrCodeToken,
                Status = slot.Status.ToString(),
                CreatedAt = slot.CreatedAt,
                UpdatedAt = slot.UpdatedAt,
                PricingTiers = pricings?.Select(p => new SlotPricingDto
                {
                    Id = p.Id,
                    SlotId = p.SlotId,
                    StartTime = p.StartTime,
                    EndTime = p.EndTime,
                    PricePerHour = p.PricePerHour,
                    Priority = p.Priority,
                    EffectiveFrom = p.EffectiveFrom,
                    EffectiveTo = p.EffectiveTo,
                    IsActive = p.IsActive,
                    CreatedAt = p.CreatedAt
                }).ToList()
            };
        }
    }
}
