using ChargeSlot.Api.DTOs.Profile;
using ChargeSlot.Api.Models;
using ChargeSlot.Api.Repositories.Interfaces;
using ChargeSlot.Api.Services.Interfaces;

namespace ChargeSlot.Api.Services.Implementation
{
    public class DriverProfileService : IDriverProfileService
    {
        private readonly IDriverRepository _driverRepository;

        public DriverProfileService(IDriverRepository driverRepository)
        {
            _driverRepository = driverRepository;
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
    }
}

