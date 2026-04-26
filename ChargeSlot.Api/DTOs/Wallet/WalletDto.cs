namespace ChargeSlot.Api.DTOs.Wallet
{
    public class WalletDto
    {
        public int Id { get; set; }
        public int? UserId { get; set; }
        public string? OwnerName { get; set; }
        public string? SystemCode { get; set; }
        public decimal AvailableBalance { get; set; }
        public decimal FrozenBalance { get; set; }
        public string WalletType { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
    }
}
