using ChargeSlot.Api.Enums;
using ChargeSlot.Api.Models;
using ChargeSlot.Api.Repositories.Interfaces;
using ChargeSlot.Api.Services.Interfaces;

namespace ChargeSlot.Api.Services.Implementation
{
    public class OtpService : IOtpService
    {
        private readonly IUserOtpRepository _otpRepository;

        public OtpService(IUserOtpRepository otpRepository)
        {
            _otpRepository = otpRepository;
        }

        public async Task SendOtpAsync(string phoneNumber, OtpPurpose purpose)

        {
            // ⏱️ CHẶN SPAM OTP (30s)
            var remainingSeconds =
                await _otpRepository.GetRemainingCooldownSecondsAsync(
                    phoneNumber,
                    TimeSpan.FromSeconds(30)
                );

            if (remainingSeconds > 0)
                throw new InvalidOperationException(
                    $"Please wait {remainingSeconds} seconds before requesting another OTP."
                );

            // 1️⃣ Sinh OTP (6 số)
            var otp = GenerateOtp();

            // 2️⃣ Hash OTP (KHÔNG lưu plain text)
            var otpHash = HashOtp(otp);

            // 3️⃣ Tạo entity
            var entity = new UserOtp
            {
                Id = Guid.NewGuid(),
                PhoneNumber = phoneNumber,
                OtpHash = otpHash,
                Purpose = purpose,
                ExpiredAt = DateTime.UtcNow.AddMinutes(5),
                IsUsed = false,
                CreatedAt = DateTime.UtcNow
            };

            // 4️⃣ Lưu DB
            await _otpRepository.AddAsync(entity);
            await _otpRepository.SaveChangesAsync();

            // 5️⃣ MOCK GỬI OTP (BÂY GIỜ)
            Console.WriteLine($"[OTP MOCK] Phone: {phoneNumber} | OTP: {otp}");
        }

        public async Task VerifyOtpAsync(string phoneNumber, string otp, OtpPurpose purpose)
        {
            // 1️⃣ Lấy OTP còn hiệu lực
            var record = await _otpRepository.GetLatestValidOtpAsync(phoneNumber);

            if (record == null || record.Purpose != purpose)
                throw new InvalidOperationException("OTP is invalid or expired.");

            if (record == null)
                throw new InvalidOperationException("OTP is invalid or expired.");

            // 2️⃣ So sánh OTP
            if (!VerifyOtp(otp, record.OtpHash))
                throw new InvalidOperationException("OTP is incorrect.");

            // 3️⃣ Đánh dấu đã dùng
            record.VerifiedAt = DateTime.UtcNow;   // 🔥 QUAN TRỌNG
            record.IsUsed = true;

            await _otpRepository.SaveChangesAsync();

        }

        // =========================
        // PRIVATE METHODS
        // =========================

        private static string GenerateOtp()
        {
            return Random.Shared.Next(100000, 999999).ToString();
        }

        private static string HashOtp(string otp)
        {
            return BCrypt.Net.BCrypt.HashPassword(otp);
        }

        private static bool VerifyOtp(string otp, string hash)
        {
            return BCrypt.Net.BCrypt.Verify(otp, hash);
        }
    }
}
