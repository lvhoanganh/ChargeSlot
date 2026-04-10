namespace ChargeSlot.Api.DTOs.ChargingSession
{
    public class ChargingSessionDto
    {
        public int Id { get; set; }
        public int BookingId { get; set; }
        public int SlotId { get; set; }
        public string SlotName { get; set; } = null!;
        public int StationId { get; set; }
        public string StationName { get; set; } = null!;
        public string DriverName { get; set; } = null!;
        public DateTime? CheckinTime { get; set; }
        public DateTime? ActualStartTime { get; set; }
        public DateTime? ActualEndTime { get; set; }
        public decimal? ActualDurationHours { get; set; }
        public DateTime BookingStartTime { get; set; }
        public DateTime BookingEndTime { get; set; }
        public decimal TotalAmount { get; set; }
        public string BookingStatus { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
    }
}
