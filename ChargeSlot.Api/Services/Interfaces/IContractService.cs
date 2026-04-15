using ChargeSlot.Api.DTOs.Contract;

namespace ChargeSlot.Api.Services.Interfaces
{
    public interface IContractService
    {
        /// <summary>Tạo hợp đồng tự động sau khi KYC Approved.</summary>
        Task CreateContractAsync(int ownerUserId);

        /// <summary>Owner xem preview hợp đồng (HTML + trạng thái).</summary>
        Task<ContractPreviewDto> GetContractPreviewAsync(int ownerUserId);

        /// <summary>Owner ký hợp đồng bằng chữ ký tay.</summary>
        Task<ContractPreviewDto> SignContractAsync(int ownerUserId, SignContractDto dto);

        /// <summary>Download PDF đã ký.</summary>
        Task<byte[]> DownloadContractPdfAsync(int ownerUserId);

        // ── Admin ──
        Task<ContractPreviewDto?> GetContractByOwnerForAdminAsync(int ownerUserId);
        Task<List<ContractPreviewDto>> GetAllContractsAsync(string? status = null);
        Task<ChargeSlot.Api.DTOs.PagedResultDto<ContractPreviewDto>> GetAllContractsPagedAsync(string? status, int page, int pageSize);
        Task<byte[]?> DownloadContractPdfForAdminAsync(int ownerUserId);
        Task TerminateContractAsync(int ownerUserId, string reason);

        /// <summary>Owner yêu cầu chấm dứt hợp đồng sớm (yêu cầu không có booking đang hoạt động).</summary>
        Task RequestTerminationAsync(int ownerUserId, string reason);
    }
}
