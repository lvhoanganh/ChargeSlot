using ChargeSlot.Api.Data;
using ChargeSlot.Api.Enums;
using ChargeSlot.Api.Models;
using ChargeSlot.Api.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

using ChargeSlot.Api.Helpers;
namespace ChargeSlot.Api.Repositories.Implementation
{
    public class UserOtpRepository : IUserOtpRepository
    {
        private readonly ChargeSlotDbContext _context;

        public UserOtpRepository(ChargeSlotDbContext context)
        {
            _context = context;
        }

        public async Task AddAsync(UserOtp otp)
        {
            await _context.UserOtps.AddAsync(otp);
        }

        public async Task<UserOtp?> GetLatestValidOtpAsync(string phoneNumber, OtpPurpose purpose)
        {
            return await _context.UserOtps
                .Where(x =>
                    x.PhoneNumber == phoneNumber &&
                    x.Purpose == purpose &&
                    !x.IsUsed &&
                    x.ExpiredAt > DateTimeHelper.VietnamNow())
                .OrderByDescending(x => x.CreatedAt)
                .FirstOrDefaultAsync();
        }
        public async Task<bool> HasVerifiedOtpAsync(string phoneNumber)
        {
            return await _context.UserOtps
                .Where(x =>
                    x.PhoneNumber == phoneNumber &&
                    x.IsUsed)
                .OrderByDescending(x => x.CreatedAt)
                .AnyAsync();
        }

        public async Task InvalidateAllOtpsAsync(string phoneNumber)
        {
            var otps = await _context.UserOtps
                .Where(x => x.PhoneNumber == phoneNumber && !x.IsUsed)
                .ToListAsync();

            foreach (var otp in otps)
            {
                otp.IsUsed = true;
            }
        }
        public async Task<bool> CanSendOtpAsync(string phoneNumber, TimeSpan cooldown)
        {
            var lastOtp = await _context.UserOtps
                .Where(x => x.PhoneNumber == phoneNumber)
                .OrderByDescending(x => x.CreatedAt)
                .FirstOrDefaultAsync();

            if (lastOtp == null)
                return true;

            return DateTimeHelper.VietnamNow() - lastOtp.CreatedAt >= cooldown;
        }
        public async Task<int> GetRemainingCooldownSecondsAsync(string phoneNumber, TimeSpan cooldown)
        {
            var lastOtp = await _context.UserOtps
                .Where(x => x.PhoneNumber == phoneNumber)
                .OrderByDescending(x => x.CreatedAt)
                .FirstOrDefaultAsync();

            if (lastOtp == null)
                return 0;

            var elapsed = DateTimeHelper.VietnamNow() - lastOtp.CreatedAt;
            var remaining = cooldown - elapsed;

            if (remaining <= TimeSpan.Zero)
                return 0;

            return (int)Math.Ceiling(remaining.TotalSeconds);
        }

        public async Task<bool> HasRecentlyVerifiedOtpAsync(
            string phoneNumber,
            OtpPurpose purpose,
            TimeSpan validWithin)
        {
            var now = DateTimeHelper.VietnamNow();

            return await _context.UserOtps
                .Where(x =>
                    x.PhoneNumber == phoneNumber &&
                    x.Purpose == purpose &&
                    x.VerifiedAt != null &&
                    x.VerifiedAt >= now - validWithin)
                .AnyAsync();
        }
    }
}

