using ChargeSlot.Api.DTOs.Booking;
using ChargeSlot.Api.Enums;
using ChargeSlot.Api.Models;
using ChargeSlot.Api.Repositories.Interfaces;
using ChargeSlot.Api.Services.Interfaces;

namespace ChargeSlot.Api.Services.Implementation
{
    public class BookingService : IBookingService
    {
        private readonly IBookingRepository _bookingRepo;
        private readonly IChargingSlotRepository _slotRepo;
        private readonly INotificationService _notificationService;

        public BookingService(
            IBookingRepository bookingRepo,
            IChargingSlotRepository slotRepo,
            INotificationService notificationService)
        {
            _bookingRepo = bookingRepo;
            _slotRepo = slotRepo;
            _notificationService = notificationService;
        }

        /// <summary>
        /// Step 4-9: Driver sends booking request → System computes endTime,
        /// validates availability, creates booking (WaitingOwner)
        /// </summary>
        public async Task<BookingDto> CreateBookingAsync(int driverUserId, CreateBookingDto dto)
        {
            // Step 5: Compute end time
            var endTime = dto.StartTime.AddHours((double)dto.DurationHours);

            // Lấy slot để tính giá
            var slot = await _slotRepo.GetByIdAsync(dto.SlotId)
                ?? throw new InvalidOperationException("Slot không tồn tại.");

            if (slot.Status != SlotStatus.Active)
                throw new InvalidOperationException("Slot hiện không khả dụng.");

            // Step 6: Validate slot availability (check overlap)
            var hasOverlap = await _bookingRepo.HasOverlappingBookingAsync(
                dto.SlotId, dto.StartTime, endTime);

            // Step 7: Available?
            if (hasOverlap)
                throw new InvalidOperationException("Slot đã được đặt trong khung giờ này.");

            // Step 9: Create booking request (status = WaitingOwner)
            var totalAmount = slot.BasePricePerHour * dto.DurationHours;

            var booking = new Booking
            {
                DriverUserId = driverUserId,
                SlotId = dto.SlotId,
                StartTime = dto.StartTime,
                EndTime = endTime,
                DurationHours = dto.DurationHours,
                Note = dto.Note,
                TotalAmount = totalAmount,
                Status = BookingStatus.WaitingOwner,
                CreatedAt = DateTime.UtcNow
            };

            await _bookingRepo.CreateAsync(booking);

            // Notify Owner về booking mới
            var station = slot.ChargingStation;
            if (station != null)
            {
                await _notificationService.SendAsync(
                    station.OwnerUserId,
                    "Yêu cầu đặt chỗ mới",
                    $"Có yêu cầu đặt chỗ mới cho slot {slot.SlotName} từ {dto.StartTime:dd/MM/yyyy HH:mm} đến {endTime:dd/MM/yyyy HH:mm}.",
                    NotificationType.Booking);
            }

            // Reload with details
            var result = await _bookingRepo.GetByIdWithDetailsAsync(booking.Id);
            return MapToDto(result!);
        }

