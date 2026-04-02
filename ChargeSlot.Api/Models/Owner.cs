using ChargeSlot.Api.Models.Identity;

using ChargeSlot.Api.Helpers;
namespace ChargeSlot.Api.Models
{
    /// <summary>SRS 1.5 Owner - extends User, business info. Payout via BankAccount.</summary>
    public class Owner
    {
        public int UserId { get; set; }
        public ApplicationUser User { get; set; } = null!;

        public string BusinessName { get; set; } = null!;
        public string TaxCode { get; set; } = null!;
        public DateTime CreatedAt { get; set; } = DateTimeHelper.VietnamNow();

        public ICollection<ChargingStation> ChargingStations { get; set; } = new List<ChargingStation>();
    }
}
