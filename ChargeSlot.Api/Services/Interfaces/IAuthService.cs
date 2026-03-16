using ChargeSlot.Api.DTOs.Auth;

namespace ChargeSlot.Api.Services.Interfaces
{
    public interface IAuthService
    {
        Task RegisterAsync(RegisterDto dto);
        Task<AuthResponseDto> LoginAsync(LoginDto dto);
        Task<AuthResponseDto> RefreshTokenAsync(string refreshToken);
        Task RevokeTokenAsync(string refreshToken, int userId);
        Task ResetPasswordAsync(string phoneNumber, string newPassword);
    }
}
