using ChargeSlot.Api.Data;
using ChargeSlot.Api.DTOs.Booking;
using ChargeSlot.Api.Enums;
using ChargeSlot.Api.Models;
using ChargeSlot.Api.Repositories.Interfaces;
using ChargeSlot.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ChargeSlot.Api.Services.Implementation
{
    public class BookingService : IBookingService
    {
        private readonly IBookingRepository _bookingRepo;
        private readonly IChargingSlotRepository _slotRepo;
        private readonly INotificationService _notificationService;
        private readonly ChargeSlotDbContext _context;

        public BookingService(
            IBookingRepository bookingRepo,
            IChargingSlotRepository slotRepo,
            INotificationService notificationService,
            ChargeSlotDbContext context)
        {
            _bookingRepo = bookingRepo;
            _slotRepo = slotRepo;
            _notificationService = notificationService;
            _context = context;
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

            if (slot.Status == SlotStatus.Inactive || slot.Status == SlotStatus.Maintenance)
                throw new InvalidOperationException("Slot hiện không khả dụng.");

            // Step 6: Validate slot availability (check overlap)
            var hasOverlap = await _bookingRepo.HasOverlappingBookingAsync(
                dto.SlotId, dto.StartTime, endTime);

            // Step 7: Available?
            if (hasOverlap)
                throw new InvalidOperationException("Slot đã được đặt trong khung giờ này.");

            // Không cho 1 driver book trùng giờ (dù khác slot)
            var driverOverlap = await _bookingRepo.HasDriverOverlappingBookingAsync(
                driverUserId, dto.StartTime, endTime);
            if (driverOverlap)
                throw new InvalidOperationException("Bạn đã có booking trùng khung giờ này. Vui lòng chọn giờ khác.");

            // Tính giá từ pricing tiers (station-level) — tách theo từng khung giờ
            // VD: booking 11h-14h, tier 5h-12h=10K + 12h-15h=12K → 1h×10K + 2h×12K = 34K
            var pricings = await _context.Set<StationPricing>()
                .Where(p => p.StationId == slot.StationId && p.IsActive)
                .OrderByDescending(p => p.Priority)
                .ThenBy(p => p.StartTime)
                .ToListAsync();

            if (pricings.Count == 0)
                throw new InvalidOperationException("Trạm chưa được cài đặt giá. Vui lòng liên hệ chủ trạm.");

            var totalAmount = CalculateTotalPrice(dto.StartTime, endTime, pricings);

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
        /// Step 14: Owner Accept booking → auto-reject overlapping → Notify Driver → PendingPayment
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

            // Check: đã có booking khác được accept trùng giờ trên slot này chưa?
            var hasConflict = await _bookingRepo.HasOverlappingBookingAsync(
                booking.SlotId, booking.StartTime, booking.EndTime, booking.Id);
            if (hasConflict)
                throw new InvalidOperationException("Slot đã có booking khác được chấp nhận trong khung giờ này.");

            // Step 16: Set booking status = PendingPayment
            booking.Status = BookingStatus.PendingPayment;

            // Step 18: Compute payment deadline (30 phút hoặc đến lúc sạc)
            var timeToCharging = booking.StartTime - DateTime.UtcNow;
            if (timeToCharging.TotalMinutes < 30)
            {
                booking.PaymentExpiresAt = booking.StartTime;
            }
            else
            {
                booking.PaymentExpiresAt = DateTime.UtcNow.AddMinutes(30);
            }

            await _bookingRepo.UpdateAsync(booking);

            // Auto-reject tất cả booking WaitingOwner trùng giờ trên cùng slot
            var overlapping = await _bookingRepo.GetOverlappingWaitingBookingsAsync(
                booking.SlotId, booking.StartTime, booking.EndTime, booking.Id);

            foreach (var b in overlapping)
            {
                b.Status = BookingStatus.Rejected;
                b.RejectionReason = "Slot đã được chấp nhận cho yêu cầu khác có giờ trùng.";
                await _bookingRepo.UpdateAsync(b);

                await _notificationService.SendAsync(
                    b.DriverUserId,
                    "Đặt chỗ bị từ chối",
                    $"Yêu cầu đặt chỗ #{b.Id} đã bị từ chối tự động do slot đã được chấp nhận cho yêu cầu khác có giờ trùng.",
                    NotificationType.Booking);
            }

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

        /// <summary>
        /// Tính tổng tiền booking bằng cách tách thời gian ra theo từng khung giá.
        /// VD: booking 11h-14h, tier 5h-12h=10K + 12h-15h=12K
        ///     → segment 11h-12h = 1h × 10K = 10K
        ///     → segment 12h-14h = 2h × 12K = 24K
        ///     → tổng = 34K
        /// </summary>
        private static decimal CalculateTotalPrice(DateTime startTime, DateTime endTime, List<StationPricing> pricings)
        {
            decimal total = 0;
            var current = startTime;

            while (current < endTime)
            {
                var currentTimeOnly = TimeOnly.FromDateTime(current);

                // Tìm tier phù hợp cho thời điểm hiện tại (ưu tiên priority cao nhất)
                var tier = pricings
                    .FirstOrDefault(p => currentTimeOnly >= p.StartTime && currentTimeOnly < p.EndTime);

                if (tier == null)
                {
                    // Fallback: dùng tier đầu tiên nếu không match
                    tier = pricings.First();
                }

                // Tính giờ kết thúc của segment = min(endTime, cuối tier ngày hôm đó)
                var tierEndToday = current.Date.Add(tier.EndTime.ToTimeSpan());

                // Nếu tier end = 23:59 nghĩa là đến hết ngày
                if (tier.EndTime == new TimeOnly(23, 59))
                    tierEndToday = current.Date.AddDays(1);

                var segmentEnd = endTime < tierEndToday ? endTime : tierEndToday;
                var hours = (decimal)(segmentEnd - current).TotalHours;

                if (hours > 0)
                {
                    total += hours * tier.PricePerHour;
                }

                current = segmentEnd;
            }

            // Làm tròn đến hàng đơn vị
            return Math.Round(total, 0);
        }
    }
}
