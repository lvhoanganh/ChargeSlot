namespace ChargeSlot.Api.DTOs.Station
{
    public class UnavailableDateDto
    {
        public int Id { get; set; }
        public int StationId { get; set; }
        public DateOnly Date { get; set; }
        public string? Reason { get; set; }
    }

    public class AddUnavailableDatesDto
    {
        /// <summary>
        /// Danh sách các ngày cần khóa trạm (định dạng YYYY-MM-DD).
        /// </summary>
        public List<DateOnly> Dates { get; set; } = new();
        public string? Reason { get; set; }
    }

    public class RemoveUnavailableDatesDto
    {
        /// <summary>
        /// Danh sách ID của các đối tượng UnavailableDate cần xóa.
        /// </summary>
        public List<int> Ids { get; set; } = new();
    }
}
