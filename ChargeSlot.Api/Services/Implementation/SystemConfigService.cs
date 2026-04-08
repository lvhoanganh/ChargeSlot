using ChargeSlot.Api.Constants;
using ChargeSlot.Api.Data;
using ChargeSlot.Api.DTOs.Admin;
using ChargeSlot.Api.Models.Identity;
using ChargeSlot.Api.Services.Interfaces;
using ChargeSlot.Api.Repositories.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace ChargeSlot.Api.Services.Implementation
{
    public class SystemConfigService : ISystemConfigService
    {
        private readonly ISystemConfigRepository _configRepo;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMemoryCache _cache;
        private readonly UserManager<ApplicationUser> _userManager;

        private const string CacheKeyPrefix = "SystemConfig_";
        private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(15);

        public SystemConfigService(
            ISystemConfigRepository configRepo,
            IUnitOfWork unitOfWork,
            IMemoryCache cache,
            UserManager<ApplicationUser> userManager)
        {
            _configRepo = configRepo;
            _unitOfWork = unitOfWork;
            _cache = cache;
            _userManager = userManager;
        }

        public async Task<int> GetIntAsync(string key, int defaultValue)
        {
            var valueStr = await GetStringValueAsync(key);
            if (string.IsNullOrEmpty(valueStr)) return defaultValue;
            
            return int.TryParse(valueStr, out int result) ? result : defaultValue;
        }

        public async Task<decimal> GetDecimalAsync(string key, decimal defaultValue)
        {
            var valueStr = await GetStringValueAsync(key);
            if (string.IsNullOrEmpty(valueStr)) return defaultValue;

            return decimal.TryParse(valueStr, out decimal result) ? result : defaultValue;
        }

        private async Task<string?> GetStringValueAsync(string key)
        {
            var cacheKey = CacheKeyPrefix + key;

            // Look up in cache
            if (_cache.TryGetValue(cacheKey, out string? cachedValue))
            {
                return cachedValue;
            }

            // Fallback to DB
            var config = await _configRepo.GetByKeyAsync(key);
            var value = config?.Value;

            // Save to cache
            var cacheOptions = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(_cacheDuration);
            _cache.Set(cacheKey, value, cacheOptions);

            return value;
        }

        public async Task<UpdateSystemConfigsDto> GetCurrentConfigsAsync()
        {
            return new UpdateSystemConfigsDto
            {
                RefundPolicy100_Hrs = await GetIntAsync(SystemConfigKeys.RefundPolicy100_Hrs, 2),
                RefundPolicy50_Hrs = await GetIntAsync(SystemConfigKeys.RefundPolicy50_Hrs, 1),
                Payment_Expiry_Minutes = await GetIntAsync(SystemConfigKeys.Payment_Expiry_Minutes, 30),
                CheckIn_Window_Minutes = await GetIntAsync(SystemConfigKeys.CheckIn_Window_Minutes, 15),
                NoShow_Grace_Minutes = await GetIntAsync(SystemConfigKeys.NoShow_Grace_Minutes, 30),
                Slot_Buffer_Minutes = await GetIntAsync(SystemConfigKeys.Slot_Buffer_Minutes, 15),
                
                VAT_Rate = await GetDecimalAsync(SystemConfigKeys.VAT_Rate, 0.08m),
                Platform_Fee_Rate = await GetDecimalAsync(SystemConfigKeys.Platform_Fee_Rate, 0.05m),
                Loyalty_Earn_Rate = await GetDecimalAsync(SystemConfigKeys.Loyalty_Earn_Rate, 0.05m),
                
                Dispute_Limit_Per_Month = await GetIntAsync(SystemConfigKeys.Dispute_Limit_Per_Month, 3),
                Dispute_OwnerEvidence_Hours = await GetIntAsync(SystemConfigKeys.Dispute_OwnerEvidence_Hours, 24),
                Dispute_AdminReview_Hours = await GetIntAsync(SystemConfigKeys.Dispute_AdminReview_Hours, 48),
                
                Ban_Duration_Days_Permanent = await GetIntAsync(SystemConfigKeys.Ban_Duration_Days_Permanent, 36500),
                Ban_Duration_Days_FirstOffense = await GetIntAsync(SystemConfigKeys.Ban_Duration_Days_FirstOffense, 30),
                
                OTP_Expiry_Minutes = await GetIntAsync(SystemConfigKeys.OTP_Expiry_Minutes, 5),
                
                SecondaryPassword = "" // Khong bao gio tra ve password ra ngoai!
            };
        }

        public async Task UpdateConfigsAsync(UpdateSystemConfigsDto dto, int adminUserId)
        {
            var adminUser = await _userManager.FindByIdAsync(adminUserId.ToString())
                ?? throw new InvalidOperationException("Không tìm thấy admin.");

            // 1. Verify Secondary Password
            if (string.IsNullOrEmpty(adminUser.SecondaryPasswordHash))
                throw new InvalidOperationException("Bạn chưa thiết lập Mật Khẩu Cấp 2. Vui lòng thiết lập trước khi cấu hình hệ thống.");

            var isSecondaryValid = _userManager.PasswordHasher.VerifyHashedPassword(
                adminUser, adminUser.SecondaryPasswordHash, dto.SecondaryPassword);

            if (isSecondaryValid != PasswordVerificationResult.Success)
                throw new InvalidOperationException("Mật khẩu cấp 2 không chính xác.");

            // 2. Perform Updates
            await UpdateSingleConfigAsync(SystemConfigKeys.RefundPolicy100_Hrs, dto.RefundPolicy100_Hrs.ToString());
            await UpdateSingleConfigAsync(SystemConfigKeys.RefundPolicy50_Hrs, dto.RefundPolicy50_Hrs.ToString());
            await UpdateSingleConfigAsync(SystemConfigKeys.Payment_Expiry_Minutes, dto.Payment_Expiry_Minutes.ToString());
            await UpdateSingleConfigAsync(SystemConfigKeys.CheckIn_Window_Minutes, dto.CheckIn_Window_Minutes.ToString());
            await UpdateSingleConfigAsync(SystemConfigKeys.NoShow_Grace_Minutes, dto.NoShow_Grace_Minutes.ToString());
            await UpdateSingleConfigAsync(SystemConfigKeys.Slot_Buffer_Minutes, dto.Slot_Buffer_Minutes.ToString());

            await UpdateSingleConfigAsync(SystemConfigKeys.VAT_Rate, dto.VAT_Rate.ToString(System.Globalization.CultureInfo.InvariantCulture));
            await UpdateSingleConfigAsync(SystemConfigKeys.Platform_Fee_Rate, dto.Platform_Fee_Rate.ToString(System.Globalization.CultureInfo.InvariantCulture));
            await UpdateSingleConfigAsync(SystemConfigKeys.Loyalty_Earn_Rate, dto.Loyalty_Earn_Rate.ToString(System.Globalization.CultureInfo.InvariantCulture));

            await UpdateSingleConfigAsync(SystemConfigKeys.Dispute_Limit_Per_Month, dto.Dispute_Limit_Per_Month.ToString());
            await UpdateSingleConfigAsync(SystemConfigKeys.Dispute_OwnerEvidence_Hours, dto.Dispute_OwnerEvidence_Hours.ToString());
            await UpdateSingleConfigAsync(SystemConfigKeys.Dispute_AdminReview_Hours, dto.Dispute_AdminReview_Hours.ToString());

            await UpdateSingleConfigAsync(SystemConfigKeys.Ban_Duration_Days_Permanent, dto.Ban_Duration_Days_Permanent.ToString());
            await UpdateSingleConfigAsync(SystemConfigKeys.Ban_Duration_Days_FirstOffense, dto.Ban_Duration_Days_FirstOffense.ToString());

            await UpdateSingleConfigAsync(SystemConfigKeys.OTP_Expiry_Minutes, dto.OTP_Expiry_Minutes.ToString());

            // 3. Save to DB
            await _unitOfWork.CompleteAsync();
        }

        private async Task UpdateSingleConfigAsync(string key, string value)
        {
            var config = await _configRepo.GetByKeyAsync(key);
            if (config != null)
            {
                config.Value = value;
                config.UpdatedAt = ChargeSlot.Api.Helpers.DateTimeHelper.VietnamNow();
                _configRepo.Update(config);
            }
            else
            {
                config = new ChargeSlot.Api.Models.SystemConfig
                {
                    Key = key,
                    Value = value,
                    Description = $"Auto-generated by SystemConfigService",
                    UpdatedAt = ChargeSlot.Api.Helpers.DateTimeHelper.VietnamNow()
                };
                _configRepo.Add(config);
            }

            // Invalidate Cache for this key
            _cache.Remove(CacheKeyPrefix + key);
        }

        public async Task SeedDefaultConfigsAsync()
        {
            var defaults = await GetCurrentConfigsAsync();
            // Just running GetCurrentConfigsAsync will seed the cache.
            // If we want to physically ensure they are in DB:
            // Since our Get operations use Defaults without inserting them,
            // we should explicitly invoke UpdateSingleConfigAsync if missing.
            
            var allKeys = new[]
            {
                SystemConfigKeys.RefundPolicy100_Hrs, SystemConfigKeys.RefundPolicy50_Hrs,
                SystemConfigKeys.Payment_Expiry_Minutes, SystemConfigKeys.CheckIn_Window_Minutes,
                SystemConfigKeys.NoShow_Grace_Minutes, SystemConfigKeys.Slot_Buffer_Minutes,
                SystemConfigKeys.VAT_Rate, SystemConfigKeys.Platform_Fee_Rate, SystemConfigKeys.Loyalty_Earn_Rate,
                SystemConfigKeys.Dispute_Limit_Per_Month, SystemConfigKeys.Dispute_OwnerEvidence_Hours,
                SystemConfigKeys.Dispute_AdminReview_Hours, SystemConfigKeys.Ban_Duration_Days_Permanent,
                SystemConfigKeys.Ban_Duration_Days_FirstOffense, SystemConfigKeys.OTP_Expiry_Minutes
            };

            var existingDbKeys = await _configRepo.GetAllKeysAsync();
            
            var missingKeys = allKeys.Except(existingDbKeys).ToList();
            if(!missingKeys.Any()) return; // Already seeded

            // Get default values using the properties we just wrote logic for
            // and explicitly insert them into DB.
            if(missingKeys.Contains(SystemConfigKeys.RefundPolicy100_Hrs)) await SeedKeyAsync(SystemConfigKeys.RefundPolicy100_Hrs, "2");
            if(missingKeys.Contains(SystemConfigKeys.RefundPolicy50_Hrs)) await SeedKeyAsync(SystemConfigKeys.RefundPolicy50_Hrs, "1");
            if(missingKeys.Contains(SystemConfigKeys.Payment_Expiry_Minutes)) await SeedKeyAsync(SystemConfigKeys.Payment_Expiry_Minutes, "30");
            if(missingKeys.Contains(SystemConfigKeys.CheckIn_Window_Minutes)) await SeedKeyAsync(SystemConfigKeys.CheckIn_Window_Minutes, "15");
            if(missingKeys.Contains(SystemConfigKeys.NoShow_Grace_Minutes)) await SeedKeyAsync(SystemConfigKeys.NoShow_Grace_Minutes, "30");
            if(missingKeys.Contains(SystemConfigKeys.Slot_Buffer_Minutes)) await SeedKeyAsync(SystemConfigKeys.Slot_Buffer_Minutes, "15");
            
            if(missingKeys.Contains(SystemConfigKeys.VAT_Rate)) await SeedKeyAsync(SystemConfigKeys.VAT_Rate, "0.08");
            if(missingKeys.Contains(SystemConfigKeys.Platform_Fee_Rate)) await SeedKeyAsync(SystemConfigKeys.Platform_Fee_Rate, "0.05");
            if(missingKeys.Contains(SystemConfigKeys.Loyalty_Earn_Rate)) await SeedKeyAsync(SystemConfigKeys.Loyalty_Earn_Rate, "0.05");
            
            if(missingKeys.Contains(SystemConfigKeys.Dispute_Limit_Per_Month)) await SeedKeyAsync(SystemConfigKeys.Dispute_Limit_Per_Month, "3");
            if(missingKeys.Contains(SystemConfigKeys.Dispute_OwnerEvidence_Hours)) await SeedKeyAsync(SystemConfigKeys.Dispute_OwnerEvidence_Hours, "24");
            if(missingKeys.Contains(SystemConfigKeys.Dispute_AdminReview_Hours)) await SeedKeyAsync(SystemConfigKeys.Dispute_AdminReview_Hours, "48");
            
            if(missingKeys.Contains(SystemConfigKeys.Ban_Duration_Days_Permanent)) await SeedKeyAsync(SystemConfigKeys.Ban_Duration_Days_Permanent, "36500");
            if(missingKeys.Contains(SystemConfigKeys.Ban_Duration_Days_FirstOffense)) await SeedKeyAsync(SystemConfigKeys.Ban_Duration_Days_FirstOffense, "30");
            
            if(missingKeys.Contains(SystemConfigKeys.OTP_Expiry_Minutes)) await SeedKeyAsync(SystemConfigKeys.OTP_Expiry_Minutes, "5");

            await _unitOfWork.CompleteAsync();
        }

        private async Task SeedKeyAsync(string key, string defaultValue)
        {
            _configRepo.Add(new ChargeSlot.Api.Models.SystemConfig
            {
                Key = key,
                Value = defaultValue,
                Description = "System Default Config",
                UpdatedAt = ChargeSlot.Api.Helpers.DateTimeHelper.VietnamNow()
            });
            await Task.CompletedTask;
        }
    }
}
