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
        private readonly IFileStorageService _fileStorageService;
        private readonly IUnitOfWork _unitOfWork;

        public OwnerProfileService(
            IOwnerRepository ownerRepository,
            UserManager<ApplicationUser> userManager,
            IFileStorageService fileStorageService,
            IUnitOfWork unitOfWork)
        {
            _ownerRepository = ownerRepository;
            _userManager = userManager;
            _fileStorageService = fileStorageService;
            _unitOfWork = unitOfWork;
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

            await _unitOfWork.CompleteAsync();
        }

        public async Task DeleteForUserAsync(int userId)
        {
            var owner = await _ownerRepository.GetByUserIdAsync(userId, tracking: true);
            if (owner == null) return;

            _ownerRepository.Remove(owner);
            await _unitOfWork.CompleteAsync();
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