        /// <summary>
        /// Step 14: Owner Accept booking → Notify Driver → PendingPayment
        /// </summary>
        public async Task<BookingDto> AcceptBookingAsync(int ownerUserId, int bookingId)
        {
            var booking = await _bookingRepo.GetByIdWithDetailsAsync(bookingId)
                ?? throw new InvalidOperationException("Booking không tồn tại.");

            // Verify owner quyền
            if (booking.ChargingSlot.ChargingStation.OwnerUserId != ownerUserId)
                throw new UnauthorizedAccessException("Bạn không có quyền thao tác trên booking này.");

            if (booking.Status != BookingStatus.WaitingOwner)
                throw new InvalidOperationException("Booking không ở trạng thái chờ duyệt.");

            // Step 16: Set booking status = PendingPayment
            booking.Status = BookingStatus.PendingPayment;

            // Step 18: Compute payment deadline
            // Time to charging < 15 minutes? → countdown to charging time
            // Otherwise → 15 minutes timeout
            var timeToCharging = booking.StartTime - DateTime.UtcNow;
            if (timeToCharging.TotalMinutes < 15)
            {
                booking.PaymentExpiresAt = booking.StartTime;
            }
            else
            {
                booking.PaymentExpiresAt = DateTime.UtcNow.AddMinutes(15);
            }

            await _bookingRepo.UpdateAsync(booking);

            // Notify Driver: booking được chấp nhận
            await _notificationService.SendAsync(
                booking.DriverUserId,
                "Đặt chỗ được chấp nhận",
                $"Yêu cầu đặt chỗ #{booking.Id} đã được chấp nhận. Vui lòng thanh toán trước {booking.PaymentExpiresAt:dd/MM/yyyy HH:mm}.",
                NotificationType.Booking);

            return MapToDto(booking);
        }

        /// <summary>
        /// Step 12-13: Owner Reject booking → Provide rejection reason → Notify Driver → END
        /// </summary>
        public async Task<BookingDto> RejectBookingAsync(int ownerUserId, int bookingId, RejectBookingDto dto)
        {
            var booking = await _bookingRepo.GetByIdWithDetailsAsync(bookingId)
                ?? throw new InvalidOperationException("Booking không tồn tại.");

            if (booking.ChargingSlot.ChargingStation.OwnerUserId != ownerUserId)
                throw new UnauthorizedAccessException("Bạn không có quyền thao tác trên booking này.");

            if (booking.Status != BookingStatus.WaitingOwner)
                throw new InvalidOperationException("Booking không ở trạng thái chờ duyệt.");

            // Step 12: Reject + Step 13: Provide rejection reason
            booking.Status = BookingStatus.Rejected;
            booking.RejectionReason = dto.RejectionReason;
            await _bookingRepo.UpdateAsync(booking);

            // Send notify for Driver → END
            await _notificationService.SendAsync(
                booking.DriverUserId,
                "Đặt chỗ bị từ chối",
                $"Yêu cầu đặt chỗ #{booking.Id} đã bị từ chối. Lý do: {dto.RejectionReason}",
                NotificationType.Booking);

            return MapToDto(booking);
        }

        public async Task<BookingDto?> GetByIdAsync(int bookingId)
        {
            var booking = await _bookingRepo.GetByIdWithDetailsAsync(bookingId);
            return booking == null ? null : MapToDto(booking);
        }

        public async Task<List<BookingDto>> GetByDriverAsync(int driverUserId)
        {
            var bookings = await _bookingRepo.GetByDriverAsync(driverUserId);
            return bookings.Select(MapToDto).ToList();
        }

        public async Task<List<BookingDto>> GetByOwnerAsync(int ownerUserId)
        {
            var bookings = await _bookingRepo.GetByOwnerAsync(ownerUserId);
            return bookings.Select(MapToDto).ToList();
        }

        private static BookingDto MapToDto(Booking b)
        {
            return new BookingDto
            {
                Id = b.Id,
                DriverUserId = b.DriverUserId,
                DriverName = b.Driver?.User?.FullName ?? "",
                SlotId = b.SlotId,
                SlotName = b.ChargingSlot?.SlotName ?? "",
                StationId = b.ChargingSlot?.StationId ?? 0,
                StationName = b.ChargingSlot?.ChargingStation?.Name ?? "",
                StartTime = b.StartTime,
                EndTime = b.EndTime,
                DurationHours = b.DurationHours,
                TotalAmount = b.TotalAmount,
                Note = b.Note,
                Status = b.Status.ToString(),
                RejectionReason = b.RejectionReason,
                CancelReason = b.CancelReason,
                PaymentExpiresAt = b.PaymentExpiresAt,
                CreatedAt = b.CreatedAt
            };
        }
    }
}
