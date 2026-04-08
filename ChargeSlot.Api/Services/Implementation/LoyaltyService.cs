using ChargeSlot.Api.DTOs.Loyalty;
using ChargeSlot.Api.Repositories.Interfaces;
using ChargeSlot.Api.Services.Interfaces;

namespace ChargeSlot.Api.Services.Implementation
{
    public class LoyaltyService : ILoyaltyService
    {
        private readonly ILoyaltyRepository _loyaltyRepo;
        private readonly IDriverRepository _driverRepo;
        private readonly ISystemConfigRepository _configRepo;

        public LoyaltyService(
            ILoyaltyRepository loyaltyRepo,
            IDriverRepository driverRepo,
            ISystemConfigRepository configRepo)
        {
            _loyaltyRepo = loyaltyRepo;
            _driverRepo = driverRepo;
            _configRepo = configRepo;
        }

        public async Task<LoyaltyInfoDto> GetLoyaltyInfoAsync(int driverUserId)
        {
            var driver = await _driverRepo.GetByUserIdAsync(driverUserId);
            if (driver == null)
            {
                throw new InvalidOperationException("Driver profile không tồn tại.");
            }

            var earnRateConfig = await _configRepo.GetByKeyAsync("LoyaltyEarnRate");
            var maxRedeemConfig = await _configRepo.GetByKeyAsync("LoyaltyMaxRedeemRate");
            
            var earnRate = decimal.TryParse(earnRateConfig?.Value, out var er) ? er : 0.05m;
            var maxRedeemRate = decimal.TryParse(maxRedeemConfig?.Value, out var mr) ? mr : 0.5m;

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
