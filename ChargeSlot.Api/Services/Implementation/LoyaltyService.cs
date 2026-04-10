using ChargeSlot.Api.DTOs.Loyalty;
using ChargeSlot.Api.Repositories.Interfaces;
using ChargeSlot.Api.Services.Interfaces;

namespace ChargeSlot.Api.Services.Implementation
{
    public class LoyaltyService : ILoyaltyService
    {
        private readonly ILoyaltyRepository _loyaltyRepo;
        private readonly IDriverRepository _driverRepo;
        private readonly ISystemConfigService _configService;

        public LoyaltyService(
            ILoyaltyRepository loyaltyRepo,
            IDriverRepository driverRepo,
            ISystemConfigService configService)
        {
            _loyaltyRepo = loyaltyRepo;
            _driverRepo = driverRepo;
            _configService = configService;
        }

        public async Task<LoyaltyInfoDto> GetLoyaltyInfoAsync(int driverUserId)
        {
            var driver = await _driverRepo.GetByUserIdAsync(driverUserId);
            if (driver == null)
            {
                throw new InvalidOperationException("Driver profile không tồn tại.");
            }

            var earnRate = await _configService.GetDecimalAsync(Constants.SystemConfigKeys.Loyalty_Earn_Rate, 0.05m);
            var maxRedeemRate = 1.0m; // 100% - User can spend all points

            var transactions = await _loyaltyRepo.GetRecentHistoryAsync(driverUserId, 20);

            var history = transactions.Select(t => new LoyaltyTransactionDto
            {
                Id = t.Id,
                BookingId = t.BookingId,
                Type = t.Type,
                Points = t.Points,
                Description = t.Description,
                CreatedAt = t.CreatedAt
            }).ToList();

            return new LoyaltyInfoDto
            {
                CurrentPoints = driver.LoyaltyPoints,
                EarnRate = earnRate,
                MaxRedeemRate = maxRedeemRate,
                RecentHistory = history
            };
        }
    }
}
