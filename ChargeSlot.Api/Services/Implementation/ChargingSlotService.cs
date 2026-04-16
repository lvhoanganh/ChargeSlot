using ChargeSlot.Api.DTOs.Slot;
using ChargeSlot.Api.Enums;
using ChargeSlot.Api.Models;
using ChargeSlot.Api.Repositories.Interfaces;
using ChargeSlot.Api.Services.Interfaces;
using ChargeSlot.Api.Helpers;

namespace ChargeSlot.Api.Services.Implementation
{
    public class ChargingSlotService : IChargingSlotService
    {
        private readonly IChargingSlotRepository _slotRepo;
        private readonly IChargingStationRepository _stationRepo;
        private readonly IBookingRepository _bookingRepo;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ISystemConfigService _configService;

        public ChargingSlotService(
            IChargingSlotRepository slotRepo,
            IChargingStationRepository stationRepo,
            IBookingRepository bookingRepo,
            IUnitOfWork unitOfWork,
            ISystemConfigService configService)
        {
            _slotRepo = slotRepo;
            _stationRepo = stationRepo;
            _bookingRepo = bookingRepo;
            _unitOfWork = unitOfWork;
            _configService = configService;
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

        public async Task<ChargeSlot.Api.DTOs.PagedResultDto<ChargingSlotDto>> GetAllByStationPagedAsync(int stationId, int ownerUserId, int page, int pageSize)
        {
            await ValidateStationOwnershipAsync(stationId, ownerUserId);

            var result = await _slotRepo.GetAllByStationPagedAsync(stationId, page, pageSize);
            return new ChargeSlot.Api.DTOs.PagedResultDto<ChargingSlotDto>
            {
                Page = page,
                PageSize = pageSize,
                TotalItems = result.TotalCount,
                Items = result.Items.Select(MapToDto).ToList()
            };
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
                CreatedAt = DateTimeHelper.VietnamNow()
            };

            await _slotRepo.AddAsync(slot);
            await _unitOfWork.CompleteAsync();

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
            slot.UpdatedAt = DateTimeHelper.VietnamNow();

            await _unitOfWork.CompleteAsync();
        }

        public async Task DeleteAsync(int stationId, int slotId, int ownerUserId)
        {
            var station = await _stationRepo.GetByIdAsync(stationId, includeDetails: false);
            if (station == null)
                throw new KeyNotFoundException($"Station {stationId} not found.");
            if (station.OwnerUserId != ownerUserId)
                throw new UnauthorizedAccessException("You do not own this station.");

            var slot = await _slotRepo.GetByIdAsync(slotId, tracking: true);
            if (slot == null || slot.StationId != stationId)
                throw new KeyNotFoundException($"Slot {slotId} not found in station {stationId}.");

            if (station.ApprovalStatus != ApprovalStatus.Draft && station.ApprovalStatus != ApprovalStatus.Rejected)
            {
                var hasBookings = await _bookingRepo.HasAnyBookingsAsync(slotId);
                if (hasBookings)
                {
                    throw new InvalidOperationException("Không thể xóa khoảng sạc/slot này vì đã từng có booking liên quan. Bạn chỉ có thể chuyển sang trạng thái ngưng hoạt động (Inactive) thay vì xóa.");
                }
            }

            _slotRepo.Remove(slot);
            await _unitOfWork.CompleteAsync();
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

            // Check if changing to Inactive or Maintenance while there are upcoming bookings
            if (dto.Status == SlotStatus.Inactive || dto.Status == SlotStatus.Maintenance)
            {
                var hasActiveBookings = await _bookingRepo.HasActiveBookingsAsync(slotId);
                                
                if (hasActiveBookings)
                {
                    throw new InvalidOperationException(
                        $"Không thể chuyển slot sang {dto.Status} vì đang có booking sắp tới hoặc đang sạc. Vui lòng hủy các booking này trước.");
                }
            }
            else if (slot.Status == SlotStatus.Booked)
            {
                // If not changing to Inactive/Maintenance, but it is currently Booked (means currently charging or paid check-in), 
                // we probably shouldn't allow manually turning it to Active if someone is using it.
                // Well, if they want to override, they can if it's not Maintenance.
                // Let's just keep the old basic lock if they try to bypass.
                throw new InvalidOperationException(
                    "Cannot change status of a slot that is currently Booked. Wait for the booking to complete or expire.");
            }

            slot.Status = dto.Status;
            slot.UpdatedAt = DateTimeHelper.VietnamNow();

            await _unitOfWork.CompleteAsync();
        }

        public async Task<string> RegenerateQrCodeAsync(int stationId, int slotId, int ownerUserId)
        {
            var station = await _stationRepo.GetByIdAsync(stationId, includeDetails: false);
            if (station == null)
                throw new KeyNotFoundException($"Station {stationId} not found.");
            if (station.OwnerUserId != ownerUserId)
                throw new UnauthorizedAccessException("You do not own this station.");

            if (station.ApprovalStatus != ApprovalStatus.Approved)
            {
                throw new InvalidOperationException(
                    $"Cannot regenerate QR code when station is in '{station.ApprovalStatus}' status. Station must be Approved.");
            }

            var slot = await _slotRepo.GetByIdAsync(slotId, tracking: true);
            if (slot == null || slot.StationId != stationId)
                throw new KeyNotFoundException($"Slot {slotId} not found in station {stationId}.");

            slot.QrCodeToken = Guid.NewGuid().ToString("N");
            slot.UpdatedAt = DateTimeHelper.VietnamNow();

            await _unitOfWork.CompleteAsync();
            return slot.QrCodeToken;
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

            var slotConfigs = await _configService.GetCurrentConfigsAsync();
            var bufferMinutes = slotConfigs.Slot_Buffer_Minutes;

            var bookedRanges = bookings.Select(b => new BookedTimeRangeDto
            {
                StartTime = b.StartTime,
                EndTime = (b.ChargingSession != null && b.ChargingSession.ActualEndTime.HasValue 
                            ? b.ChargingSession.ActualEndTime.Value 
                            : b.EndTime).AddMinutes(bufferMinutes), // Bao gồm buffer từ config
                Status = b.Status.ToString()
            }).ToList();

            // Tính NextAvailableAt: thời gian trống tiếp theo từ bây giờ
            DateTime? nextAvailable = null;
            var now = DateTimeHelper.VietnamNow();

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

