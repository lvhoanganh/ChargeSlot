using ChargeSlot.Api.DTOs.Kyc;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ChargeSlot.Api.Services.Interfaces
{
    public interface IKycService
    {
        Task<OwnerKycProfileDto> GetKycProfileAsync(int ownerUserId);
        Task<OwnerKycProfileDto> SubmitKycAsync(int ownerUserId, SubmitKycDto dto);
        
        Task<List<OwnerKycProfileDto>> GetPendingKycsAsync();
        Task<ChargeSlot.Api.DTOs.PagedResultDto<OwnerKycProfileDto>> GetPendingKycsPagedAsync(int page, int pageSize);
        Task<List<OwnerKycProfileDto>> GetAllKycsAsync(string? status = null);
        Task<ChargeSlot.Api.DTOs.PagedResultDto<OwnerKycProfileDto>> GetAllKycsPagedAsync(string? status, int page, int pageSize);
        Task<OwnerKycProfileDto> ReviewKycAsync(int adminUserId, int targetOwnerUserId, ReviewKycDto dto);
    }
}
