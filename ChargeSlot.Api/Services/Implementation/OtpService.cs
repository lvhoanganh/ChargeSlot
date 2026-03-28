using ChargeSlot.Api.Enums;
using ChargeSlot.Api.Helpers;
using ChargeSlot.Api.Models;
using ChargeSlot.Api.Models.Identity;
using ChargeSlot.Api.Repositories.Interfaces;
using ChargeSlot.Api.Services.Interfaces;
using Microsoft.AspNetCore.Identity;

using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;

namespace ChargeSlot.Api.Services.Implementation
{
    public class OtpService : IOtpService
    {
        private readonly IUserOtpRepository _otpRepository;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IConfiguration _config;
        
        public OtpService(IUserOtpRepository otpRepository, UserManager<ApplicationUser> userManager, IConfiguration config)
        {
            _otpRepository = otpRepository;
            _userManager = userManager;
            _config = config;
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

            // 5️⃣ GỬI OTP QUA SPEEDSMS (Hoặc Console Mock nếu chưa có API key)
            await SendSpeedSmsAsync(phone, otp);
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

            // 5️⃣ GỬI OTP QUA SPEEDSMS (Hoặc Console Mock nếu chưa có API key)
            await SendSpeedSmsAsync(phone, otp);
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

        private async Task SendSpeedSmsAsync(string phone, string otp)
        {
            var apiKey = _config["SpeedSms:ApiKey"];
            if (string.IsNullOrEmpty(apiKey))
            {
                // Fallback to console mock if no API key is provided
                Console.WriteLine($"[OTP MOCK] Phone: {phone} | OTP: {otp}");
                return;
            }

            // SpeedSMS uses Basic Auth with Access Token as username and 'x' as password
            using var client = new HttpClient();
            var authBytes = System.Text.Encoding.ASCII.GetBytes($"{apiKey}:x");
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(authBytes));

            var payload = new
            {
                to = phone,
                content = $"Ma xac thuc cua ban tren ChargeSlot la {otp}",
                sms_type = 2,
                sender = "" // Empty for default brandname (e.g., Verify)
            };

            var response = await client.PostAsJsonAsync("https://api.speedsms.vn/index.php/sms/send", payload);
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new InvalidOperationException($"Lỗi gửi SMS qua SpeedSMS: {errorContent}");
            }
        }
    }
}
