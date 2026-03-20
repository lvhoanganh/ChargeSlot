using ChargeSlot.Api.DTOs.Dispute;

namespace ChargeSlot.Api.Services.Interfaces
{
    public interface IDisputeService
    {
        /// <summary>Driver submits dispute + evidence.</summary>
        Task<DisputeDto> SubmitDisputeAsync(int driverUserId, CreateDisputeDto dto);

        /// <summary>Owner submits response + evidence.</summary>
        Task<DisputeDto> SubmitOwnerEvidenceAsync(int ownerUserId, int disputeId, OwnerEvidenceDto dto);

        /// <summary>Admin resolves dispute (refund or payout).</summary>
        Task<DisputeDto> ResolveDisputeAsync(int adminUserId, int disputeId, ResolveDisputeDto dto);

        /// <summary>Get dispute by ID.</summary>
        Task<DisputeDto?> GetByIdAsync(int disputeId);

        /// <summary>Get dispute by booking ID.</summary>
        Task<DisputeDto?> GetByBookingIdAsync(int bookingId);

        /// <summary>Get all pending disputes for admin.</summary>
        Task<List<DisputeDto>> GetPendingAsync();
    }
}
