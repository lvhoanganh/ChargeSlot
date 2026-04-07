using ChargeSlot.Api.Data;
using ChargeSlot.Api.Enums;
using ChargeSlot.Api.Helpers;
using ChargeSlot.Api.Models;
using ChargeSlot.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

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

        /// <summary>Gửi nhắc trước deadline bao lâu.</summary>
        private static readonly TimeSpan ReminderWindow = TimeSpan.FromHours(1);

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
                    await RemindInvoicePendingConfirmAsync(stoppingToken);
                    await RemindWithdrawPendingConfirmAsync(stoppingToken);
                    await RemindDisputeOwnerEvidenceAsync(stoppingToken);
                    await RemindDisputeAdminReviewAsync(stoppingToken);
                    await RemindBookingApproachingAsync(stoppingToken);
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
        private async Task RemindInvoicePendingConfirmAsync(CancellationToken ct)
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ChargeSlotDbContext>();
            var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

            var now = DateTimeHelper.VietnamNow();
            // Invoice tạo > 23h nhưng < 24h (tức còn 0-1 tiếng trước auto-confirm)
            var reminderStart = now.AddHours(-24);     // đã quá 24h → bỏ qua (auto-confirm đã chạy)
            var reminderEnd = now.AddHours(-23);       // tạo cách đây 23h → còn 1h

            var invoices = await db.Invoices
                .AsNoTracking()
                .Include(i => i.Booking).ThenInclude(b => b.ChargingSlot).ThenInclude(s => s.ChargingStation)
                .Where(i => i.Status == InvoiceStatus.PendingConfirm
                         && i.CreatedAt > reminderStart
                         && i.CreatedAt <= reminderEnd
                         && !i.ReminderSentAt.HasValue)
                .ToListAsync(ct);

            foreach (var invoice in invoices)
            {
                try
                {
                    var booking = invoice.Booking;
                    var stationName = booking?.ChargingSlot?.ChargingStation?.Name ?? "Trạm";
                    var slotName = booking?.ChargingSlot?.SlotName ?? "Slot";

                    await notificationService.SendAsync(
                        booking!.DriverUserId,
                        "⏰ Nhắc nhở: Hóa đơn sắp tự động xác nhận",
                        $"Hóa đơn tại {slotName} — {stationName} sẽ được tự động xác nhận sau 1 giờ nữa. "
                        + "Nếu có vấn đề, vui lòng tạo khiếu nại ngay.",
                        NotificationType.System);

                    // Đánh dấu đã gửi reminder (update trực tiếp)
                    await db.Invoices
                        .Where(i => i.Id == invoice.Id)
                        .ExecuteUpdateAsync(s => s.SetProperty(i => i.ReminderSentAt, now), ct);

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
        private async Task RemindWithdrawPendingConfirmAsync(CancellationToken ct)
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ChargeSlotDbContext>();
            var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

            var now = DateTimeHelper.VietnamNow();
            var reminderStart = now.AddHours(-24);
            var reminderEnd = now.AddHours(-23);

            var requests = await db.Set<WithdrawRequest>()
                .Where(r => r.Status == WithdrawStatus.TransferCompleted
                         && r.TransferredAt.HasValue
                         && r.TransferredAt.Value > reminderStart
                         && r.TransferredAt.Value <= reminderEnd
                         && !r.ReminderSentAt.HasValue)
                .ToListAsync(ct);

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
                    await db.SaveChangesAsync(ct);

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
        private async Task RemindDisputeOwnerEvidenceAsync(CancellationToken ct)
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ChargeSlotDbContext>();
            var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

            var now = DateTimeHelper.VietnamNow();
            var reminderCutoff = now.Add(ReminderWindow); // Deadline trong vòng 1h tới

            var disputes = await db.Disputes
                .AsNoTracking()
                .Include(d => d.Booking).ThenInclude(b => b.ChargingSlot).ThenInclude(s => s.ChargingStation)
                .Where(d => d.Status == DisputeStatus.Open
                         && d.OwnerEvidenceDeadlineAt.HasValue
                         && d.OwnerEvidenceDeadlineAt.Value > now
                         && d.OwnerEvidenceDeadlineAt.Value <= reminderCutoff
                         && !d.OwnerReminderSentAt.HasValue)
                .ToListAsync(ct);

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

                    await db.Disputes
                        .Where(d => d.Id == dispute.Id)
                        .ExecuteUpdateAsync(s => s.SetProperty(d => d.OwnerReminderSentAt, now), ct);

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
        private async Task RemindDisputeAdminReviewAsync(CancellationToken ct)
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ChargeSlotDbContext>();
            var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

            var now = DateTimeHelper.VietnamNow();
            var reminderCutoff = now.Add(ReminderWindow);

            var disputes = await db.Disputes
                .AsNoTracking()
                .Where(d => d.Status == DisputeStatus.PendingReview
                         && d.AdminReviewDeadlineAt.HasValue
                         && d.AdminReviewDeadlineAt.Value > now
                         && d.AdminReviewDeadlineAt.Value <= reminderCutoff
                         && !d.AdminReminderSentAt.HasValue)
                .ToListAsync(ct);

            // Tìm tất cả Admin users
            var adminUserIds = await db.UserRoles
                .Join(db.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => new { ur.UserId, r.Name })
                .Where(x => x.Name == "Admin")
                .Select(x => x.UserId)
                .ToListAsync(ct);

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

                    await db.Disputes
                        .Where(d => d.Id == dispute.Id)
                        .ExecuteUpdateAsync(s => s.SetProperty(d => d.AdminReminderSentAt, now), ct);

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
        private async Task RemindBookingApproachingAsync(CancellationToken ct)
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ChargeSlotDbContext>();
            var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

            var now = DateTimeHelper.VietnamNow();
            var reminderCutoff = now.Add(ReminderWindow);

            var bookings = await db.Bookings
                .AsNoTracking()
                .Include(b => b.ChargingSlot).ThenInclude(s => s.ChargingStation)
                .Where(b => b.Status == BookingStatus.Paid
                         && b.StartTime > now
                         && b.StartTime <= reminderCutoff
                         && !b.ReminderSentAt.HasValue)
                .ToListAsync(ct);

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

                    await db.Bookings
                        .Where(b => b.Id == booking.Id)
                        .ExecuteUpdateAsync(s => s.SetProperty(b => b.ReminderSentAt, now), ct);

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
