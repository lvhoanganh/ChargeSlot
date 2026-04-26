using ChargeSlot.Api.Repositories.Interfaces;

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
            var userRepo = scope.ServiceProvider.GetRequiredService<IUserRepository>();
            var driverRepo = scope.ServiceProvider.GetRequiredService<IDriverRepository>();
            var ownerRepo = scope.ServiceProvider.GetRequiredService<IOwnerRepository>();
            var refreshTokenRepo = scope.ServiceProvider.GetRequiredService<IRefreshTokenRepository>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var cutoff = DateTimeHelper.VietnamNow().AddHours(-24);

            var expiredUsers = await userRepo.GetExpiredPendingVerificationAsync(cutoff);

            if (!expiredUsers.Any()) return;

            foreach (var user in expiredUsers)
            {
                _logger.LogInformation(
                    "[EmailVerificationCleanupJob] Removing expired pending account: UserId={UserId}, Phone={Phone}, Email={Email}, CreatedAt={CreatedAt}",
                    user.Id, user.PhoneNumber, user.Email, user.CreatedAt);

                // Xoá các related entities trước (Driver/Owner profiles)
                var driver = await driverRepo.GetByUserIdAsync(user.Id);
                if (driver != null) driverRepo.Remove(driver);

                var owner = await ownerRepo.GetByUserIdAsync(user.Id);
                if (owner != null) ownerRepo.Remove(owner);

                // Xoá refresh tokens
                await refreshTokenRepo.RemoveAllByUserIdAsync(user.Id);

                // Xoá user roles (EF Identity)
                await userRepo.RemoveUserRolesAsync(user.Id);

                // Xoá user
                userRepo.Remove(user);
            }

            await unitOfWork.CompleteAsync();

            _logger.LogInformation(
                "[EmailVerificationCleanupJob] Cleaned up {Count} expired pending accounts.", 
                expiredUsers.Count);
        }
    }
}
