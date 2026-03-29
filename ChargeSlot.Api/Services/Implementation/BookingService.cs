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
        private readonly ChargeSlotDbContext _context;

        public BookingService(
            IBookingRepository bookingRepo,
            IChargingSlotRepository slotRepo,
            INotificationService notificationService,
            IWalletRepository walletRepo,
            ChargeSlotDbContext context)
        {
            _bookingRepo = bookingRepo;
            _slotRepo = slotRepo;
            _notificationService = notificationService;
            _walletRepo = walletRepo;
            _context = context;
        }

        /// <summary>
        /// Step 4-9: Driver sends booking request → System computes endTime,
        /// validates availability, creates booking (WaitingOwner)
        /// </summary>
        public async Task<BookingDto> CreateBookingAsync(int driverUserId, CreateBookingDto dto)
        {
            // Validate: DurationHours phải > 0
            if (dto.DurationHours <= 0)
                throw new InvalidOperationException("Thời lượng sạc phải lớn hơn 0.");

            // Validate: StartTime phải trong tương lai và cách hiện tại ít nhất 30 phút
            var minutesUntilStart = (dto.StartTime - DateTimeHelper.VietnamNow()).TotalMinutes;
            if (minutesUntilStart <= 0)
                throw new InvalidOperationException("Thời gian bắt đầu phải trong tương lai.");
            if (minutesUntilStart < 30)
                throw new InvalidOperationException("Phải đặt trước ít nhất 30 phút trước giờ sạc.");

            // Validate: Driver chỉ được có tối đa 3 booking đang chờ xử lý
            var pendingCount = await _context.Bookings
                .CountAsync(b => b.DriverUserId == driverUserId
                    && (b.Status == BookingStatus.WaitingOwner || b.Status == BookingStatus.PendingPayment));
            if (pendingCount >= 3)
                throw new InvalidOperationException("Bạn đang có 3 booking chờ xử lý. Vui lòng hoàn tất hoặc hủy bớt trước khi đặt mới.");

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

            // ── Validate & create ExtraServices (topping) ──
            decimal serviceAmount = 0;
            var extraServiceRecords = new List<BookingExtraService>();

            if (dto.ExtraServices != null && dto.ExtraServices.Count > 0)
            {
                var serviceIds = dto.ExtraServices.Select(e => e.ServiceId).ToList();
                var services = await _context.Set<ExtraService>()
                    .Where(s => serviceIds.Contains(s.Id))
                    .ToListAsync();

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
                var driver = await _context.Driver.FirstOrDefaultAsync(d => d.UserId == driverUserId)
                    ?? throw new InvalidOperationException("Driver profile không tồn tại.");

                if (dto.PointsToUse > driver.LoyaltyPoints)
                    throw new InvalidOperationException(
                        $"Bạn chỉ có {driver.LoyaltyPoints:N0} điểm, không đủ {dto.PointsToUse:N0} điểm.");

                // Load max redeem rate from config
                var maxRedeemConfig = await _context.SystemConfigs.FindAsync("LoyaltyMaxRedeemRate");
                var maxRedeemRate = decimal.TryParse(maxRedeemConfig?.Value, out var rate) ? rate : 0.5m;
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
                _context.LoyaltyTransactions.Add(new LoyaltyTransaction
                {
                    DriverUserId = driverUserId,
                    Type = "Redeem",
                    Points = pointsUsed,
                    Description = $"Dùng {pointsUsed:N0} điểm cho booking slot {dto.SlotId}",
                    CreatedAt = DateTimeHelper.VietnamNow()
                });
            }

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
                CreatedAt = DateTimeHelper.VietnamNow()
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
            var timeToCharging = booking.StartTime - DateTimeHelper.VietnamNow();
            if (timeToCharging.TotalMinutes < 30)
            {
                booking.PaymentExpiresAt = booking.StartTime;
            }
            else
            {
                booking.PaymentExpiresAt = DateTimeHelper.VietnamNow().AddMinutes(30);
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
                    $"Yêu cầu đặt chỗ tại slot {booking.ChargingSlot?.SlotName} — trạm {booking.ChargingSlot?.ChargingStation?.Name} đã bị từ chối tự động do slot đã được chấp nhận cho khách khác.",
                    NotificationType.Booking);
            }

            // Notify Driver: booking được chấp nhận
            await _notificationService.SendAsync(
                booking.DriverUserId,
                "Đặt chỗ được chấp nhận",
                $"Yêu cầu đặt chỗ tại slot {booking.ChargingSlot?.SlotName} — trạm {booking.ChargingSlot?.ChargingStation?.Name} ({booking.StartTime:HH:mm} - {booking.EndTime:HH:mm dd/MM}) đã được chấp nhận. Vui lòng thanh toán {booking.TotalAmount:N0}đ trước {booking.PaymentExpiresAt:HH:mm dd/MM}.",
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
            using var transaction = await _context.Database.BeginTransactionAsync();
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
                await _bookingRepo.UpdateAsync(booking);

                // Xử lý hoàn tiền nếu đã Paid
                if (wasPaid)
                {
                    var hoursBeforeStart = (booking.StartTime - DateTimeHelper.VietnamNow()).TotalHours;

                    if (hoursBeforeStart >= 2)
                    {
                        refundPercent = 1.0m;
                        refundNote = "Hoàn 100% (hủy trước ≥2 giờ)";
                    }
                    else if (hoursBeforeStart >= 1)
                    {
                        refundPercent = 0.5m;
                        refundNote = "Hoàn 50% (hủy trước 1-2 giờ)";
                    }
                    else
                    {
                        refundPercent = 0m;
                        refundNote = "Không hoàn tiền (hủy trước <1 giờ)";
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
            using var transaction = await _context.Database.BeginTransactionAsync();
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
                await _bookingRepo.UpdateAsync(booking);

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

        // ─── CANCEL HELPERS ───

        /// <summary>Hoàn tiền từ ESCROW về ví Driver (và Owner nếu có phần bồi thường).</summary>
        private async Task ProcessRefundAsync(Booking booking, decimal refundPercent, string memo)
        {
            if (refundPercent == 0)
            {
                // 0% refund → toàn bộ ESCROW → Owner
                var ownerUserId2 = booking.ChargingSlot!.ChargingStation!.OwnerUserId;
                await TransferFromEscrow(booking, booking.TotalAmount, ownerUserId2, WalletType.Owner, $"{memo} — {booking.TotalAmount:N0}đ → Owner");
                return;
            }

            var refundAmount = booking.TotalAmount * refundPercent;
            var ownerAmount = booking.TotalAmount - refundAmount;

            // Refund → Driver
            if (refundAmount > 0)
            {
                await TransferFromEscrow(booking, refundAmount, booking.DriverUserId, WalletType.Driver, $"{memo} — {refundAmount:N0}đ → Driver");
            }

            // Bồi thường → Owner
            if (ownerAmount > 0)
            {
                var ownerUserId3 = booking.ChargingSlot!.ChargingStation!.OwnerUserId;
                await TransferFromEscrow(booking, ownerAmount, ownerUserId3, WalletType.Owner, $"{memo} — {ownerAmount:N0}đ → Owner");
            }
        }

        private async Task TransferFromEscrow(Booking booking, decimal amount, int userId, WalletType walletType, string memo)
        {
            var escrowWallet = await _context.Wallets.FirstAsync(w => w.SystemCode == "ESCROW");
            var userWallet = await _context.Wallets.FirstOrDefaultAsync(w => w.UserId == userId);

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
                _context.Wallets.Add(userWallet);
                await _context.SaveChangesAsync();
            }

            escrowWallet.AvailableBalance -= amount;
            userWallet.AvailableBalance += amount;

            _context.Set<LedgerTransaction>().Add(new LedgerTransaction
            {
                ReferenceType = "BookingCancel",
                ReferenceId = booking.Id,
                Memo = memo,
                CreatedAt = DateTimeHelper.VietnamNow(),
                Entries = new List<LedgerEntry>
                {
                    new LedgerEntry { WalletId = escrowWallet.Id, Direction = LedgerDirection.Debit, Amount = amount, CreatedAt = DateTimeHelper.VietnamNow() },
                    new LedgerEntry { WalletId = userWallet.Id, Direction = LedgerDirection.Credit, Amount = amount, CreatedAt = DateTimeHelper.VietnamNow() }
                }
            });

            await _context.SaveChangesAsync();
        }

        private async Task ReleaseSlotIfBooked(int slotId)
        {
            var slot = await _slotRepo.GetByIdAsync(slotId, tracking: true);
            if (slot != null && slot.Status == SlotStatus.Booked)
            {
                slot.Status = SlotStatus.Active;
                slot.UpdatedAt = DateTimeHelper.VietnamNow();
                await _slotRepo.SaveChangesAsync();
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
                var hoursBeforeStart = (booking.StartTime - DateTimeHelper.VietnamNow()).TotalHours;

                if (hoursBeforeStart >= 2)
                {
                    result.RefundPercent = 100;
                    result.RefundAmount = booking.TotalAmount;
                    result.PenaltyAmount = 0;
                    result.Message = "Hoàn 100% vào ví (hủy trước ≥2 giờ).";
                }
                else if (hoursBeforeStart >= 1)
                {
                    result.RefundPercent = 50;
                    result.RefundAmount = Math.Round(booking.TotalAmount * 0.5m, 0);
                    result.PenaltyAmount = booking.TotalAmount - result.RefundAmount;
                    result.Message = $"Hoàn 50% ({result.RefundAmount:N0}đ) vào ví. Mất {result.PenaltyAmount:N0}đ phí hủy muộn.";
                }
                else
                {
                    result.RefundPercent = 0;
                    result.RefundAmount = 0;
                    result.PenaltyAmount = booking.TotalAmount;
                    result.Message = $"⚠️ Không hoàn tiền! Bạn sẽ mất toàn bộ {booking.TotalAmount:N0}đ vì hủy dưới 1 giờ trước giờ sạc.";
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
                var svc = await _context.Set<ExtraService>().FindAsync(bes.ServiceId);
                if (svc != null && svc.TotalStock.HasValue)
                {
                    svc.TotalStock += bes.Quantity;
                }
            }
            await _context.SaveChangesAsync();
        }

        /// <summary>Hoàn điểm tích lũy khi cancel booking đã dùng điểm.</summary>
        private async Task RefundLoyaltyPointsAsync(Booking booking)
        {
            if (booking.PointsUsed <= 0) return;

            var driver = await _context.Driver.FirstOrDefaultAsync(d => d.UserId == booking.DriverUserId);
            if (driver == null) return;

            driver.LoyaltyPoints += booking.PointsUsed;

            _context.LoyaltyTransactions.Add(new LoyaltyTransaction
            {
                DriverUserId = booking.DriverUserId,
                BookingId = booking.Id,
                Type = "Earn",
                Points = booking.PointsUsed,
                Description = $"Hoàn {booking.PointsUsed:N0} điểm do hủy booking #{booking.Id}",
                CreatedAt = DateTimeHelper.VietnamNow()
            });

            await _context.SaveChangesAsync();
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
    }
}
