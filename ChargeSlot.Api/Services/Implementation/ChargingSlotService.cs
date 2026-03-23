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
        private readonly IBookingRepository _bookingRepo;
        private readonly ChargeSlotDbContext _db;

        private const int BufferMinutes = 15;

        public ChargingSlotService(
            IChargingSlotRepository slotRepo,
            IChargingStationRepository stationRepo,
            IBookingRepository bookingRepo,
            ChargeSlotDbContext db)
        {
            _slotRepo = slotRepo;
            _stationRepo = stationRepo;
            _bookingRepo = bookingRepo;
            _db = db;
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
            await ValidateStationOwnershipAsync(stationId, ownerUserId);

            var slot = await _slotRepo.GetByIdAsync(slotId, tracking: true);
            if (slot == null || slot.StationId != stationId)
                throw new KeyNotFoundException($"Slot {slotId} not found in station {stationId}.");

            slot.SlotName = dto.SlotName;
            slot.PositionX = dto.PositionX;
            slot.PositionY = dto.PositionY;
            slot.UpdatedAt = DateTime.UtcNow;

            await _slotRepo.SaveChangesAsync();
        }

        public async Task DeleteAsync(int stationId, int slotId, int ownerUserId)
        {
            await ValidateStationOwnershipAsync(stationId, ownerUserId);

            var slot = await _slotRepo.GetByIdAsync(slotId, tracking: true);
            if (slot == null || slot.StationId != stationId)
                throw new KeyNotFoundException($"Slot {slotId} not found in station {stationId}.");

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

        /// <summary>
        /// Lấy thông tin availability của slot cho ngày cụ thể.
        /// Trả về các khung giờ đã đặt (bao gồm 15 phút buffer) và NextAvailableAt.
        /// </summary>
        public async Task<SlotAvailabilityDto> GetSlotAvailabilityAsync(int slotId, DateTime date)
        {
            var slot = await _slotRepo.GetByIdAsync(slotId)
                ?? throw new KeyNotFoundException($"Slot {slotId} not found.");

            var bookings = await _bookingRepo.GetActiveBookingsForSlotAsync(slotId, date);

            var bookedRanges = bookings.Select(b => new BookedTimeRangeDto
            {
                StartTime = b.StartTime,
                EndTime = b.EndTime.AddMinutes(BufferMinutes), // Bao gồm 15 phút buffer
                Status = b.Status.ToString()
            }).ToList();

            // Tính NextAvailableAt: thời gian trống tiếp theo từ bây giờ
            DateTime? nextAvailable = null;
            var now = DateTime.UtcNow;

            if (bookedRanges.Count == 0)
            {
                nextAvailable = now; // Slot trống ngay
            }
            else
            {
                // Tìm khung trống đầu tiên từ bây giờ
                foreach (var range in bookedRanges.OrderBy(r => r.StartTime))
                {
                    if (range.StartTime > now && (nextAvailable == null || range.StartTime > nextAvailable))
                    {
                        // Có khoảng trống trước booking này
                        nextAvailable = now > (nextAvailable ?? now) ? nextAvailable : now;
                        break;
                    }
                    if (range.EndTime > now)
                    {
                        // Đang trong khoảng booking này
                        nextAvailable = range.EndTime;
                    }
                }

                // Nếu tất cả booking đều trước now → slot trống ngay
                if (nextAvailable == null || nextAvailable < now)
                    nextAvailable = now;
            }

            return new SlotAvailabilityDto
            {
                SlotId = slot.Id,
                SlotName = slot.SlotName,
                Status = slot.Status.ToString(),
                BookedRanges = bookedRanges,
                NextAvailableAt = nextAvailable
            };
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

        private static ChargingSlotDto MapToDto(ChargingSlot slot)
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
                UpdatedAt = slot.UpdatedAt
            };
        }
    }
}
