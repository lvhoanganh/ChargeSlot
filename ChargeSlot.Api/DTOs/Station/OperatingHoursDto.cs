namespace ChargeSlot.Api.DTOs.Station
{
    public class OperatingHoursDto
    {
        public byte DayOfWeek { get; set; }
        public bool IsClosed { get; set; }
        public TimeOnly? OpenTime { get; set; }
        public TimeOnly? CloseTime { get; set; }
    }
}
