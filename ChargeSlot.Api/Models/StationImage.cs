using ChargeSlot.Api.Helpers;
namespace ChargeSlot.Api.Models
{
    public class StationImage
    {
        public int Id { get; set; }
        public int StationId { get; set; }
        public ChargingStation ChargingStation { get; set; } = null!;

        public string ImageUrl { get; set; } = null!;
        public DateTime CreatedAt { get; set; } = DateTimeHelper.VietnamNow();
    }
}
