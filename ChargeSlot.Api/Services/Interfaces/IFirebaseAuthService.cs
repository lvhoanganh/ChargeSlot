using ChargeSlot.Api.DTOs.Auth;

namespace ChargeSlot.Api.Services.Interfaces
{
    public interface IFirebaseAuthService
    {
        Task<AuthResponseDto> LoginWithFirebaseAsync(string firebaseIdToken, string? role, string? fullName = null);
    }
}
