using ChargeSlot.Api.DTOs.Profile;
using ChargeSlot.Api.Models;
using ChargeSlot.Api.Models.Identity;
using ChargeSlot.Api.Repositories.Interfaces;
using ChargeSlot.Api.Services.Interfaces;
using Microsoft.AspNetCore.Identity;

using ChargeSlot.Api.Helpers;
namespace ChargeSlot.Api.Services.Implementation
{
    public class OwnerProfileService : IOwnerProfileService
    {
        private readonly IOwnerRepository _ownerRepository;
        private readonly UserManager<ApplicationUser> _userManager;

        public OwnerProfileService(IOwnerRepository ownerRepository, UserManager<ApplicationUser> userManager)
        {
            _ownerRepository = ownerRepository;
            _userManager = userManager;
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
                    CreatedAt = DateTimeHelper.VietnamNow()
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


