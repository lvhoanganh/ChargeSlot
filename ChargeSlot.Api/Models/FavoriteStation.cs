using ChargeSlot.Api.Models.Identity;

using ChargeSlot.Api.Helpers;
namespace ChargeSlot.Api.Models
{
    /// <summary>Driver yêu thích trạm sạc (kiểu Be).</summary>
    public class FavoriteStation
    {
        public int Id { get; set; }
        public int DriverUserId { get; set; }
        public ApplicationUser DriverUser { get; set; } = null!;
        public int StationId { get; set; }
        public ChargingStation Station { get; set; } = null!;
        public DateTime CreatedAt { get; set; } = DateTimeHelper.VietnamNow();
    }
}
