using ChargeSlot.Api.Helpers;

namespace ChargeSlot.Api.Models
{
    public class StationUnavailableDate
    {
        public int Id { get; set; }
        
        public int StationId { get; set; }
        public ChargingStation Station { get; set; } = null!;

        public DateOnly Date { get; set; }

        public string? Reason { get; set; }

        public DateTime CreatedAt { get; set; } = DateTimeHelper.VietnamNow();
    }
}
