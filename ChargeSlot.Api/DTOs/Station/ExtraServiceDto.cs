namespace ChargeSlot.Api.DTOs.Station
{
    public class ExtraServiceDto
    {
        public int Id { get; set; }
        public string ServiceName { get; set; } = null!;
        public string? Description { get; set; }
        public decimal Price { get; set; }
        public int? TotalStock { get; set; }
        public bool IsRental { get; set; }
        public bool IsActive { get; set; }
    }
}
