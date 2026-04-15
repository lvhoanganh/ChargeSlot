using ChargeSlot.Api.Enums;
using ChargeSlot.Api.Helpers;
using ChargeSlot.Api.Repositories.Interfaces;
using ChargeSlot.Api.Services.Interfaces;

namespace ChargeSlot.Api.BackgroundJobs
{
    /// <summary>
    /// Background job xử lý hợp đồng hết hạn:
    /// 1. Gửi nhắc nhở 30 ngày trước khi hết hạn
    /// 2. Tự động gia hạn hợp đồng đã hết hạn (theo Điều 5.2)
    /// Chạy mỗi 6 giờ.
    /// </summary>
    public class ContractExpiryJob : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ContractExpiryJob> _logger;

        public ContractExpiryJob(IServiceProvider serviceProvider, ILogger<ContractExpiryJob> logger)
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
                    await ProcessRenewalRemindersAsync(stoppingToken);
                    await ProcessExpiredContractsAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[ContractExpiry] Unhandled error in job loop");
                }

                await Task.Delay(TimeSpan.FromHours(6), stoppingToken);
            }
        }

        /// <summary>
        /// Gửi nhắc nhở cho Owner có hợp đồng sắp hết hạn trong 30 ngày.
        /// </summary>
        private async Task ProcessRenewalRemindersAsync(CancellationToken ct)
        {
            using var scope = _serviceProvider.CreateScope();
            var contractRepo = scope.ServiceProvider.GetRequiredService<IContractRepository>();
            var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var now = DateTimeHelper.VietnamNow();
            var deadline = now.AddDays(30);

            var expiringContracts = await contractRepo.GetExpiringAsync(deadline);

            foreach (var contract in expiringContracts)
            {
                if (ct.IsCancellationRequested) break;

                try
                {
                    var daysLeft = (contract.ExpiresAt!.Value - now).Days;

                    await notificationService.SendAsync(
                        contract.OwnerUserId,
                        "Hợp đồng sắp hết hạn",
                        $"Hợp đồng {contract.ContractNumber} sẽ hết hạn sau {daysLeft} ngày (ngày {contract.ExpiresAt.Value:dd/MM/yyyy}). " +
                        "Hợp đồng sẽ tự động gia hạn thêm 12 tháng theo Điều 5.2.",
                        NotificationType.System);

                    contract.RenewalNotifiedAt = now;
                    contractRepo.Update(contract);
                    await unitOfWork.CompleteAsync();

                    _logger.LogInformation(
                        "[ContractExpiry] Sent renewal reminder for {ContractNumber} to Owner {UserId} ({DaysLeft} days left)",
                        contract.ContractNumber, contract.OwnerUserId, daysLeft);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "[ContractExpiry] Error sending reminder for contract {ContractNumber}",
                        contract.ContractNumber);
                }
            }
        }

        /// <summary>
        /// Tự động gia hạn hợp đồng đã hết hạn (Điều 5.2: tự động gia hạn 12 tháng).
        /// </summary>
        private async Task ProcessExpiredContractsAsync(CancellationToken ct)
        {
            using var scope = _serviceProvider.CreateScope();
            var contractRepo = scope.ServiceProvider.GetRequiredService<IContractRepository>();
            var ownerRepo = scope.ServiceProvider.GetRequiredService<IOwnerRepository>();
            var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var now = DateTimeHelper.VietnamNow();
            var expiredContracts = await contractRepo.GetExpiredSignedAsync(now);

            foreach (var contract in expiredContracts)
            {
                if (ct.IsCancellationRequested) break;

                try
                {
                    // Bug #8 fix: Kiểm tra Owner có bị Rejected KYC không
                    var owner = await ownerRepo.GetByUserIdAsync(contract.OwnerUserId);
                    if (owner != null && owner.KycStatus == KycStatus.Rejected)
                    {
                        // Không gia hạn → chuyển sang Expired
                        contract.Status = ContractStatus.Expired;
                        contractRepo.Update(contract);
                        await unitOfWork.CompleteAsync();

                        await notificationService.SendAsync(
                            contract.OwnerUserId,
                            "Hợp đồng đã hết hạn",
                            $"Hợp đồng {contract.ContractNumber} đã hết hạn và không được gia hạn do hồ sơ KYC chưa đạt yêu cầu.",
                            NotificationType.System);

                        _logger.LogWarning(
                            "[ContractExpiry] Contract {ContractNumber} expired (not renewed): Owner {UserId} KYC is Rejected.",
                            contract.ContractNumber, contract.OwnerUserId);
                        continue;
                    }

                    // Gia hạn: ExpiresAt += 12 tháng, reset thông báo
                    var oldExpiry = contract.ExpiresAt!.Value;
                    contract.ExpiresAt = oldExpiry.AddMonths(contract.ContractDurationMonths > 0
                        ? contract.ContractDurationMonths
                        : 12);
                    contract.RenewalNotifiedAt = null; // Reset để gửi lại nhắc nhở lần sau
                    contractRepo.Update(contract);
                    await unitOfWork.CompleteAsync();

                    await notificationService.SendAsync(
                        contract.OwnerUserId,
                        "Hợp đồng đã được gia hạn tự động",
                        $"Hợp đồng {contract.ContractNumber} đã được tự động gia hạn thêm 12 tháng đến ngày {contract.ExpiresAt.Value:dd/MM/yyyy} theo Điều 5.2.",
                        NotificationType.System);

                    _logger.LogInformation(
                        "[ContractExpiry] Auto-renewed contract {ContractNumber} for Owner {UserId}: {OldExpiry:dd/MM/yyyy} → {NewExpiry:dd/MM/yyyy}",
                        contract.ContractNumber, contract.OwnerUserId, oldExpiry, contract.ExpiresAt.Value);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "[ContractExpiry] Error auto-renewing contract {ContractNumber}",
                        contract.ContractNumber);
                }
            }
        }
    }
}
