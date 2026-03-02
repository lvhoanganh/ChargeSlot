using ChargeSlot.Api.DTOs.Profile;
using ChargeSlot.Api.Models;
using ChargeSlot.Api.Repositories.Interfaces;
using ChargeSlot.Api.Services.Interfaces;

namespace ChargeSlot.Api.Services.Implementation
{
    public class OwnerProfileService : IOwnerProfileService
    {
        private readonly IOwnerRepository _ownerRepository;

        public OwnerProfileService(IOwnerRepository ownerRepository)
        {
            _ownerRepository = ownerRepository;
        }

        public async Task<OwnerProfileDto?> GetByUserIdAsync(int userId)
        {
            var owner = await _ownerRepository.GetByUserIdAsync(userId, tracking: false);
            if (owner == null) return null;

            return new OwnerProfileDto
            {
                BusinessName = owner.BusinessName,
                TaxCode = owner.TaxCode
            };
        }

        public async Task UpsertForUserAsync(int userId, OwnerProfileDto dto)
        {
            var owner = await _ownerRepository.GetByUserIdAsync(userId, tracking: true);

            if (owner == null)
            {
                owner = new Owner
                {
                    UserId = userId,
                    BusinessName = dto.BusinessName,
                    TaxCode = dto.TaxCode,
                    CreatedAt = DateTime.UtcNow
                };

                await _ownerRepository.AddAsync(owner);
            }
            else
            {
                owner.BusinessName = dto.BusinessName;
                owner.TaxCode = dto.TaxCode;
            }

            await _ownerRepository.SaveChangesAsync();
        }

        public async Task DeleteForUserAsync(int userId)
        {
            var owner = await _ownerRepository.GetByUserIdAsync(userId, tracking: true);
            if (owner == null) return;

            _ownerRepository.Remove(owner);
            await _ownerRepository.SaveChangesAsync();
        }
    }
}

