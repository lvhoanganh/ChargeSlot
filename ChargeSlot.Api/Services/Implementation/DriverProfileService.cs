using ChargeSlot.Api.DTOs.Profile;
using ChargeSlot.Api.Models;
using ChargeSlot.Api.Models.Identity;
using ChargeSlot.Api.Repositories.Interfaces;
using ChargeSlot.Api.Services.Interfaces;
using Microsoft.AspNetCore.Identity;

using ChargeSlot.Api.Helpers;
namespace ChargeSlot.Api.Services.Implementation
{
    public class DriverProfileService : IDriverProfileService
    {
        private readonly IDriverRepository _driverRepository;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IFileStorageService _fileStorageService;

        public DriverProfileService(
            IDriverRepository driverRepository,
            UserManager<ApplicationUser> userManager,
            IFileStorageService fileStorageService)
        {
            _driverRepository = driverRepository;
            _userManager = userManager;
            _fileStorageService = fileStorageService;
        }

        public async Task<DriverProfileDto?> GetByUserIdAsync(int userId)
        {
            var driver = await _driverRepository.GetByUserIdAsync(userId, tracking: false);
            if (driver == null) return null;

            return new DriverProfileDto
            {
                VehicleType = driver.VehicleType,
                LicensePlate = driver.LicensePlate,
                LicenseNumber = driver.LicenseNumber
            };
        }

        public async Task UpsertForUserAsync(int userId, DriverProfileDto dto)
        {
            var driver = await _driverRepository.GetByUserIdAsync(userId, tracking: true);

            if (driver == null)
            {
                driver = new Driver
                {
                    UserId = userId,
                    VehicleType = dto.VehicleType,
                    LicensePlate = dto.LicensePlate,
                    LicenseNumber = dto.LicenseNumber,
                    CreatedAt = DateTimeHelper.VietnamNow()
                };

                await _driverRepository.AddAsync(driver);
            }
            else
            {
                driver.VehicleType = dto.VehicleType;
                driver.LicensePlate = dto.LicensePlate;
                driver.LicenseNumber = dto.LicenseNumber;
            }

            await _driverRepository.SaveChangesAsync();
        }

        public async Task DeleteForUserAsync(int userId)
        {
            var driver = await _driverRepository.GetByUserIdAsync(userId, tracking: true);
            if (driver == null) return;

            _driverRepository.Remove(driver);
            await _driverRepository.SaveChangesAsync();
        }

        public async Task<string> UploadAvatarAsync(int userId, IFormFile file)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString())
                ?? throw new InvalidOperationException("User không tồn tại.");

            // Delete old avatar on Firebase (bỏ qua URL cũ từ wwwroot tự động)
            if (!string.IsNullOrEmpty(user.AvatarUrl))
            {
                await _fileStorageService.DeleteAsync(user.AvatarUrl);
            }

            // Upload lên Firebase Storage
            var avatarUrl = await _fileStorageService.UploadAsync(file, $"avatars/{userId}");
            user.AvatarUrl = avatarUrl;
            await _userManager.UpdateAsync(user);

            return avatarUrl;
        }
    }
}
