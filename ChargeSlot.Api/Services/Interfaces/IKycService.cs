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
        Task<OwnerKycProfileDto> ReviewKycAsync(int adminUserId, int targetOwnerUserId, ReviewKycDto dto);
    }
}
