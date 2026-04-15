using ChargeSlot.Api.DTOs.Admin.Overview;

namespace ChargeSlot.Api.DTOs.Contract
{
    public class ContractFilterDto : PagedFilterDto
    {
        /// <summary>Lọc theo trạng thái: Pending, Signed, Expired, Terminated</summary>
        public string? Status { get; set; }

        /// <summary>Lọc theo OwnerUserId</summary>
        public int? OwnerUserId { get; set; }

        /// <summary>Tìm kiếm theo tên Owner hoặc số hợp đồng</summary>
        public string? Search { get; set; }
    }
}
