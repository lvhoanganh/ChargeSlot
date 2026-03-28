using ChargeSlot.Api.Enums;
using ChargeSlot.Api.Helpers;
using ChargeSlot.Api.Models;
using ChargeSlot.Api.Models.Identity;
using ChargeSlot.Api.Repositories.Interfaces;
using ChargeSlot.Api.Services.Interfaces;
using Microsoft.AspNetCore.Identity;

namespace ChargeSlot.Api.Services.Implementation
{
    public class OtpService : IOtpService
    {
        private readonly IUserOtpRepository _otpRepository;
        private readonly UserManager<ApplicationUser> _userManager;
        public OtpService(IUserOtpRepository otpRepository, UserManager<ApplicationUser> userManager)
        {
            _otpRepository = otpRepository;
            _userManager = userManager;
        }

        public async Task SendOtpAsync(string phoneNumber, OtpPurpose purpose)

        {

            var phone = PhoneNumberHelper.NormalizeAndValidate(phoneNumber);
            var existing = await _userManager.FindByNameAsync(phone);
            if (existing == null)
                throw new InvalidOperationException("Phone number not exist.");
            // ⏱️ CHẶN SPAM OTP (30s)
            var remainingSeconds =
                await _otpRepository.GetRemainingCooldownSecondsAsync(
                    phone,
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
                PhoneNumber = phone,
                OtpHash = otpHash,
                Purpose = purpose,
                ExpiredAt = DateTimeHelper.VietnamNow().AddMinutes(5),
                IsUsed = false,
                CreatedAt = DateTimeHelper.VietnamNow()
            };

            // 4️⃣ Lưu DB
            await _otpRepository.AddAsync(entity);
            await _otpRepository.SaveChangesAsync();

            // 5️⃣ MOCK GỬI OTP (BÂY GIỜ)
            Console.WriteLine($"[OTP MOCK] Phone: {phone} | OTP: {otp}");
        }
        public async Task SendOtpRegister(string phoneNumber, OtpPurpose purpose)

        {
            var phone = PhoneNumberHelper.NormalizeAndValidate(phoneNumber);
            var existing = await _userManager.FindByNameAsync(phone);
            if (existing != null)
                throw new InvalidOperationException("Phone number already registered.");
            

            // ⏱️ CHẶN SPAM OTP (30s)
            var remainingSeconds =
                await _otpRepository.GetRemainingCooldownSecondsAsync(
                    phone,
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
                PhoneNumber = phone,
                OtpHash = otpHash,
                Purpose = purpose,
                ExpiredAt = DateTimeHelper.VietnamNow().AddMinutes(5),
                IsUsed = false,
                CreatedAt = DateTimeHelper.VietnamNow()
            };

            // 4️⃣ Lưu DB
            await _otpRepository.AddAsync(entity);
            await _otpRepository.SaveChangesAsync();

            // 5️⃣ MOCK GỬI OTP (BÂY GIỜ)
            Console.WriteLine($"[OTP MOCK] Phone: {phone} | OTP: {otp}");
        }
        public async Task VerifyOtpAsync(string phoneNumber, string otp, OtpPurpose purpose)
        {
            var phone = PhoneNumberHelper.NormalizeAndValidate(phoneNumber);

            // 1️⃣ Lấy OTP còn hiệu lực
            var record = await _otpRepository.GetLatestValidOtpAsync(phone, purpose);

            if (record == null)
                throw new InvalidOperationException("OTP is invalid or expired.");

            // 2️⃣ So sánh OTP
            if (!VerifyOtp(otp, record.OtpHash))
                throw new InvalidOperationException("OTP is incorrect.");

            // 3️⃣ Đánh dấu đã dùng
            record.VerifiedAt = DateTimeHelper.VietnamNow();   // 🔥 QUAN TRỌNG
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

        private static bool VerifyOtp(string otp, string hash) =>
            BCrypt.Net.BCrypt.Verify(otp, hash);
    }
}
