using ChargeSlot.Api.Constants;
using ChargeSlot.Api.Data;
using ChargeSlot.Api.Enums;
using ChargeSlot.Api.Models;
using ChargeSlot.Api.Models.Identity;
using ChargeSlot.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

using ChargeSlot.Api.Helpers;
namespace ChargeSlot.Api.BackgroundJobs
{
    /// <summary>
    /// Job kiểm tra và tự động mở khóa (unban) cho Driver hoặc Station 
    /// đã hết thời hạn đình chỉ 30 ngày (BannedUntil <= Now).
    /// Chạy mỗi 1 phút.
    /// </summary>
    public class UnbanAutoJob : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<UnbanAutoJob> _logger;

        public UnbanAutoJob(IServiceProvider serviceProvider, ILogger<UnbanAutoJob> logger)
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
                    await ProcessUnbanAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[UnbanAutoJob] Error in auto-unban process.");
                }

                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }

        private async Task ProcessUnbanAsync(CancellationToken stoppingToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ChargeSlotDbContext>();
            var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

            var now = DateTimeHelper.VietnamNow();

            // 1. Unban Users (Drivers / Owners)
            var usersToUnban = await db.Users
                .Where(u => u.Status == UserStatusConstants.Suspended 
                         && u.BannedUntil != null 
                         && u.BannedUntil <= now)
                .ToListAsync(stoppingToken);

            foreach (var user in usersToUnban)
            {
                user.Status = UserStatusConstants.Active;
                user.BannedUntil = null;
                db.Users.Update(user);

                _logger.LogInformation("[UnbanAutoJob] Unbanned User ID: {UserId}", user.Id);

                await notificationService.SendAsync(
                    user.Id,
                    "Tài khoản đã được mở khóa",
                    "Thời gian đình chỉ 30 ngày đã kết thúc. Tài khoản của bạn đã có thể hoạt động bình thường trở lại.",
                    NotificationType.System);
            }

            // 2. Unban Stations
            var stationsToUnban = await db.ChargingStations
                .Where(s => s.BannedUntil != null 
                         && s.BannedUntil <= now) // we don't strictly check OperationalStatus=Inactive here because maybe owner manually activated it but we still need to clear BannedUntil
                .ToListAsync(stoppingToken);

            foreach (var station in stationsToUnban)
            {
                // Chỉ set Active nếu nó đang là Inactive (trường hợp bị system khóa). 
                // Có thể owner tự khóa bảo trì, kệ nó. Mở khóa này là xóa cờ Suspended.
                if (station.OperationalStatus == OperationalStatus.Inactive)
                {
                    station.OperationalStatus = OperationalStatus.Active;
                }
                station.BannedUntil = null;
                db.ChargingStations.Update(station);

                _logger.LogInformation("[UnbanAutoJob] Unbanned Station ID: {StationId}", station.Id);

                await notificationService.SendAsync(
                    station.OwnerUserId,
                    "Trạm sạc đã được mở khóa",
                    $"Thời gian hạn chế 30 ngày cho trạm {station.Name} đã kết thúc. Trạm đã hoạt động bình thường trở lại.",
                    NotificationType.System);
            }

            if (usersToUnban.Any() || stationsToUnban.Any())
            {
                await db.SaveChangesAsync(stoppingToken);
            }
        }
    }
}
