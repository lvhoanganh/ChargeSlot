using ChargeSlot.Api.DTOs.Auth;

namespace ChargeSlot.Api.Services.Interfaces
{
    public interface IAuthService
    {
        Task RegisterAsync(RegisterDto dto);
        Task<AuthResponseDto> LoginAsync(LoginDto dto);
        Task ResetPasswordAsync(string phoneNumber, string newPassword);
    }
}
