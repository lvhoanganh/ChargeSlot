using ChargeSlot.Api.Enums;
using ChargeSlot.Api.Helpers;
using ChargeSlot.Api.Repositories.Interfaces;
using ChargeSlot.Api.Services.Interfaces;

namespace ChargeSlot.Api.BackgroundJobs
{
    /// <summary>
    /// Gửi nhắc nhở (notification + email) trước khi hệ thống tự động xử lý.
    /// Quét mỗi 5 phút, gửi reminder 1 lần duy nhất trước deadline 1 giờ.
    /// 
    /// Các scenario:
    /// 1. Invoice PendingConfirm → nhắc Driver xác nhận trước auto-confirm (24h)
    /// 2. Withdraw TransferCompleted → nhắc User xác nhận trước auto-confirm (24h)
    /// 3. Dispute OwnerEvidenceDeadline → nhắc Owner nộp bằng chứng
    /// 4. Dispute AdminReviewDeadline → nhắc Admin phán quyết
    /// 5. Booking Paid sắp đến giờ sạc → nhắc Driver chuẩn bị check-in
    /// </summary>
    public class DeadlineReminderJob : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<DeadlineReminderJob> _logger;



        public DeadlineReminderJob(IServiceProvider serviceProvider, ILogger<DeadlineReminderJob> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Chờ app khởi động xong 30s
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var configScope = _serviceProvider.CreateScope();
                    var configService = configScope.ServiceProvider.GetRequiredService<ISystemConfigService>();
                    var reminderHours = await configService.GetIntAsync(Constants.SystemConfigKeys.Reminder_Window_Hours, 1);
                    var autoConfirmHours = await configService.GetIntAsync(Constants.SystemConfigKeys.Invoice_AutoConfirm_Hours, 24);
                    var withdrawAutoHours = await configService.GetIntAsync(Constants.SystemConfigKeys.Withdraw_AutoConfirm_Hours, 24);
                    var reminderWindow = TimeSpan.FromHours(reminderHours);

                    await RemindInvoicePendingConfirmAsync(autoConfirmHours, reminderHours, stoppingToken);
                    await RemindWithdrawPendingConfirmAsync(withdrawAutoHours, reminderHours, stoppingToken);
                    await RemindDisputeOwnerEvidenceAsync(reminderWindow, stoppingToken);
                    await RemindDisputeAdminReviewAsync(reminderWindow, stoppingToken);
                    await RemindBookingApproachingAsync(reminderWindow, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[DeadlineReminderJob] Unhandled error in job loop");
                }

                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }

