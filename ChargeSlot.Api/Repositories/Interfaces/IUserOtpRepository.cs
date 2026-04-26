using ChargeSlot.Api.Enums;
using ChargeSlot.Api.Models;

namespace ChargeSlot.Api.Repositories.Interfaces
{
    public interface IUserOtpRepository
    {
        Task AddAsync(UserOtp otp);

        Task<UserOtp?> GetLatestValidOtpAsync(string phoneNumber, OtpPurpose purpose);
        Task<bool> HasVerifiedOtpAsync(string phoneNumber);
        Task InvalidateAllOtpsAsync(string phoneNumber);
        Task<bool> CanSendOtpAsync(string phoneNumber, TimeSpan cooldown);
        Task<int> GetRemainingCooldownSecondsAsync(string phoneNumber, TimeSpan cooldown);
        Task<bool> HasRecentlyVerifiedOtpAsync(
            string phoneNumber,
            OtpPurpose purpose,
            TimeSpan validWithin
        );
    }
}

