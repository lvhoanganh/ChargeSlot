using ChargeSlot.Api.Constants;
using ChargeSlot.Api.Data;
using Microsoft.EntityFrameworkCore;

using ChargeSlot.Api.Helpers;
namespace ChargeSlot.Api.BackgroundJobs
{
    /// <summary>
    /// Job tự động xoá các tài khoản có status PENDING_EMAIL_VERIFICATION
    /// đã quá 24 giờ mà chưa xác thực email.
    /// Chạy mỗi 30 phút.
    /// </summary>
    public class EmailVerificationCleanupJob : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<EmailVerificationCleanupJob> _logger;

        public EmailVerificationCleanupJob(IServiceProvider serviceProvider, ILogger<EmailVerificationCleanupJob> logger)
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
                    await CleanupExpiredPendingAccountsAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[EmailVerificationCleanupJob] Error in cleanup process.");
                }

                await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken);
            }
        }

        private async Task CleanupExpiredPendingAccountsAsync(CancellationToken stoppingToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ChargeSlotDbContext>();

            var cutoff = DateTimeHelper.VietnamNow().AddHours(-24);

            var expiredUsers = await db.Users
                .Where(u => u.Status == UserStatusConstants.PendingEmailVerification
                         && u.CreatedAt < cutoff)
                .ToListAsync(stoppingToken);

            if (!expiredUsers.Any()) return;

            foreach (var user in expiredUsers)
            {
                _logger.LogInformation(
                    "[EmailVerificationCleanupJob] Removing expired pending account: UserId={UserId}, Phone={Phone}, Email={Email}, CreatedAt={CreatedAt}",
                    user.Id, user.PhoneNumber, user.Email, user.CreatedAt);

                // Xoá các related entities trước (Driver/Owner profiles)
                var driver = await db.Driver.FirstOrDefaultAsync(d => d.UserId == user.Id, stoppingToken);
                if (driver != null) db.Driver.Remove(driver);

                var owner = await db.Owner.FirstOrDefaultAsync(o => o.UserId == user.Id, stoppingToken);
                if (owner != null) db.Owner.Remove(owner);

                // Xoá refresh tokens
                var tokens = await db.RefreshTokens.Where(t => t.UserId == user.Id).ToListAsync(stoppingToken);
                db.RefreshTokens.RemoveRange(tokens);

                // Xoá user roles (EF Identity)
                var userRoles = await db.UserRoles.Where(ur => ur.UserId == user.Id).ToListAsync(stoppingToken);
                db.UserRoles.RemoveRange(userRoles);

                // Xoá user
                db.Users.Remove(user);
            }

            await db.SaveChangesAsync(stoppingToken);

            _logger.LogInformation(
                "[EmailVerificationCleanupJob] Cleaned up {Count} expired pending accounts.", 
                expiredUsers.Count);
        }
    }
}
