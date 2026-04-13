using ChargeSlot.Api.Enums;
using ChargeSlot.Api.Models;
using ChargeSlot.Api.Repositories.Interfaces;
using ChargeSlot.Api.Services.Interfaces;
using ChargeSlot.Api.Helpers;

namespace ChargeSlot.Api.BackgroundJobs
{
    /// <summary>
    /// Xử lý 3 trường hợp overdue:
    /// 1. WaitingOwner quá hạn (config NoShow_Grace_Minutes) → auto-expire
    /// 2. Paid quá EndTime → CompletedPendingInvoice (cho Driver 24h dispute, no-show)
    /// 3. CheckedIn quá EndTime → auto-stop + CompletedPendingInvoice (giải phóng slot ngay)
    /// Chạy mỗi 60 giây.
    /// </summary>
    public class NoShowJob : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<NoShowJob> _logger;

        public NoShowJob(IServiceProvider serviceProvider, ILogger<NoShowJob> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessWaitingOwnerTimeoutAsync();
                    await ProcessPaidNoShowAsync();
                    await ProcessCheckedInOvertimeAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in NoShowJob");
                }

                await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
            }
        }

        // ═══════════════════════════════════════════════════════
        // 1. WaitingOwner quá hạn (config) → Auto-expire
        // ═══════════════════════════════════════════════════════
        private async Task ProcessWaitingOwnerTimeoutAsync()
        {
            using var scope = _serviceProvider.CreateScope();
            var bookingRepo = scope.ServiceProvider.GetRequiredService<IBookingRepository>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
            var configService = scope.ServiceProvider.GetRequiredService<ISystemConfigService>();

            var configs = await configService.GetCurrentConfigsAsync();
            var graceTime = configs.NoShow_Grace_Minutes;

            var now = DateTimeHelper.VietnamNow();
            var cutoff = now.AddMinutes(-graceTime);

            var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();
            var staleBookings = await bookingRepo.GetStaleWaitingOwnerAsync(cutoff);

            foreach (var booking in staleBookings)
            {
                try
                {
                    await bookingService.ExpireSystemBookingAsync(booking.Id, $"Không có phản hồi từ chủ trạm trong {graceTime} phút.");
                    _logger.LogInformation("Booking {BookingId} auto-expired: WaitingOwner timeout {GraceMinutes} min.", booking.Id, graceTime);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error auto-expiring WaitingOwner booking {BookingId}", booking.Id);
                }
            }
        }

        // ═══════════════════════════════════════════════════════
        // 2. Paid quá EndTime → CompletedPendingInvoice (No-Show)
        //    Driver có 24h để review/dispute trước khi auto-confirm
        // ═══════════════════════════════════════════════════════
        private async Task ProcessPaidNoShowAsync()
        {
            using var scope = _serviceProvider.CreateScope();
            var bookingRepo = scope.ServiceProvider.GetRequiredService<IBookingRepository>();
            var slotRepo = scope.ServiceProvider.GetRequiredService<IChargingSlotRepository>();
            var invoiceRepo = scope.ServiceProvider.GetRequiredService<IInvoiceRepository>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

            // Driver có thể check-in cho đến lúc EndTime.
            // Do đó, ngay khi vừa qua EndTime mà vẫn chưa check-in -> Đóng check-in, thành No-Show & giải phóng slot ngay.
            var now = DateTimeHelper.VietnamNow();
            var cutoff = now;

            var overdueBookings = await bookingRepo.GetPaidNoShowAsync(cutoff);

            foreach (var booking in overdueBookings)
            {
                using var transaction = await unitOfWork.BeginTransactionAsync();
                try
                {
                    // FIX: Set CompletedPendingInvoice thay vì Completed
                    // → Cho Driver 24h review/dispute trước khi auto-confirm
                    booking.Status = BookingStatus.CompletedPendingInvoice;
                    booking.UpdatedAt = now;
                    bookingRepo.Update(booking);
                    await unitOfWork.CompleteAsync();

                    if (booking.ChargingSlot != null && booking.ChargingSlot.Status == SlotStatus.Booked)
                    {
                        booking.ChargingSlot.Status = SlotStatus.Active;
                        slotRepo.Update(booking.ChargingSlot);
                        await unitOfWork.CompleteAsync();
                    }

                    // Tạo Invoice với PendingConfirm (chờ Driver review)
                    var grossAmount = booking.TotalAmount;
                    var vatRate = booking.VatRateSnapshot == 0 ? 0.08m : booking.VatRateSnapshot;
                    var platformFeeRate = booking.PlatformFeeRateSnapshot == 0 ? 0.05m : booking.PlatformFeeRateSnapshot;
                    var vatAmount = Math.Round(grossAmount * vatRate, 0);
                    var platformFee = Math.Round(grossAmount * platformFeeRate, 0);
                    var ownerNetAmount = grossAmount - vatAmount - platformFee;

                    var invoice = new Invoice
                    {
                        BookingId = booking.Id,
                        ChargingAmount = ownerNetAmount,
                        ServiceAmount = 0,
                        VatAmount = vatAmount,
                        PlatformFee = platformFee,
                        TotalAmount = grossAmount,
                        Status = InvoiceStatus.PendingConfirm, // Chờ Driver review 24h
                        CreatedAt = now,
                        UpdatedAt = now
                    };
                    invoiceRepo.Add(invoice);
                    await unitOfWork.CompleteAsync();

                    // Loyalty + Settlement sẽ do ConfirmCompletionAsync hoặc InvoiceAutoConfirmJob xử lý

                    await transaction.CommitAsync();

                    // Notify Driver: cho 24h review/dispute
                    await notificationService.SendAsync(
                        booking.DriverUserId,
                        "Booking chưa check-in — vui lòng xác nhận",
                        $"Bạn không check-in tại slot {booking.ChargingSlot?.SlotName} — trạm {booking.ChargingSlot?.ChargingStation?.Name} ({booking.StartTime:HH:mm} - {booking.EndTime:HH:mm dd/MM}). Hóa đơn {grossAmount:N0}đ đã được tạo. Bạn có 24h để xác nhận hoặc khiếu nại.",
                        NotificationType.Booking);

                    var ownerUserId = booking.ChargingSlot?.ChargingStation?.OwnerUserId;
                    if (ownerUserId.HasValue)
                    {
                        await notificationService.SendAsync(
                            ownerUserId.Value,
                            "Driver không check-in",
                            $"Driver không check-in tại slot {booking.ChargingSlot?.SlotName} ({booking.StartTime:HH:mm} - {booking.EndTime:HH:mm dd/MM}). Chờ Driver xác nhận hóa đơn (24h).",
                            NotificationType.Booking);
                    }

                    _logger.LogInformation("Booking {BookingId} no-show → CompletedPendingInvoice (Driver has 24h to review/dispute).", booking.Id);
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Error auto-completing Paid booking {BookingId}", booking.Id);
                }
            }
        }

        // ═══════════════════════════════════════════════════════
        // 3. CheckedIn quá EndTime → Auto-stop + invoice (giải phóng slot ngay)
        //    Không chờ grace period — hết EndTime là auto-stop + giải phóng slot ngay
        // ═══════════════════════════════════════════════════════
        private async Task ProcessCheckedInOvertimeAsync()
        {
            using var scope = _serviceProvider.CreateScope();
            var bookingRepo = scope.ServiceProvider.GetRequiredService<IBookingRepository>();
            var invoiceRepo = scope.ServiceProvider.GetRequiredService<IInvoiceRepository>();
            var chargingSessionRepo = scope.ServiceProvider.GetRequiredService<IChargingSessionRepository>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

            // Không chờ grace period — hết EndTime là auto-stop + giải phóng slot ngay
            var now = DateTimeHelper.VietnamNow();
            var cutoff = now; // EndTime < now → hết giờ là xử lý luôn

            var overtimeBookings = await bookingRepo.GetCheckedInOvertimeAsync(cutoff);

            foreach (var booking in overtimeBookings)
            {
                using var transaction = await unitOfWork.BeginTransactionAsync();
                try
                {
                    // 1. Stop session
                    var session = booking.ChargingSession;
                    if (session != null)
                    {
                        session.ActualEndTime = booking.EndTime; // Dùng EndTime gốc, không phải now
                        session.ActualDurationHours = (decimal)(booking.EndTime - (session.ActualStartTime ?? booking.StartTime)).TotalHours;
                        chargingSessionRepo.Update(session);
                    }

                    // 2. Create invoice with PendingConfirm (chờ Driver review)
                    var grossAmount = booking.TotalAmount;
                    var vatRate = booking.VatRateSnapshot == 0 ? 0.08m : booking.VatRateSnapshot;
                    var platformFeeRate = booking.PlatformFeeRateSnapshot == 0 ? 0.05m : booking.PlatformFeeRateSnapshot;

                    var vatAmount = Math.Round(grossAmount * vatRate, 0);
                    var platformFee = Math.Round(grossAmount * platformFeeRate, 0);
                    var ownerNetAmount = grossAmount - vatAmount - platformFee;

                    var invoice = new Invoice
                    {
                        BookingId = booking.Id,
                        ChargingAmount = ownerNetAmount,
                        ServiceAmount = 0,
                        VatAmount = vatAmount,
                        PlatformFee = platformFee,
                        TotalAmount = grossAmount,
                        Status = InvoiceStatus.PendingConfirm, // Chờ Driver review 24h
                        CreatedAt = now,
                        UpdatedAt = now
                    };
                    invoiceRepo.Add(invoice);

                    // 3. Set CompletedPendingInvoice (cho Driver 24h review/dispute)
                    booking.Status = BookingStatus.CompletedPendingInvoice;
                    booking.UpdatedAt = now;
                    bookingRepo.Update(booking);

                    // 4. Release slot
                    if (booking.ChargingSlot != null && booking.ChargingSlot.Status == SlotStatus.Booked)
                    {
                        booking.ChargingSlot.Status = SlotStatus.Active;
                        booking.ChargingSlot.UpdatedAt = now;
                        var slotRepo = scope.ServiceProvider.GetRequiredService<IChargingSlotRepository>();
                        slotRepo.Update(booking.ChargingSlot);
                    }

                    await unitOfWork.CompleteAsync();

                    // Loyalty + Settlement sẽ do ConfirmCompletionAsync hoặc InvoiceAutoConfirmJob xử lý

                    await transaction.CommitAsync();

                    // Notifications (ngoài transaction)
                    await notificationService.SendAsync(
                        booking.DriverUserId,
                        "Phiên sạc đã kết thúc tự động — vui lòng xác nhận",
                        $"Phiên sạc tại slot {booking.ChargingSlot?.SlotName} — trạm {booking.ChargingSlot?.ChargingStation?.Name} đã tự động kết thúc. Hóa đơn {grossAmount:N0}đ đã được tạo. Bạn có 24h để xác nhận hoặc khiếu nại.",
                        NotificationType.Booking);

                    var ownerUserId = booking.ChargingSlot?.ChargingStation?.OwnerUserId;
                    if (ownerUserId.HasValue)
                    {
                        await notificationService.SendAsync(
                            ownerUserId.Value,
                            "Phiên sạc đã kết thúc tự động",
                            $"Phiên sạc tại slot {booking.ChargingSlot?.SlotName} đã tự động kết thúc (quá thời gian). Chờ Driver xác nhận hóa đơn (24h).",
                            NotificationType.Payment);
                    }

                    _logger.LogInformation("Booking {BookingId} auto-stopped overtime → CompletedPendingInvoice (Driver has 24h to review/dispute).", booking.Id);
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Error auto-stopping CheckedIn booking {BookingId}", booking.Id);
                }
            }
        }

    }
}