        // ═══════════════════════════════════════════════════════
        // 1. Invoice PendingConfirm > 23h (tức còn <1h trước auto-confirm)
        //    → Nhắc Driver xác nhận hoặc khiếu nại
        // ═══════════════════════════════════════════════════════
        private async Task RemindInvoicePendingConfirmAsync(int autoConfirmHours, int reminderHours, CancellationToken ct)
        {
            using var scope = _serviceProvider.CreateScope();
            var invoiceRepo = scope.ServiceProvider.GetRequiredService<IInvoiceRepository>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

            var now = DateTimeHelper.VietnamNow();
            // Invoice tạo > (autoConfirmHours - reminderHours)h nhưng < autoConfirmHoursH
            var reminderStart = now.AddHours(-autoConfirmHours);
            var reminderEnd = now.AddHours(-(autoConfirmHours - reminderHours));

            var invoices = await invoiceRepo.GetPendingConfirmForReminderAsync(reminderStart, reminderEnd);

            foreach (var invoice in invoices)
            {
                try
                {
                    var booking = invoice.Booking;
                    if (booking == null) continue; // Safety: Invoice thiếu Booking nav

                    var stationName = booking.ChargingSlot?.ChargingStation?.Name ?? "Trạm";
                    var slotName = booking.ChargingSlot?.SlotName ?? "Slot";

                    await notificationService.SendAsync(
                        booking.DriverUserId,
                        "⏰ Nhắc nhở: Hóa đơn sắp tự động xác nhận",
                        $"Hóa đơn tại {slotName} — {stationName} sẽ được tự động xác nhận sau 1 giờ nữa. "
                        + "Nếu có vấn đề, vui lòng tạo khiếu nại ngay.",
                        NotificationType.System);

                    // Đánh dấu đã gửi reminder
                    await invoiceRepo.MarkReminderSentAsync(invoice.Id, now);
                    await unitOfWork.CompleteAsync();

                    _logger.LogInformation(
                        "[DeadlineReminderJob] Sent invoice reminder for Invoice #{InvoiceId} to Driver {DriverUserId}",
                        invoice.Id, booking.DriverUserId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[DeadlineReminderJob] Failed reminder for Invoice #{InvoiceId}", invoice.Id);
                }
            }
        }

        // ═══════════════════════════════════════════════════════
        // 2. Withdraw TransferCompleted > 23h → nhắc User xác nhận nhận tiền
        // ═══════════════════════════════════════════════════════
        private async Task RemindWithdrawPendingConfirmAsync(int autoConfirmHours, int reminderHours, CancellationToken ct)
        {
            using var scope = _serviceProvider.CreateScope();
            var withdrawRepo = scope.ServiceProvider.GetRequiredService<IWithdrawRequestRepository>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

            var now = DateTimeHelper.VietnamNow();
            var reminderStart = now.AddHours(-autoConfirmHours);
            var reminderEnd = now.AddHours(-(autoConfirmHours - reminderHours));

            var requests = await withdrawRepo.GetTransferCompletedForReminderAsync(reminderStart, reminderEnd);

            foreach (var req in requests)
            {
                try
                {
                    await notificationService.SendAsync(
                        req.UserId,
                        "⏰ Nhắc nhở: Xác nhận rút tiền trước khi tự động hoàn tất",
                        $"Yêu cầu rút {req.Amount:N0} VND sẽ tự động xác nhận sau 1 giờ nữa. "
                        + "Nếu chưa nhận được tiền, vui lòng báo cáo ngay.",
                        NotificationType.Wallet);

                    req.ReminderSentAt = now;
                    await unitOfWork.CompleteAsync();

                    _logger.LogInformation(
                        "[DeadlineReminderJob] Sent withdraw reminder for Withdraw #{Id} to User {UserId}",
                        req.Id, req.UserId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[DeadlineReminderJob] Failed reminder for Withdraw #{Id}", req.Id);
                }
            }
        }

        // ═══════════════════════════════════════════════════════
        // 3. Dispute OwnerEvidenceDeadline sắp hết → nhắc Owner nộp bằng chứng
        // ═══════════════════════════════════════════════════════
        private async Task RemindDisputeOwnerEvidenceAsync(TimeSpan reminderWindow, CancellationToken ct)
        {
            using var scope = _serviceProvider.CreateScope();
            var disputeRepo = scope.ServiceProvider.GetRequiredService<IDisputeRepository>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

            var now = DateTimeHelper.VietnamNow();
            var reminderCutoff = now.Add(reminderWindow); // Deadline trong vòng reminderWindow tới

            var disputes = await disputeRepo.GetOwnerEvidenceForReminderAsync(now, reminderCutoff);

            foreach (var dispute in disputes)
            {
                try
                {
                    var stationName = dispute.Booking?.ChargingSlot?.ChargingStation?.Name ?? "Trạm";
                    var ownerUserId = dispute.Booking?.ChargingSlot?.ChargingStation?.OwnerUserId;
                    if (!ownerUserId.HasValue) continue;

                    var minutesLeft = (int)(dispute.OwnerEvidenceDeadlineAt!.Value - now).TotalMinutes;

                    await notificationService.SendAsync(
                        ownerUserId.Value,
                        "⏰ Nhắc nhở: Nộp bằng chứng khiếu nại ngay!",
                        $"Khiếu nại #{dispute.Id} tại {stationName} sẽ tự động phán quyết sau {minutesLeft} phút nữa "
                        + "do không nhận được phản hồi từ bạn. Vui lòng nộp bằng chứng ngay.",
                        NotificationType.System);

                    await disputeRepo.MarkOwnerReminderSentAsync(dispute.Id, now);
                    await unitOfWork.CompleteAsync();

                    _logger.LogInformation(
                        "[DeadlineReminderJob] Sent dispute owner evidence reminder for Dispute #{Id}",
                        dispute.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[DeadlineReminderJob] Failed reminder for Dispute #{Id}", dispute.Id);
                }
            }
        }

        // ═══════════════════════════════════════════════════════
        // 4. Dispute AdminReviewDeadline sắp hết → nhắc Admin phán quyết
        // ═══════════════════════════════════════════════════════
        private async Task RemindDisputeAdminReviewAsync(TimeSpan reminderWindow, CancellationToken ct)
        {
            using var scope = _serviceProvider.CreateScope();
            var disputeRepo = scope.ServiceProvider.GetRequiredService<IDisputeRepository>();
            var adminAccountRepo = scope.ServiceProvider.GetRequiredService<IAdminAccountRepository>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

            var now = DateTimeHelper.VietnamNow();
            var reminderCutoff = now.Add(reminderWindow);

            var disputes = await disputeRepo.GetAdminReviewForReminderAsync(now, reminderCutoff);

            // Tìm tất cả Admin users
            var adminUserIds = await adminAccountRepo.GetAdminUserIdsAsync();

            foreach (var dispute in disputes)
            {
                try
                {
                    var minutesLeft = (int)(dispute.AdminReviewDeadlineAt!.Value - now).TotalMinutes;

                    foreach (var adminId in adminUserIds)
                    {
                        await notificationService.SendAsync(
                            adminId,
                            "⏰ Nhắc nhở: Dispute sắp tự động phán quyết",
                            $"Khiếu nại #{dispute.Id} sẽ tự động phán quyết theo mặc định sau {minutesLeft} phút nữa. "
                            + "Vui lòng xem xét và phán quyết ngay.",
                            NotificationType.System);
                    }

                    await disputeRepo.MarkAdminReminderSentAsync(dispute.Id, now);
                    await unitOfWork.CompleteAsync();

                    _logger.LogInformation(
                        "[DeadlineReminderJob] Sent dispute admin review reminder for Dispute #{Id}",
                        dispute.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[DeadlineReminderJob] Failed admin reminder for Dispute #{Id}", dispute.Id);
                }
            }
        }

        // ═══════════════════════════════════════════════════════
        // 5. Booking Paid → sắp đến giờ sạc (1h trước StartTime)
        //    → Nhắc Driver chuẩn bị check-in
        // ═══════════════════════════════════════════════════════
        private async Task RemindBookingApproachingAsync(TimeSpan reminderWindow, CancellationToken ct)
        {
            using var scope = _serviceProvider.CreateScope();
            var bookingRepo = scope.ServiceProvider.GetRequiredService<IBookingRepository>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

            var now = DateTimeHelper.VietnamNow();
            var reminderCutoff = now.Add(reminderWindow);

            var bookings = await bookingRepo.GetApproachingPaidBookingsAsync(now, reminderCutoff);

            foreach (var booking in bookings)
            {
                try
                {
                    var stationName = booking.ChargingSlot?.ChargingStation?.Name ?? "Trạm";
                    var slotName = booking.ChargingSlot?.SlotName ?? "Slot";
                    var minutesLeft = (int)(booking.StartTime - now).TotalMinutes;

                    await notificationService.SendAsync(
                        booking.DriverUserId,
                        "⏰ Sắp đến giờ sạc!",
                        $"Lịch sạc tại {slotName} — {stationName} sẽ bắt đầu sau {minutesLeft} phút nữa "
                        + $"({booking.StartTime:HH:mm} - {booking.EndTime:HH:mm}). Vui lòng di chuyển đến trạm và chuẩn bị check-in.",
                        NotificationType.Booking);

                    // Notify Owner chuẩn bị
                    var ownerUserId = booking.ChargingSlot?.ChargingStation?.OwnerUserId;
                    if (ownerUserId.HasValue)
                    {
                        await notificationService.SendAsync(
                            ownerUserId.Value,
                            "📋 Khách sắp đến sạc",
                            $"Khách đã đặt {slotName} — {stationName} lúc {booking.StartTime:HH:mm}. "
                            + "Vui lòng chuẩn bị slot sẵn sàng.",
                            NotificationType.Booking);
                    }

                    await bookingRepo.MarkReminderSentAsync(booking.Id, now);
                    await unitOfWork.CompleteAsync();

                    _logger.LogInformation(
                        "[DeadlineReminderJob] Sent approaching reminder for Booking #{Id} to Driver {DriverUserId}",
                        booking.Id, booking.DriverUserId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[DeadlineReminderJob] Failed reminder for Booking #{Id}", booking.Id);
                }
            }
        }
    }
}
