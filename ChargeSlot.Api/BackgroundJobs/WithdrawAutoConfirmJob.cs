using ChargeSlot.Api.Data;
using ChargeSlot.Api.Enums;
using ChargeSlot.Api.Models;
using ChargeSlot.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

using ChargeSlot.Api.Helpers;
namespace ChargeSlot.Api.BackgroundJobs
{
    /// <summary>
    /// Auto-confirm rút tiền sau 24h nếu User không xác nhận hoặc báo lỗi.
    /// Chạy mỗi 5 phút.
    /// Flow: WithdrawRequest TransferCompleted > 24h → auto Completed (trừ frozen + ghi ledger).
    /// </summary>
    public class WithdrawAutoConfirmJob : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<WithdrawAutoConfirmJob> _logger;

        private static readonly TimeSpan AutoConfirmDeadline = TimeSpan.FromHours(24);

        public WithdrawAutoConfirmJob(IServiceProvider serviceProvider, ILogger<WithdrawAutoConfirmJob> logger)
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
                    var deadline = DateTimeHelper.VietnamNow() - AutoConfirmDeadline;
                    List<int> expiredIds;

                    // 1. Lấy danh sách ID đã quá 24h
                    using (var outerScope = _serviceProvider.CreateScope())
                    {
                        var outerDb = outerScope.ServiceProvider.GetRequiredService<ChargeSlotDbContext>();
                        expiredIds = await outerDb.Set<WithdrawRequest>()
                            .Where(r => r.Status == WithdrawStatus.TransferCompleted
                                     && r.TransferredAt != null
                                     && r.TransferredAt <= deadline)
                            .Select(r => r.Id)
                            .ToListAsync(stoppingToken);
                    }

                    if (expiredIds.Count > 0)
                    {
                        _logger.LogInformation(
                            "[WithdrawAutoConfirm] Found {Count} withdraw request(s) past 24h deadline",
                            expiredIds.Count);
                    }

                    // 2. Xử lý từng request trong scope riêng
                    foreach (var requestId in expiredIds)
                    {
                        if (stoppingToken.IsCancellationRequested) break;

                        try
                        {
                            using var scope = _serviceProvider.CreateScope();
                            var db = scope.ServiceProvider.GetRequiredService<ChargeSlotDbContext>();
                            var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

                            var request = await db.Set<WithdrawRequest>()
                                .Include(r => r.User)
                                .Include(r => r.Wallet)
                                .FirstOrDefaultAsync(r => r.Id == requestId, stoppingToken);

                            if (request == null || request.Status != WithdrawStatus.TransferCompleted)
                                continue;

                            // M8 FIX: Dùng interface thay vì cast sang implementation
                            var walletService = scope.ServiceProvider.GetRequiredService<IWalletService>();
                            request.UserConfirmedAt = DateTimeHelper.VietnamNow(); // auto
                            await walletService.FinalizeWithdrawCompletedAsync(request);

                            _logger.LogInformation(
                                "[WithdrawAutoConfirm] Auto-confirmed withdraw #{Id} ({Amount} VND) for user {UserId}",
                                request.Id, request.Amount, request.UserId);

                            await notificationService.SendAsync(
                                request.UserId,
                                "Rút tiền hoàn tất (tự động)",
                                $"Yêu cầu rút {request.Amount:N0} VND đã được tự động xác nhận sau 24 giờ.",
                                NotificationType.Wallet);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex,
                                "[WithdrawAutoConfirm] Error processing withdraw #{Id}", requestId);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[WithdrawAutoConfirm] Unhandled error in job loop");
                }

                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }
    }
}
