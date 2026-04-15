namespace ChargeSlot.Api.DTOs
{
    public class PagedResultDto<T>
    {
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalItems { get; set; }
        public int TotalPages => PageSize > 0 ? (int)System.Math.Ceiling(TotalItems / (double)PageSize) : 0;
        public List<T> Items { get; set; } = new();
    }
}
