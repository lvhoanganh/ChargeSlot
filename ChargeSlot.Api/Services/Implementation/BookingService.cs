using ChargeSlot.Api.Helpers;
using ChargeSlot.Api.Data;
using ChargeSlot.Api.DTOs.Booking;
using ChargeSlot.Api.Enums;
using ChargeSlot.Api.Models;
using ChargeSlot.Api.Repositories.Interfaces;
using ChargeSlot.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

using ChargeSlot.Api.Helpers;
namespace ChargeSlot.Api.Services.Implementation
{
    public class BookingService : IBookingService
    {
        private readonly IBookingRepository _bookingRepo;
        private readonly IChargingSlotRepository _slotRepo;
        private readonly INotificationService _notificationService;
        private readonly IWalletRepository _walletRepo;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IStationUnavailableDateRepository _unavailableDateRepo;
        private readonly IStationPricingRepository _pricingRepo;
        private readonly IExtraServiceRepository _extraServiceRepo;
        private readonly IDriverRepository _driverRepo;
        private readonly ILoyaltyTransactionRepository _loyaltyRepo;
        private readonly ILedgerTransactionRepository _ledgerRepo;
        private readonly ILogger<BookingService> _logger;
        private readonly ISystemConfigService _configService;

        public BookingService(
            IBookingRepository bookingRepo,
            IChargingSlotRepository slotRepo,
            INotificationService notificationService,
            IWalletRepository walletRepo,
            IUnitOfWork unitOfWork,
            IStationUnavailableDateRepository unavailableDateRepo,
            IStationPricingRepository pricingRepo,
            IExtraServiceRepository extraServiceRepo,
            IDriverRepository driverRepo,
            ILoyaltyTransactionRepository loyaltyRepo,
            ILedgerTransactionRepository ledgerRepo,
            ILogger<BookingService> logger,
            ISystemConfigService configService)
        {
            _bookingRepo = bookingRepo;
            _slotRepo = slotRepo;
            _notificationService = notificationService;
            _walletRepo = walletRepo;
            _unitOfWork = unitOfWork;
            _unavailableDateRepo = unavailableDateRepo;
            _pricingRepo = pricingRepo;
            _extraServiceRepo = extraServiceRepo;
            _driverRepo = driverRepo;
            _loyaltyRepo = loyaltyRepo;
            _ledgerRepo = ledgerRepo;
            _logger = logger;
            _configService = configService;
        }

