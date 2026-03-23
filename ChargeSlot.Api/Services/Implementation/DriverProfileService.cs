using ChargeSlot.Api.DTOs.Profile;
using ChargeSlot.Api.Models;
using ChargeSlot.Api.Models.Identity;
using ChargeSlot.Api.Repositories.Interfaces;
using ChargeSlot.Api.Services.Interfaces;
using Microsoft.AspNetCore.Identity;

namespace ChargeSlot.Api.Services.Implementation
{
    public class DriverProfileService : IDriverProfileService
    {
        private readonly IDriverRepository _driverRepository;
        private readonly UserManager<ApplicationUser> _userManager;

        public DriverProfileService(IDriverRepository driverRepository, UserManager<ApplicationUser> userManager)
        {
            _driverRepository = driverRepository;
            _userManager = userManager;
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
                    CreatedAt = DateTime.UtcNow
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

            // Delete old avatar file if exists
            if (!string.IsNullOrEmpty(user.AvatarUrl))
            {
                var oldPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", user.AvatarUrl.TrimStart('/'));
                if (File.Exists(oldPath)) File.Delete(oldPath);
            }

            // Save new avatar
            var uploadDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "avatars", userId.ToString());
            Directory.CreateDirectory(uploadDir);

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            var fileName = $"{Guid.NewGuid():N}{ext}";
            var filePath = Path.Combine(uploadDir, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var avatarUrl = $"/uploads/avatars/{userId}/{fileName}";
            user.AvatarUrl = avatarUrl;
            await _userManager.UpdateAsync(user);

            return avatarUrl;
        }
    }
}

