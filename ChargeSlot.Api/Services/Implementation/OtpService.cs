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
        private readonly IUnitOfWork _unitOfWork;
        private readonly ISystemConfigService _systemConfig;
        
        public OtpService(IUserOtpRepository otpRepository, UserManager<ApplicationUser> userManager, IConfiguration config, IUnitOfWork unitOfWork, ISystemConfigService systemConfig)
        {
            _otpRepository = otpRepository;
            _userManager = userManager;
            _config = config;
            _unitOfWork = unitOfWork;
            _systemConfig = systemConfig;
        }

        public async Task SendOtpAsync(string phoneNumber, OtpPurpose purpose)

        {

            var phone = PhoneNumberHelper.NormalizeAndValidate(phoneNumber);
            var existing = await _userManager.FindByNameAsync(phone);
            if (existing == null)
                throw new InvalidOperationException("Số điện thoại không tồn tại.");
            var cooldownSeconds = await _systemConfig.GetIntAsync(Constants.SystemConfigKeys.OTP_Cooldown_Seconds, 30);
            var remainingSeconds =
                await _otpRepository.GetRemainingCooldownSecondsAsync(
                    phone,
                    TimeSpan.FromSeconds(cooldownSeconds)
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
            var otpExpiryMinutes = await _systemConfig.GetIntAsync(Constants.SystemConfigKeys.OTP_Expiry_Minutes, 5);
            var entity = new UserOtp
            {
                PhoneNumber = phone,
                OtpHash = otpHash,
                Purpose = purpose,
                ExpiredAt = DateTimeHelper.VietnamNow().AddMinutes(otpExpiryMinutes),
                IsUsed = false,
                CreatedAt = DateTimeHelper.VietnamNow()
            };

            // 4️⃣ Lưu DB
            await _otpRepository.AddAsync(entity);
            await _unitOfWork.CompleteAsync();

            // 5️⃣ GỬI OTP QUA SPEEDSMS (Hoặc Console Mock nếu chưa có API key)
            await SendSpeedSmsAsync(phone, otp);
        }
        public async Task SendOtpRegister(string phoneNumber, OtpPurpose purpose)

        {
            var phone = PhoneNumberHelper.NormalizeAndValidate(phoneNumber);
            var existing = await _userManager.FindByNameAsync(phone);
            if (existing != null)
                throw new InvalidOperationException("Số điện thoại đã được đăng ký.");
            

            // ⏱️ CHẶN SPAM OTP
            var cooldownSeconds = await _systemConfig.GetIntAsync(Constants.SystemConfigKeys.OTP_Cooldown_Seconds, 30);
            var remainingSeconds =
                await _otpRepository.GetRemainingCooldownSecondsAsync(
                    phone,
                    TimeSpan.FromSeconds(cooldownSeconds)
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
            var otpExpiryMinutes = await _systemConfig.GetIntAsync(Constants.SystemConfigKeys.OTP_Expiry_Minutes, 5);
            var entity = new UserOtp
            {
                PhoneNumber = phone,
                OtpHash = otpHash,
                Purpose = purpose,
                ExpiredAt = DateTimeHelper.VietnamNow().AddMinutes(otpExpiryMinutes),
                IsUsed = false,
                CreatedAt = DateTimeHelper.VietnamNow()
            };

            // 4️⃣ Lưu DB
            await _otpRepository.AddAsync(entity);
            await _unitOfWork.CompleteAsync();

            // 5️⃣ GỬI OTP QUA SPEEDSMS (Hoặc Console Mock nếu chưa có API key)
            await SendSpeedSmsAsync(phone, otp);
        }
        public async Task VerifyOtpAsync(string phoneNumber, string otp, OtpPurpose purpose)
        {
            var phone = PhoneNumberHelper.NormalizeAndValidate(phoneNumber);

            // 1️⃣ Lấy OTP còn hiệu lực
            var record = await _otpRepository.GetLatestValidOtpAsync(phone, purpose);

            if (record == null)
                throw new InvalidOperationException("Mã OTP không hợp lệ hoặc đã hết hạn.");

            // 2️⃣ So sánh OTP
            if (!VerifyOtp(otp, record.OtpHash))
                throw new InvalidOperationException("Mã OTP không chính xác.");

            // 3️⃣ Đánh dấu đã dùng
            record.VerifiedAt = DateTimeHelper.VietnamNow();   // 🔥 QUAN TRỌNG
            record.IsUsed = true;

            await _unitOfWork.CompleteAsync();

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
                throw new InvalidOperationException($"Lỗi kết nối tới SpeedSMS: {errorContent}");
            }

            // SpeedSMS luôn trả về HTTP 200, kết quả thực sự nằm trong body JSON
            var result = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
            if (result.TryGetProperty("status", out var statusProp) && statusProp.GetString() == "error")
            {
                var code = result.TryGetProperty("code", out var codeProp) ? codeProp.GetInt32().ToString() : "Unknown";
                var message = result.TryGetProperty("message", out var msgProp) ? msgProp.GetString() : "Unknown error";
                throw new InvalidOperationException($"Lỗi từ SpeedSMS - Code: {code}, Message: {message}");
            }
        }
    }
}

