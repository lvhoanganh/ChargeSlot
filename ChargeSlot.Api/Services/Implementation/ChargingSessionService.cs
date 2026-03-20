using ChargeSlot.Api.DTOs.Booking;
using ChargeSlot.Api.DTOs.ChargingSession;
using ChargeSlot.Api.DTOs.Invoice;
using ChargeSlot.Api.Enums;
using ChargeSlot.Api.Models;
using ChargeSlot.Api.Repositories.Interfaces;
using ChargeSlot.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ChargeSlot.Api.Services.Implementation
{
    public class ChargingSessionService : IChargingSessionService
    {
        private readonly IChargingSessionRepository _sessionRepo;
        private readonly IInvoiceRepository _invoiceRepo;
        private readonly IBookingRepository _bookingRepo;
        private readonly IChargingSlotRepository _slotRepo;
        private readonly INotificationService _notificationService;
        private readonly Data.ChargeSlotDbContext _db;

        // Business constants
        private const int CheckInWindowMinutes = 15;
        private const decimal VatRate = 0.08m;       // 8%
        private const decimal PlatformFeeRate = 0.05m; // 5%

        public ChargingSessionService(
            IChargingSessionRepository sessionRepo,
            IInvoiceRepository invoiceRepo,
            IBookingRepository bookingRepo,
            IChargingSlotRepository slotRepo,
            INotificationService notificationService,
            Data.ChargeSlotDbContext db)
        {
            _sessionRepo = sessionRepo;
            _invoiceRepo = invoiceRepo;
            _bookingRepo = bookingRepo;
            _slotRepo = slotRepo;
            _notificationService = notificationService;
            _db = db;
        }

        /// <summary>
        /// Driver scans QR code on slot → system finds matching Paid booking → check in.
        /// Validates: slot exists, booking is Paid, time window ±15 min.
        /// </summary>
        public async Task<ChargingSessionDto> CheckInAsync(int driverUserId, string qrCodeToken)
        {
            // 1. Find slot by QR token
            var slot = await _db.ChargingSlots
                .Include(s => s.ChargingStation)
                .FirstOrDefaultAsync(s => s.QrCodeToken == qrCodeToken)
                ?? throw new InvalidOperationException("QR code không hợp lệ.");

            // 2. Find Paid booking for this driver on this slot
            var now = DateTime.UtcNow;
            var booking = await _db.Bookings
                .Include(b => b.Driver).ThenInclude(d => d.User)
                .Include(b => b.ChargingSlot).ThenInclude(s => s.ChargingStation)
                .FirstOrDefaultAsync(b =>
                    b.DriverUserId == driverUserId
                    && b.SlotId == slot.Id
                    && b.Status == BookingStatus.Paid)
                ?? throw new InvalidOperationException("Không tìm thấy booking đã thanh toán trên slot này.");

            // 3. Validate time window: now must be within ±15 min of StartTime
            var earliestCheckin = booking.StartTime.AddMinutes(-CheckInWindowMinutes);
            var latestCheckin = booking.StartTime.AddMinutes(CheckInWindowMinutes);
            if (now < earliestCheckin)
                throw new InvalidOperationException($"Chưa đến giờ check-in. Vui lòng quay lại lúc {earliestCheckin:HH:mm dd/MM/yyyy} UTC.");
            if (now > latestCheckin)
                throw new InvalidOperationException("Đã quá thời gian check-in cho booking này.");

            // 4. Update booking status
            booking.Status = BookingStatus.CheckedIn;
            booking.CheckedInAt = now;
            await _bookingRepo.UpdateAsync(booking);

            // 5. Create charging session
            var session = new ChargingSession
            {
                BookingId = booking.Id,
                CheckinTime = now,
                ActualStartTime = now,
                CreatedAt = now
            };
            await _sessionRepo.CreateAsync(session);

            // 6. Update slot status to Booked
            var slotEntity = await _slotRepo.GetByIdAsync(slot.Id, tracking: true);
            if (slotEntity != null)
            {
                slotEntity.Status = SlotStatus.Booked;
                slotEntity.UpdatedAt = now;
                await _slotRepo.SaveChangesAsync();
            }

            // 7. Notify Owner
            await _notificationService.SendAsync(
                slot.ChargingStation.OwnerUserId,
                "Driver đã check-in",
                $"Driver {booking.Driver?.User?.FullName ?? ""} đã check-in tại slot {slot.SlotName}.",
                NotificationType.Booking);

            return MapToDto(session, booking);
        }

        /// <summary>
        /// Owner stops charging session → create invoice from booking TotalAmount → notify driver.
        /// </summary>
        public async Task<ChargingSessionDto> StopChargingAsync(int ownerUserId, int sessionId)
        {
            var session = await _sessionRepo.GetByIdWithDetailsAsync(sessionId)
                ?? throw new InvalidOperationException("Session không tồn tại.");

            var booking = session.Booking;

            // Validate owner
            if (booking.ChargingSlot.ChargingStation.OwnerUserId != ownerUserId)
                throw new UnauthorizedAccessException("Bạn không có quyền thao tác trên session này.");

            // Validate status
            if (booking.Status != BookingStatus.CheckedIn)
                throw new InvalidOperationException("Booking không ở trạng thái CheckedIn.");

            var now = DateTime.UtcNow;

            // Update session
            session.ActualEndTime = now;
            session.ActualDurationHours = (decimal)(now - (session.ActualStartTime ?? now)).TotalHours;
            await _sessionRepo.UpdateAsync(session);

            // Update booking → CompletedPendingInvoice (= WaitingDriverConfirm)
            booking.Status = BookingStatus.CompletedPendingInvoice;
            await _bookingRepo.UpdateAsync(booking);

            // Create invoice from pre-calculated booking amount
            var chargingAmount = booking.TotalAmount;
            var vatAmount = Math.Round(chargingAmount * VatRate, 0);
            var platformFee = Math.Round(chargingAmount * PlatformFeeRate, 0);
            var totalInvoice = chargingAmount + vatAmount + platformFee;

            var invoice = new Invoice
            {
                BookingId = booking.Id,
                ChargingAmount = chargingAmount,
                ServiceAmount = 0, // TODO: calculate from BookingExtraServices
                VatAmount = vatAmount,
                PlatformFee = platformFee,
                TotalAmount = totalInvoice,
                Status = InvoiceStatus.PendingConfirm,
                CreatedAt = now
            };
            await _invoiceRepo.CreateAsync(invoice);

            // Release slot → Active
            var slot = await _slotRepo.GetByIdAsync(booking.SlotId, tracking: true);
            if (slot != null)
            {
                slot.Status = SlotStatus.Active;
                slot.UpdatedAt = now;
                await _slotRepo.SaveChangesAsync();
            }

            // Notify Driver
            await _notificationService.SendAsync(
                booking.DriverUserId,
                "Phiên sạc đã kết thúc",
                $"Phiên sạc #{session.Id} đã kết thúc. Vui lòng xác nhận hóa đơn {totalInvoice:N0}đ.",
                NotificationType.Booking);

            return MapToDto(session, booking);
        }

        /// <summary>
        /// Driver confirms invoice → booking = Completed.
        /// </summary>
        public async Task<BookingDto> ConfirmCompletionAsync(int driverUserId, int sessionId)
        {
            var session = await _sessionRepo.GetByIdWithDetailsAsync(sessionId)
                ?? throw new InvalidOperationException("Session không tồn tại.");

            var booking = session.Booking;

            if (booking.DriverUserId != driverUserId)
                throw new InvalidOperationException("Booking này không thuộc về bạn.");

            if (booking.Status != BookingStatus.CompletedPendingInvoice)
                throw new InvalidOperationException("Booking không ở trạng thái chờ xác nhận.");

            // Confirm invoice
            var invoice = await _invoiceRepo.GetByBookingIdAsync(booking.Id);
            if (invoice != null)
            {
                invoice.Status = InvoiceStatus.Confirmed;
                await _invoiceRepo.UpdateAsync(invoice);
            }

            // Complete booking
            booking.Status = BookingStatus.Completed;
            await _bookingRepo.UpdateAsync(booking);

            // Notify Owner
            var station = booking.ChargingSlot?.ChargingStation;
            if (station != null)
            {
                await _notificationService.SendAsync(
                    station.OwnerUserId,
                    "Driver đã xác nhận hoàn thành",
                    $"Booking #{booking.Id} đã được xác nhận hoàn thành.",
                    NotificationType.Booking);
            }

            return MapToBookingDto(booking);
        }

        public async Task<ChargingSessionDto?> GetByBookingIdAsync(int bookingId)
        {
            var session = await _sessionRepo.GetByBookingIdAsync(bookingId);
            if (session == null) return null;
            return MapToDto(session, session.Booking);
        }

        public async Task<List<ChargingSessionDto>> GetActiveByOwnerAsync(int ownerUserId)
        {
            var sessions = await _sessionRepo.GetActiveByOwnerAsync(ownerUserId);
            return sessions.Select(s => MapToDto(s, s.Booking)).ToList();
        }

        public async Task<InvoiceDto?> GetInvoiceByBookingIdAsync(int bookingId)
        {
            var invoice = await _invoiceRepo.GetByBookingIdAsync(bookingId);
            if (invoice == null) return null;
            return MapToInvoiceDto(invoice);
        }

        // ─────────────── MAPPING ───────────────

        private static ChargingSessionDto MapToDto(ChargingSession session, Booking booking)
        {
            return new ChargingSessionDto
            {
                Id = session.Id,
                BookingId = session.BookingId,
                SlotId = booking.SlotId,
                SlotName = booking.ChargingSlot?.SlotName ?? "",
                StationId = booking.ChargingSlot?.StationId ?? 0,
                StationName = booking.ChargingSlot?.ChargingStation?.Name ?? "",
                DriverName = booking.Driver?.User?.FullName ?? "",
                CheckinTime = session.CheckinTime,
                ActualStartTime = session.ActualStartTime,
                ActualEndTime = session.ActualEndTime,
                ActualDurationHours = session.ActualDurationHours,
                BookingStartTime = booking.StartTime,
                BookingEndTime = booking.EndTime,
                TotalAmount = booking.TotalAmount,
                BookingStatus = booking.Status.ToString(),
                CreatedAt = session.CreatedAt
            };
        }

        private static BookingDto MapToBookingDto(Booking b)
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

        private static InvoiceDto MapToInvoiceDto(Invoice invoice)
        {
            return new InvoiceDto
            {
                Id = invoice.Id,
                BookingId = invoice.BookingId,
                ChargingAmount = invoice.ChargingAmount,
                ServiceAmount = invoice.ServiceAmount,
                VatAmount = invoice.VatAmount,
                PlatformFee = invoice.PlatformFee,
                TotalAmount = invoice.TotalAmount,
                Status = invoice.Status.ToString(),
                CreatedAt = invoice.CreatedAt,
                UpdatedAt = invoice.UpdatedAt
            };
        }
    }
}
