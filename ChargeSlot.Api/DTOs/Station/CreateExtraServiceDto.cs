namespace ChargeSlot.Api.DTOs.Station
{
    public class CreateExtraServiceDto
    {
        public string ServiceName { get; set; } = null!;
        public string? Description { get; set; }
        public decimal Price { get; set; }
        /// <summary>NULL = không giới hạn (dịch vụ). Có giá trị = số lượng vật lý cho thuê.</summary>
        public int? TotalStock { get; set; }
        /// <summary>Trả lại kho (true), Bán đứt/ tiêu hóa (false)</summary>
        public bool IsRental { get; set; } = false;
    }
}
