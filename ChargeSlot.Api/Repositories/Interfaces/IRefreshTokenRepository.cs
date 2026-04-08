using ChargeSlot.Api.Models;

namespace ChargeSlot.Api.Repositories.Interfaces
{
    public interface IRefreshTokenRepository
    {
        Task<RefreshToken?> GetByTokenAsync(string token);
        Task<RefreshToken?> GetByTokenAndUserAsync(string token, int userId);
        void Add(RefreshToken refreshToken);
        Task RemoveAllByUserIdAsync(int userId);
    }
}
