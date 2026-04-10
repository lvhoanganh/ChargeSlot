namespace ChargeSlot.Api.DTOs.Loyalty
{
    public class LoyaltyInfoDto
    {
        public decimal CurrentPoints { get; set; }
        public decimal EarnRate { get; set; }
        public decimal MaxRedeemRate { get; set; }
        public List<LoyaltyTransactionDto> RecentHistory { get; set; } = new();
    }
}
