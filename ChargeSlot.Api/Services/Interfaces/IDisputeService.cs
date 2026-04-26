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
        Task<DisputeDto?> GetByIdAsync(int disputeId, int currentUserId, string currentUserRole);

        /// <summary>Get dispute by booking ID.</summary>
        Task<DisputeDto?> GetByBookingIdAsync(int bookingId, int currentUserId, string currentUserRole);

        /// <summary>Get all pending disputes for admin.</summary>
        Task<List<DisputeDto>> GetPendingAsync();

        /// <summary>Get all disputes (admin) with optional status filter.</summary>
        Task<List<DisputeDto>> GetAllAsync(string? status = null);

        /// <summary>Get all disputes submitted by the current driver.</summary>
        Task<List<DisputeDto>> GetMyDisputesAsync(int driverUserId);

        /// <summary>Get all disputes related to the current owner's stations.</summary>
        Task<List<DisputeDto>> GetOwnerDisputesAsync(int ownerUserId);

        /// <summary>Get driver's dispute strike status for current month.</summary>
        Task<DisputeStrikeStatusDto> GetDriverStrikeStatusAsync(int driverUserId);

        /// <summary>Get station's dispute strike status for current month.</summary>
        Task<DisputeStrikeStatusDto> GetStationStrikeStatusAsync(int stationId, int ownerUserId);
    }
}