        /// <summary>
        /// Step 4-9: Driver sends booking request → System computes endTime,
        /// validates availability, creates booking (WaitingOwner)
        /// </summary>
        public async Task<BookingDto> CreateBookingAsync(int driverUserId, CreateBookingDto dto)
        {
            using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                // Validate: DurationHours phải > 0 và <= 24
                if (dto.DurationHours <= 0)
                    throw new InvalidOperationException("Thời lượng sạc phải lớn hơn 0.");
                if (dto.DurationHours > 24)
                    throw new InvalidOperationException("Thời lượng sạc tối đa là 24 giờ.");
                if (dto.DurationHours % 0.5m != 0)
                    throw new InvalidOperationException("Thời lượng sạc phải là bội số của 30 phút (VD: 0.5h, 1h, 1.5h).");

                // Validate: StartTime block scheduling (chỉ nhận phút 00 hoặc 30)
                if (dto.StartTime.Minute != 0 && dto.StartTime.Minute != 30)
                    throw new InvalidOperationException("Giờ bắt đầu sạc bắt buộc phải chẵn theo block 30 phút (VD: 10:00, 10:30).");
                if (dto.StartTime.Second != 0 || dto.StartTime.Millisecond != 0)
                    throw new InvalidOperationException("Hệ thống chỉ nhận khung giờ chẵn tới mức giây (00s). Không nhận giờ phân mảnh.");

                // Cấp khóa lock đồng bộ (Prevent Simultaneous Double Booking)
                var lockResource = $"SlotLock_{dto.SlotId}";
                await _unitOfWork.ExecuteSqlRawSafeAsync(
                    "EXEC sp_getapplock @Resource = {0}, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 5000",
                    lockResource);

                // Validate: StartTime phải trong tương lai và cách hiện tại ít nhất 30 phút
                var minutesUntilStart = (dto.StartTime - DateTimeHelper.VietnamNow()).TotalMinutes;
                if (minutesUntilStart <= 0)
                    throw new InvalidOperationException("Thời gian bắt đầu phải trong tương lai.");
                if (minutesUntilStart < 30)
                    throw new InvalidOperationException("Phải đặt trước ít nhất 30 phút trước giờ sạc.");

                // Validate: Driver chỉ được có tối đa 3 booking đang chờ xử lý
                var pendingCount = await _bookingRepo.GetPendingCountByDriverAsync(driverUserId);
                if (pendingCount >= 3)
                    throw new InvalidOperationException("Bạn đang có 3 booking chờ xử lý. Vui lòng hoàn tất hoặc hủy bớt trước khi đặt mới.");

                // Step 5: Compute end time
                var endTime = dto.StartTime.AddHours((double)dto.DurationHours);

                // Lấy slot để tính giá
                var slot = await _slotRepo.GetByIdAsync(dto.SlotId)
                    ?? throw new InvalidOperationException("Slot không tồn tại.");

                if (slot.Status == SlotStatus.Inactive || slot.Status == SlotStatus.Maintenance)
                    throw new InvalidOperationException("Slot hiện không khả dụng.");

                // Validate station is approved and operational
                if (slot.ChargingStation != null)
                {
                    if (slot.ChargingStation.ApprovalStatus != ApprovalStatus.Approved)
                        throw new InvalidOperationException("Trạm sạc chưa được phê duyệt hoạt động.");
                    if (slot.ChargingStation.OperationalStatus == OperationalStatus.Inactive)
                        throw new InvalidOperationException("Trạm sạc hiện đang ngừng hoạt động.");

                    // Check StationUnavailableDates
                    var bookingStartDate = DateOnly.FromDateTime(dto.StartTime);
                    var bookingEndDate = DateOnly.FromDateTime(endTime);
                    var unavailableDates = await _unavailableDateRepo.GetDatesByStationAndDateRangeAsync(
                        slot.StationId, bookingStartDate, bookingEndDate);

                    if (unavailableDates.Count > 0)
                    {
                        var datesStr = string.Join(", ", unavailableDates.Select(d => d.ToString("dd/MM/yyyy")));
                        throw new InvalidOperationException($"Trạm sạc đóng cửa/bảo trì vào các ngày: {datesStr}. Vui lòng chọn thời gian khác.");
                    }
                }

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
            var pricings = await _pricingRepo.GetActiveByStationIdAsync(slot.StationId);

            if (pricings.Count == 0)
                throw new InvalidOperationException("Trạm chưa được cài đặt giá. Vui lòng liên hệ chủ trạm.");

            var totalAmount = CalculateTotalPrice(dto.StartTime, endTime, pricings);

            // ── Validate & create ExtraServices (topping) ──
            decimal serviceAmount = 0;
            var extraServiceRecords = new List<BookingExtraService>();

            if (dto.ExtraServices != null && dto.ExtraServices.Count > 0)
            {
                var serviceIds = dto.ExtraServices.Select(e => e.ServiceId).ToList();
                var services = await _extraServiceRepo.GetByIdsAsync(serviceIds);

                foreach (var item in dto.ExtraServices)
                {
                    var svc = services.FirstOrDefault(s => s.Id == item.ServiceId)
                        ?? throw new InvalidOperationException($"Dịch vụ #{item.ServiceId} không tồn tại.");

                    if (svc.StationId != slot.StationId)
                        throw new InvalidOperationException($"Dịch vụ '{svc.ServiceName}' không thuộc trạm này.");

                    if (!svc.IsActive)
                        throw new InvalidOperationException($"Dịch vụ '{svc.ServiceName}' hiện không khả dụng.");

                    if (svc.TotalStock.HasValue && svc.TotalStock.Value < item.Quantity)
                        throw new InvalidOperationException($"Dịch vụ '{svc.ServiceName}' chỉ còn {svc.TotalStock} — không đủ {item.Quantity}.");

                    var unitPrice = svc.Price;
                    var totalPrice = unitPrice * item.Quantity;
                    serviceAmount += totalPrice;

                    extraServiceRecords.Add(new BookingExtraService
                    {
                        ServiceId = item.ServiceId,
                        Quantity = item.Quantity,
                        UnitPrice = unitPrice,
                        TotalPrice = totalPrice
                    });
                }
            }

            totalAmount += serviceAmount;

            // ── Loyalty Points redemption ──
            decimal pointsUsed = 0;
            decimal pointsDiscountAmount = 0;

            if (dto.PointsToUse > 0)
            {
                var driver = await _driverRepo.GetByUserIdAsync(driverUserId, tracking: true)
                    ?? throw new InvalidOperationException("Driver profile không tồn tại.");

                if (dto.PointsToUse > driver.LoyaltyPoints)
                    throw new InvalidOperationException(
                        $"Bạn chỉ có {driver.LoyaltyPoints:N0} điểm, không đủ {dto.PointsToUse:N0} điểm.");

                // Load max redeem rate from config
                var maxRedeemRate = await _configService.GetDecimalAsync("LoyaltyMaxRedeemRate", 0.5m);
                var maxPointsAllowed = Math.Floor(totalAmount * maxRedeemRate);

                if (dto.PointsToUse > maxPointsAllowed)
                    throw new InvalidOperationException(
                        $"Tối đa được dùng {maxPointsAllowed:N0} điểm ({maxRedeemRate * 100:N0}% của {totalAmount:N0}đ).");

                pointsUsed = dto.PointsToUse;
                pointsDiscountAmount = pointsUsed; // 1 điểm = 1 VND
                totalAmount -= pointsDiscountAmount;

                // Trừ điểm Driver
                driver.LoyaltyPoints -= pointsUsed;

                // Ghi lịch sử
                _loyaltyRepo.Add(new LoyaltyTransaction
                {
                    DriverUserId = driverUserId,
                    Type = "Redeem",
                    Points = pointsUsed,
                    Description = $"Dùng {pointsUsed:N0} điểm cho booking slot {dto.SlotId}",
                    CreatedAt = DateTimeHelper.VietnamNow()
                });
            }

            var configs = await _configService.GetCurrentConfigsAsync();

            var booking = new Booking
            {
                DriverUserId = driverUserId,
                SlotId = dto.SlotId,
                StartTime = dto.StartTime,
                EndTime = endTime,
                DurationHours = dto.DurationHours,
                Note = dto.Note,
                TotalAmount = totalAmount,
                PointsUsed = pointsUsed,
                PointsDiscountAmount = pointsDiscountAmount,
                Status = BookingStatus.WaitingOwner,
                BookingExtraServices = extraServiceRecords,
                CreatedAt = DateTimeHelper.VietnamNow(),

                // Snapshots
                Refund100DeadlineAt = dto.StartTime.AddHours(-configs.RefundPolicy100_Hrs),
                Refund50DeadlineAt = dto.StartTime.AddHours(-configs.RefundPolicy50_Hrs),
                PlatformFeeRateSnapshot = configs.Platform_Fee_Rate,
                VatRateSnapshot = configs.VAT_Rate,
                LoyaltyEarnRateSnapshot = configs.Loyalty_Earn_Rate
            };

            _bookingRepo.Add(booking);
            await _unitOfWork.CompleteAsync();

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
                
                await transaction.CommitAsync();
                return MapToDto(result!);
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        /// <summary>
        /// Step 14: Owner Accept booking → auto-reject overlapping → Notify Driver → PendingPayment
        /// </summary>
        public async Task<BookingDto> AcceptBookingAsync(int ownerUserId, int bookingId)
        {
            using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                var booking = await _bookingRepo.GetByIdWithDetailsAsync(bookingId)
                    ?? throw new InvalidOperationException("Booking không tồn tại.");

                // Lock độc quyền trên Slot này để chống việc Accept 2 Booking cùng 1 lúc (Double-Booking Race Condition)
                var lockResource = $"SlotLock_{booking.SlotId}";
                await _unitOfWork.ExecuteSqlRawSafeAsync(
                    "EXEC sp_getapplock @Resource = {0}, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 5000",
                    lockResource);

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

            // Step 18: Compute payment deadline
            var configs = await _configService.GetCurrentConfigsAsync();
            var paymentExpiryMinutes = configs.Payment_Expiry_Minutes;

            var timeToCharging = booking.StartTime - DateTimeHelper.VietnamNow();
            if (timeToCharging.TotalMinutes < paymentExpiryMinutes)
            {
                booking.PaymentExpiresAt = booking.StartTime;
            }
            else
            {
                booking.PaymentExpiresAt = DateTimeHelper.VietnamNow().AddMinutes(paymentExpiryMinutes);
            }

            _bookingRepo.Update(booking);
            await _unitOfWork.CompleteAsync();

            // Auto-reject tất cả booking WaitingOwner trùng giờ trên cùng slot
            var overlapping = await _bookingRepo.GetOverlappingWaitingBookingsAsync(
                booking.SlotId, booking.StartTime, booking.EndTime, booking.Id);

            foreach (var b in overlapping)
            {
                b.Status = BookingStatus.Rejected;
                b.RejectionReason = "Slot đã được chấp nhận cho yêu cầu khác có giờ trùng.";
                _bookingRepo.Update(b);
            await _unitOfWork.CompleteAsync();

                await _notificationService.SendAsync(
                    b.DriverUserId,
                    "Đặt chỗ bị từ chối",
                    $"Yêu cầu đặt chỗ tại slot {booking.ChargingSlot?.SlotName} — trạm {booking.ChargingSlot?.ChargingStation?.Name} đã bị từ chối tự động do slot đã được chấp nhận cho khách khác.",
                    NotificationType.Booking);
            }

                // Notify Driver: booking được chấp nhận
                await _notificationService.SendAsync(
                    booking.DriverUserId,
                    "Đặt chỗ được chấp nhận",
                    $"Yêu cầu đặt chỗ tại slot {booking.ChargingSlot?.SlotName} — trạm {booking.ChargingSlot?.ChargingStation?.Name} ({booking.StartTime:HH:mm} - {booking.EndTime:HH:mm dd/MM}) đã được chấp nhận. Vui lòng thanh toán {booking.TotalAmount:N0}đ trước {booking.PaymentExpiresAt:HH:mm dd/MM}.",
                    NotificationType.Booking);

                await transaction.CommitAsync();
                return MapToDto(booking);
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
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
            _bookingRepo.Update(booking);
            await _unitOfWork.CompleteAsync();

            // Send notify for Driver → END
            await _notificationService.SendAsync(
                booking.DriverUserId,
                "Đặt chỗ bị từ chối",
                $"Yêu cầu đặt chỗ tại slot {booking.ChargingSlot?.SlotName} — trạm {booking.ChargingSlot?.ChargingStation?.Name} ({booking.StartTime:HH:mm} - {booking.EndTime:HH:mm dd/MM}) bị từ chối. Lý do: {dto.RejectionReason}",
                NotificationType.Booking);

            return MapToDto(booking);
        }

        // ═══════════════════════════════════════════════════════
        // CANCEL BOOKING
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// Driver hủy booking.
        /// - WaitingOwner / PendingPayment → hủy miễn phí (chưa trả tiền)
        /// - Paid → hoàn tiền theo chính sách: ≥2h=100%, 1-2h=50%, &lt;1h=0%
        /// </summary>
        public async Task<BookingDto> DriverCancelBookingAsync(int driverUserId, int bookingId, string? cancelReason)
        {
            // FIX: Transaction bảo vệ refund + restore stock + refund points
            using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                var booking = await _bookingRepo.GetByIdWithDetailsAsync(bookingId)
                    ?? throw new InvalidOperationException("Booking không tồn tại.");

                if (booking.DriverUserId != driverUserId)
                    throw new UnauthorizedAccessException("Booking này không thuộc về bạn.");

                var allowedStatuses = new[] { BookingStatus.WaitingOwner, BookingStatus.PendingPayment, BookingStatus.Paid };
                if (!allowedStatuses.Contains(booking.Status))
                    throw new InvalidOperationException("Không thể hủy booking ở trạng thái hiện tại.");

                var slotName = booking.ChargingSlot?.SlotName ?? "";
                var stationName = booking.ChargingSlot?.ChargingStation?.Name ?? "";
                var ownerUserId = booking.ChargingSlot?.ChargingStation?.OwnerUserId;
                decimal refundAmount = 0;
                decimal refundPercent = 0;
                string refundNote = "";

                // FIX Bug 8: Set cancelled TRƯỚC khi refund → tránh double-refund khi retry
                var wasPaid = booking.Status == BookingStatus.Paid;
                booking.Status = BookingStatus.Cancelled;
                booking.CancelledAt = DateTimeHelper.VietnamNow();
                booking.CancelReason = cancelReason ?? "Driver tự hủy";
                _bookingRepo.Update(booking);
            await _unitOfWork.CompleteAsync();

                // Xử lý hoàn tiền nếu đã Paid
                if (wasPaid)
                {
                    var now = DateTimeHelper.VietnamNow();
                    var hoursBeforeStart = (booking.StartTime - now).TotalHours;

                    // Fallback policies in case snapshots are missing (old bookings)
                    var refund100Deadline = booking.Refund100DeadlineAt ?? booking.StartTime.AddHours(-2);
                    var refund50Deadline = booking.Refund50DeadlineAt ?? booking.StartTime.AddHours(-1);

                    if (now <= refund100Deadline)
                    {
                        refundPercent = 1.0m;
                        refundNote = $"Hoàn 100% (hủy trước {refund100Deadline:HH:mm dd/MM})";
                    }
                    else if (now <= refund50Deadline)
                    {
                        refundPercent = 0.5m;
                        refundNote = $"Hoàn 50% (hủy trước {refund50Deadline:HH:mm dd/MM})";
                    }
                    else
                    {
                        refundPercent = 0m;
                        refundNote = "Không hoàn tiền (quá hạn hủy có mức hoàn)";
                    }

                    await ProcessRefundAsync(booking, refundPercent, $"Driver hủy booking — {refundNote}");
                    await RestoreExtraServiceStockAsync(booking);
                    await RefundLoyaltyPointsAsync(booking);
                    refundAmount = booking.TotalAmount * refundPercent;
                }

                // Release slot
                await ReleaseSlotIfBooked(booking.SlotId);

                await transaction.CommitAsync();

                // Notifications (ngoài transaction)
                if (wasPaid)
                {
                    await _notificationService.SendAsync(
                        driverUserId,
                        "Đặt chỗ đã hủy",
                        refundPercent > 0
                            ? $"Bạn đã hủy đặt chỗ tại slot {slotName} — trạm {stationName}. {refundNote}: {refundAmount:N0}đ đã hoàn vào ví."
                            : $"Bạn đã hủy đặt chỗ tại slot {slotName} — trạm {stationName}. {refundNote}.",
                        NotificationType.Booking);

                    if (ownerUserId.HasValue)
                    {
                        var ownerReceive = booking.TotalAmount * (1 - refundPercent);
                        await _notificationService.SendAsync(
                            ownerUserId.Value,
                            "Khách hủy đặt chỗ",
                            refundPercent < 1
                                ? $"Khách đã hủy slot {slotName} — trạm {stationName} ({booking.StartTime:HH:mm} - {booking.EndTime:HH:mm dd/MM}). Bạn nhận bồi thường {ownerReceive:N0}đ."
                                : $"Khách đã hủy slot {slotName} — trạm {stationName} ({booking.StartTime:HH:mm} - {booking.EndTime:HH:mm dd/MM}). Đã hoàn toàn bộ tiền cho khách.",
                            NotificationType.Booking);
                    }
                }
                else
                {
                    await _notificationService.SendAsync(
                        driverUserId,
                        "Đặt chỗ đã hủy",
                        $"Bạn đã hủy yêu cầu đặt chỗ tại slot {slotName} — trạm {stationName}.",
                        NotificationType.Booking);

                    if (ownerUserId.HasValue)
                    {
                        await _notificationService.SendAsync(
                            ownerUserId.Value,
                            "Khách hủy yêu cầu",
                            $"Khách đã hủy yêu cầu đặt chỗ tại slot {slotName} — trạm {stationName} ({booking.StartTime:HH:mm} - {booking.EndTime:HH:mm dd/MM}).",
                            NotificationType.Booking);
                    }
                }

                return MapToDto(booking);
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        /// <summary>
        /// Owner hủy booking đã Paid → luôn hoàn 100% cho Driver.
        /// </summary>
        public async Task<BookingDto> OwnerCancelBookingAsync(int ownerUserId, int bookingId, string? cancelReason)
        {
            // FIX: Transaction bảo vệ refund flow
            using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                var booking = await _bookingRepo.GetByIdWithDetailsAsync(bookingId)
                    ?? throw new InvalidOperationException("Booking không tồn tại.");

                if (booking.ChargingSlot.ChargingStation.OwnerUserId != ownerUserId)
                    throw new UnauthorizedAccessException("Bạn không có quyền thao tác trên booking này.");

                if (booking.Status != BookingStatus.Paid)
                    throw new InvalidOperationException("Chỉ có thể hủy booking đã thanh toán. Dùng Reject cho booking chờ duyệt.");

                var slotName = booking.ChargingSlot?.SlotName ?? "";
                var stationName = booking.ChargingSlot?.ChargingStation?.Name ?? "";

                // Set cancelled TRƯỚC refund
                booking.Status = BookingStatus.Cancelled;
                booking.CancelledAt = DateTimeHelper.VietnamNow();
                booking.CancelReason = cancelReason ?? "Owner hủy";
                _bookingRepo.Update(booking);
            await _unitOfWork.CompleteAsync();

                // Owner hủy → hoàn 100% cho Driver
                await ProcessRefundAsync(booking, 1.0m, $"Owner hủy booking — hoàn 100% cho Driver");
                await RestoreExtraServiceStockAsync(booking);
                await RefundLoyaltyPointsAsync(booking);

                await ReleaseSlotIfBooked(booking.SlotId);

                await transaction.CommitAsync();

                // Notifications (ngoài transaction)
                await _notificationService.SendAsync(
                    booking.DriverUserId,
                    "Chủ trạm đã hủy đặt chỗ",
                    $"Chủ trạm {stationName} đã hủy đặt chỗ tại slot {slotName} ({booking.StartTime:HH:mm} - {booking.EndTime:HH:mm dd/MM}).{(cancelReason != null ? $" Lý do: {cancelReason}" : "")} {booking.TotalAmount:N0}đ đã hoàn vào ví của bạn.",
                    NotificationType.Booking);

                await _notificationService.SendAsync(
                    ownerUserId,
                    "Bạn đã hủy đặt chỗ",
                    $"Đã hủy booking slot {slotName} ({booking.StartTime:HH:mm} - {booking.EndTime:HH:mm dd/MM}). Hoàn {booking.TotalAmount:N0}đ cho khách.",
                    NotificationType.Booking);

                return MapToDto(booking);
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        /// <summary>
        /// Admin huỷ booking (khi Driver/Owner bị Ban).
        /// Luôn hoàn 100% tiền cho Driver nếu đã Paid.
        /// </summary>
        public async Task CancelSystemBookingAsync(int bookingId, string systemReason)
        {
            using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                var booking = await _bookingRepo.GetByIdWithDetailsAsync(bookingId)
                    ?? throw new InvalidOperationException("Booking không tồn tại.");

                var allowedStatuses = new[] { BookingStatus.WaitingOwner, BookingStatus.PendingPayment, BookingStatus.Paid };
                if (!allowedStatuses.Contains(booking.Status))
                    throw new InvalidOperationException("Không thể hủy booking ở trạng thái hiện tại.");

                var wasPaid = booking.Status == BookingStatus.Paid;

                booking.Status = BookingStatus.Cancelled;
                booking.CancelledAt = DateTimeHelper.VietnamNow();
                booking.CancelReason = $"Hệ thống hủy: {systemReason}";
                _bookingRepo.Update(booking);
            await _unitOfWork.CompleteAsync();

                if (wasPaid)
                {
                    // Hoàn tiền 100% cho Driver
                    await ProcessRefundAsync(booking, 1.0m, booking.CancelReason);
                    await RestoreExtraServiceStockAsync(booking);
                    await RefundLoyaltyPointsAsync(booking);
                }

                await ReleaseSlotIfBooked(booking.SlotId);

                await transaction.CommitAsync();
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Lỗi khi CancelSystemBookingAsync cho booking {BookingId}", bookingId);
                throw;
            }
        }

        // ─── CANCEL HELPERS ───

        private async Task ProcessRefundAsync(Booking booking, decimal refundPercent, string memo)
        {
            if (refundPercent == 0)
            {
                // 0% refund → toàn bộ ESCROW → Owner (trừ phí sàn + VAT)
                var ownerUserId2 = booking.ChargingSlot!.ChargingStation!.OwnerUserId;
                await SettleCompensationToOwner(booking, booking.TotalAmount, ownerUserId2, $"{memo} — phạt 100%");
                return;
            }

            var refundAmount = booking.TotalAmount * refundPercent;
            var ownerAmount = booking.TotalAmount - refundAmount;

            // Refund → Driver (hoàn trả trực tiếp, không tính phí)
            if (refundAmount > 0)
            {
                await TransferFromEscrow(booking, refundAmount, booking.DriverUserId, WalletType.Driver, $"{memo} — {refundAmount:N0}đ → Driver");
            }

            // Bồi thường → Owner (trừ phí sàn + VAT giống như settlement)
            if (ownerAmount > 0)
            {
                var ownerUserId3 = booking.ChargingSlot!.ChargingStation!.OwnerUserId;
                await SettleCompensationToOwner(booking, ownerAmount, ownerUserId3, $"{memo} — phạt {ownerAmount:N0}đ");
            }
        }

        private async Task SettleCompensationToOwner(Booking booking, decimal grossAmount, int ownerUserId, string memo)
        {
            var vatRate = booking.VatRateSnapshot == 0 ? 0.08m : booking.VatRateSnapshot;
            var platformFeeRate = booking.PlatformFeeRateSnapshot == 0 ? 0.05m : booking.PlatformFeeRateSnapshot;

            var vatAmount = Math.Round(grossAmount * vatRate, 0);
            var platformFee = Math.Round(grossAmount * platformFeeRate, 0);
            var ownerNet = grossAmount - vatAmount - platformFee;

            var escrowWallet = await _walletRepo.GetBySystemCodeAsync("ESCROW");
            var platformWallet = await _walletRepo.GetBySystemCodeAsync("PLATFORM_REVENUE");
            var ownerWallet = await _walletRepo.GetByUserIdAsync(ownerUserId);

            if (ownerWallet == null)
            {
                ownerWallet = new Wallet
                {
                    UserId = ownerUserId,
                    WalletType = WalletType.Owner,
                    AvailableBalance = 0,
                    FrozenBalance = 0,
                    CreatedAt = DateTimeHelper.VietnamNow()
                };
                _walletRepo.Add(ownerWallet);
                await _unitOfWork.CompleteAsync();
            }

            // Chuyển tiền nét cho Owner (Atomic để tránh lỗ hổng Lost Update tranh chấp luồng)
            await _unitOfWork.ExecuteSqlRawSafeAsync(
                "UPDATE Wallet SET AvailableBalance = AvailableBalance - {0} WHERE Id = {1}",
                ownerNet, escrowWallet!.Id);
            await _unitOfWork.ExecuteSqlRawSafeAsync(
                "UPDATE Wallet SET AvailableBalance = AvailableBalance + {0} WHERE Id = {1}",
                ownerNet, ownerWallet.Id);

            // Chuyển phí nền tảng (Atomic)
            await _unitOfWork.ExecuteSqlRawSafeAsync(
                "UPDATE Wallet SET AvailableBalance = AvailableBalance - {0} WHERE Id = {1}",
                platformFee, escrowWallet!.Id);
            await _unitOfWork.ExecuteSqlRawSafeAsync(
                "UPDATE Wallet SET AvailableBalance = AvailableBalance + {0} WHERE Id = {1}",
                platformFee, platformWallet!.Id);

            var now = DateTimeHelper.VietnamNow();

            _ledgerRepo.Add(new LedgerTransaction
            {
                ReferenceType = "BookingCancelCompensation",
                ReferenceId = booking.Id,
                Memo = $"{memo} (Owner nhận thực {ownerNet:N0}đ, Sàn thu {platformFee:N0}đ phí bồi thường)",
                CreatedAt = now,
                Entries = new List<LedgerEntry>
                {
                    new LedgerEntry { WalletId = escrowWallet.Id, Direction = LedgerDirection.Debit, Amount = ownerNet + platformFee, CreatedAt = now },
                    new LedgerEntry { WalletId = ownerWallet.Id, Direction = LedgerDirection.Credit, Amount = ownerNet, CreatedAt = now },
                    new LedgerEntry { WalletId = platformWallet.Id, Direction = LedgerDirection.Credit, Amount = platformFee, CreatedAt = now }
                }
            });

            await _unitOfWork.CompleteAsync();
        }

        private async Task TransferFromEscrow(Booking booking, decimal amount, int userId, WalletType walletType, string memo)
        {
            var escrowWallet = await _walletRepo.GetBySystemCodeAsync("ESCROW");
            var userWallet = await _walletRepo.GetByUserIdAsync(userId);

            if (userWallet == null)
            {
                userWallet = new Wallet
                {
                    UserId = userId,
                    WalletType = walletType,
                    AvailableBalance = 0,
                    FrozenBalance = 0,
                    CreatedAt = DateTimeHelper.VietnamNow()
                };
                _walletRepo.Add(userWallet);
                await _unitOfWork.CompleteAsync();
            }

            // Sử dụng SQL Atomic nguyên thủy để chống đè tiền Escrow
            await _unitOfWork.ExecuteSqlRawSafeAsync(
                "UPDATE Wallet SET AvailableBalance = AvailableBalance - {0} WHERE Id = {1}",
                amount, escrowWallet!.Id);
            await _unitOfWork.ExecuteSqlRawSafeAsync(
                "UPDATE Wallet SET AvailableBalance = AvailableBalance + {0} WHERE Id = {1}",
                amount, userWallet.Id);

            _ledgerRepo.Add(new LedgerTransaction
            {
                ReferenceType = "BookingCancelRefund",
                ReferenceId = booking.Id,
                Memo = memo,
                CreatedAt = DateTimeHelper.VietnamNow(),
                Entries = new List<LedgerEntry>
                {
                    new LedgerEntry { WalletId = escrowWallet.Id, Direction = LedgerDirection.Debit, Amount = amount, CreatedAt = DateTimeHelper.VietnamNow() },
                    new LedgerEntry { WalletId = userWallet.Id, Direction = LedgerDirection.Credit, Amount = amount, CreatedAt = DateTimeHelper.VietnamNow() }
                }
            });

            await _unitOfWork.CompleteAsync();
        }

        private async Task ReleaseSlotIfBooked(int slotId)
        {
            var slot = await _slotRepo.GetByIdAsync(slotId, tracking: true);
            if (slot != null && slot.Status == SlotStatus.Booked)
            {
                slot.Status = SlotStatus.Active;
                slot.UpdatedAt = DateTimeHelper.VietnamNow();
                await _unitOfWork.CompleteAsync();
            }
        }

        public async Task<BookingDto?> GetByIdAsync(int bookingId)
        {
            var booking = await _bookingRepo.GetByIdWithDetailsAsync(bookingId);
            return booking == null ? null : MapToDto(booking);
        }

        /// <summary>
        /// Preview phí hủy booking trước khi Driver xác nhận.
        /// Giúp FE hiện popup cảnh báo "Bạn sẽ mất X% tiền nếu hủy ngay".
        /// </summary>
        public async Task<CancelPreviewDto> GetCancelPreviewAsync(int driverUserId, int bookingId)
        {
            var booking = await _bookingRepo.GetByIdWithDetailsAsync(bookingId)
                ?? throw new InvalidOperationException("Booking không tồn tại.");

            if (booking.DriverUserId != driverUserId)
                throw new UnauthorizedAccessException("Booking này không thuộc về bạn.");

            var result = new CancelPreviewDto
            {
                BookingId = bookingId,
                TotalAmount = booking.TotalAmount,
                Status = booking.Status.ToString()
            };

            if (booking.Status == BookingStatus.WaitingOwner || booking.Status == BookingStatus.PendingPayment)
            {
                result.RefundPercent = 100;
                result.RefundAmount = 0; // Chưa trả tiền nên 0
                result.PenaltyAmount = 0;
                result.Message = "Hủy miễn phí (chưa thanh toán).";
            }
            else if (booking.Status == BookingStatus.Paid)
            {
                var now = DateTimeHelper.VietnamNow();
                // Dùng snapshot deadline (đã snapshot lúc tạo booking) — đồng nhất với DriverCancelBookingAsync
                var refund100Deadline = booking.Refund100DeadlineAt ?? booking.StartTime.AddHours(-2);
                var refund50Deadline = booking.Refund50DeadlineAt ?? booking.StartTime.AddHours(-1);

                if (now <= refund100Deadline)
                {
                    result.RefundPercent = 100;
                    result.RefundAmount = booking.TotalAmount;
                    result.PenaltyAmount = 0;
                    result.Message = $"Hoàn 100% vào ví (hủy trước {refund100Deadline:HH:mm dd/MM}).";
                }
                else if (now <= refund50Deadline)
                {
                    result.RefundPercent = 50;
                    result.RefundAmount = Math.Round(booking.TotalAmount * 0.5m, 0);
                    result.PenaltyAmount = booking.TotalAmount - result.RefundAmount;
                    result.Message = $"Hoàn 50% ({result.RefundAmount:N0}đ) vào ví. Mất {result.PenaltyAmount:N0}đ phí hủy muộn (hủy trước {refund50Deadline:HH:mm dd/MM}).";
                }
                else
                {
                    result.RefundPercent = 0;
                    result.RefundAmount = 0;
                    result.PenaltyAmount = booking.TotalAmount;
                    result.Message = $"⚠️ Không hoàn tiền! Bạn sẽ mất toàn bộ {booking.TotalAmount:N0}đ vì đã quá hạn hủy có mức hoàn.";
                }
            }
            else
            {
                throw new InvalidOperationException("Không thể hủy booking ở trạng thái hiện tại.");
            }

            return result;
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
            var serviceAmount = b.BookingExtraServices?.Sum(e => e.TotalPrice) ?? 0;

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
                ServiceAmount = serviceAmount,
                PointsUsed = b.PointsUsed,
                PointsDiscountAmount = b.PointsDiscountAmount,
                PointsEarned = b.PointsEarned,
                Note = b.Note,
                Status = b.Status.ToString(),
                RejectionReason = b.RejectionReason,
                CancelReason = b.CancelReason,
                PaymentExpiresAt = b.PaymentExpiresAt,
                CreatedAt = b.CreatedAt,
                ExtraServices = b.BookingExtraServices?.Select(e => new BookingExtraServiceDto
                {
                    ServiceId = e.ServiceId,
                    ServiceName = e.ExtraService?.ServiceName ?? "",
                    Quantity = e.Quantity,
                    UnitPrice = e.UnitPrice,
                    TotalPrice = e.TotalPrice
                }).ToList()
            };
        }

        /// <summary>Hoàn stock cho ExtraService khi cancel booking đã paid.</summary>
        private async Task RestoreExtraServiceStockAsync(Booking booking)
        {
            if (booking.BookingExtraServices == null || booking.BookingExtraServices.Count == 0)
                return;

            foreach (var bes in booking.BookingExtraServices)
            {
                var svc = await _extraServiceRepo.GetByIdAsync(bes.ServiceId);
                if (svc != null && svc.TotalStock.HasValue)
                {
                    svc.TotalStock += bes.Quantity;
                }
            }
            await _unitOfWork.CompleteAsync();
        }

        /// <summary>Hoàn điểm tích lũy khi cancel booking đã dùng điểm.</summary>
        private async Task RefundLoyaltyPointsAsync(Booking booking)
        {
            if (booking.PointsUsed <= 0) return;

            var driver = await _driverRepo.GetByUserIdAsync(booking.DriverUserId, tracking: true);
            if (driver == null) return;

            driver.LoyaltyPoints += booking.PointsUsed;

            _loyaltyRepo.Add(new LoyaltyTransaction
            {
                DriverUserId = booking.DriverUserId,
                BookingId = booking.Id,
                Type = "Refund",
                Points = booking.PointsUsed,
                Description = $"Hoàn {booking.PointsUsed:N0} điểm do hủy booking #{booking.Id}",
                CreatedAt = DateTimeHelper.VietnamNow()
            });

            await _unitOfWork.CompleteAsync();
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
            int maxIterations = 1000; // Safety guard

            while (current < endTime && maxIterations-- > 0)
            {
                var currentTimeOnly = TimeOnly.FromDateTime(current);

                // Tìm tier phù hợp cho thời điểm hiện tại
                // Hỗ trợ cả tier kết thúc lúc 23:00 hoặc 23:59 (inclusive end)
                var tier = pricings
                    .FirstOrDefault(p => currentTimeOnly >= p.StartTime && currentTimeOnly < p.EndTime);

                // Nếu không match (ví dụ: 23:00 với tier end = 23:00) → thử tier cuối cùng
                if (tier == null)
                {
                    tier = pricings
                        .OrderByDescending(p => p.StartTime)
                        .FirstOrDefault(p => currentTimeOnly >= p.StartTime);
                }

                // Vẫn null → fallback tier đầu tiên (sẽ tính qua ngày mới)
                if (tier == null)
                {
                    tier = pricings.First();
                }

                // Tính giờ kết thúc tier trong ngày hiện tại
                var tierEndToday = current.Date.Add(tier.EndTime.ToTimeSpan());

                // Nếu tier end = 23:59 → hết ngày
                if (tier.EndTime == new TimeOnly(23, 59))
                    tierEndToday = current.Date.AddDays(1);

                // Nếu tierEndToday <= current → tier wrap qua ngày mới (ví dụ: tier 22:00-06:00)
                // hoặc fallback tier có endTime < current → đẩy sang ngày mai
                if (tierEndToday <= current)
                    tierEndToday = current.Date.AddDays(1).Add(tier.EndTime.ToTimeSpan());

                var segmentEnd = endTime < tierEndToday ? endTime : tierEndToday;

                // Safety: đảm bảo luôn tiến về phía trước
                if (segmentEnd <= current)
                    segmentEnd = endTime; // Tính phần còn lại với tier hiện tại

                var hours = (decimal)(segmentEnd - current).TotalHours;

                if (hours > 0)
                {
                    total += hours * tier.PricePerHour;
                }

                current = segmentEnd;

                // Nếu current sang ngày mới, reset để match tier mới
                // (không cần code đặc biệt vì TimeOnly.FromDateTime tự handle)
            }

            // Làm tròn đến hàng đơn vị
            return Math.Round(total, 0);
        }

        public async Task<ChargeSlot.Api.DTOs.Admin.Overview.PagedResultDto<BookingDto>> GetAdminAllBookingsAsync(ChargeSlot.Api.DTOs.Admin.Overview.BookingFilterDto filter)
        {
            var result = await _bookingRepo.GetAdminBookingsPagedAsync(filter);
            
            return new ChargeSlot.Api.DTOs.Admin.Overview.PagedResultDto<BookingDto>
            {
                Items = result.Items.Select(MapToDto).ToList(),
                TotalCount = result.TotalCount,
                Page = filter.Page,
                PageSize = filter.PageSize
            };
        }
    }
}



