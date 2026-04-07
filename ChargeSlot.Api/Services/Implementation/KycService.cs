using ChargeSlot.Api.Data;
using ChargeSlot.Api.DTOs.Kyc;
using ChargeSlot.Api.Enums;
using ChargeSlot.Api.Helpers;
using ChargeSlot.Api.Models;
using ChargeSlot.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ChargeSlot.Api.Services.Implementation
{
    public class KycService : IKycService
    {
        private readonly ChargeSlotDbContext _context;
        private readonly IFileStorageService _fileService;
        private readonly INotificationService _notificationService;
        private readonly ILogger<KycService> _logger;

        public KycService(ChargeSlotDbContext context, IFileStorageService fileService, INotificationService notificationService, ILogger<KycService> logger)
        {
            _context = context;
            _fileService = fileService;
            _notificationService = notificationService;
            _logger = logger;
        }

        private OwnerKycProfileDto MapToDto(Owner owner)
        {
            return new OwnerKycProfileDto
            {
                OwnerUserId = owner.UserId,
                BusinessName = owner.BusinessName,
                TaxCode = owner.TaxCode,
                IdCardNumber = owner.IdCardNumber,
                IdCardDate = owner.IdCardDate,
                FrontIdCardUrl = owner.FrontIdCardUrl,
                BackIdCardUrl = owner.BackIdCardUrl,
                BusinessLicenseNumber = owner.BusinessLicenseNumber,
                BusinessLicenseUrl = owner.BusinessLicenseUrl,
                Address = owner.Address,
                KycStatus = owner.KycStatus.ToString(),
                KycRejectReason = owner.KycRejectReason,
                KycSubmittedAt = owner.KycSubmittedAt,
                KycReviewedAt = owner.KycReviewedAt
            };
        }

        public async Task<OwnerKycProfileDto> GetKycProfileAsync(int ownerUserId)
        {
            var owner = await _context.Owner.FirstOrDefaultAsync(o => o.UserId == ownerUserId)
                ?? throw new InvalidOperationException("Hồ sơ chủ trạm chưa được tạo.");
            return MapToDto(owner);
        }

        public async Task<OwnerKycProfileDto> SubmitKycAsync(int ownerUserId, SubmitKycDto dto)
        {
            var owner = await _context.Owner.FirstOrDefaultAsync(o => o.UserId == ownerUserId)
                ?? throw new InvalidOperationException("Hồ sơ chủ trạm chưa được tạo.");

            if (owner.KycStatus == KycStatus.Pending)
                throw new InvalidOperationException("Hồ sơ đang chờ duyệt, không thể nộp lại.");
            if (owner.KycStatus == KycStatus.Approved)
                throw new InvalidOperationException("Hồ sơ đã được duyệt.");

            // Validate uploads
            var fileExts = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            ValidateImage(dto.FrontIdCardImage, fileExts);
            ValidateImage(dto.BackIdCardImage, fileExts);
            ValidateImage(dto.BusinessLicenseImage, fileExts);

            string folder = $"kyc/{ownerUserId}";

            // Upload
            owner.FrontIdCardUrl = await _fileService.UploadAsync(dto.FrontIdCardImage, folder);
            owner.BackIdCardUrl = await _fileService.UploadAsync(dto.BackIdCardImage, folder);
            owner.BusinessLicenseUrl = await _fileService.UploadAsync(dto.BusinessLicenseImage, folder);

            // Update info
            owner.IdCardNumber = dto.IdCardNumber;
            owner.IdCardDate = dto.IdCardDate;
            owner.BusinessName = dto.BusinessName;
            owner.BusinessLicenseNumber = dto.BusinessLicenseNumber;
            owner.TaxCode = dto.TaxCode;
            owner.Address = dto.Address;

            // Reset status
            owner.KycStatus = KycStatus.Pending;
            owner.KycRejectReason = null;
            owner.KycSubmittedAt = DateTimeHelper.VietnamNow();

            await _context.SaveChangesAsync();

            // Gửi thông báo đến toàn hệ thống (Admin check)
            _logger.LogInformation("Owner {OwnerUserId} đã nộp hồ sơ KYC.", ownerUserId);

            return MapToDto(owner);
        }

        public async Task<List<OwnerKycProfileDto>> GetPendingKycsAsync()
        {
            var owners = await _context.Owner
                .Where(o => o.KycStatus == KycStatus.Pending)
                .OrderBy(o => o.KycSubmittedAt)
                .ToListAsync();

            return owners.Select(MapToDto).ToList();
        }

        public async Task<OwnerKycProfileDto> ReviewKycAsync(int adminUserId, int targetOwnerUserId, ReviewKycDto dto)
        {
            var owner = await _context.Owner.FirstOrDefaultAsync(o => o.UserId == targetOwnerUserId)
                ?? throw new InvalidOperationException("Không tìm thấy hồ sơ chủ trạm.");

            if (owner.KycStatus != KycStatus.Pending)
                throw new InvalidOperationException("Hồ sơ này không ở trạng thái chờ duyệt.");

            owner.KycStatus = dto.IsApproved ? KycStatus.Approved : KycStatus.Rejected;
            owner.KycRejectReason = dto.IsApproved ? null : (dto.RejectReason ?? "Không đủ điều kiện");
            owner.KycReviewedAt = DateTimeHelper.VietnamNow();
            owner.KycReviewedByUserId = adminUserId;

            await _context.SaveChangesAsync();

            // Gửi thông báo cho Chủ trạm
            string subject = dto.IsApproved ? "Hồ sơ của bạn đã được duyệt" : "Hồ sơ xác minh danh tính bị từ chối";
            string content = dto.IsApproved 
                ? "Chúc mừng, giờ đây bạn có thể đăng ký Trạm Sạc mới!" 
                : $"Vui lòng nộp lại hồ sơ. Lý do từ chối: {owner.KycRejectReason}";

            await _notificationService.SendAsync(
                targetOwnerUserId,
                subject,
                content,
                NotificationType.System
            );

            return MapToDto(owner);
        }

        private void ValidateImage(IFormFile file, string[] allowedExtensions)
        {
            if (file == null || file.Length == 0)
                throw new InvalidOperationException("Vui lòng tải lên đầy đủ các file ảnh bắt buộc.");
            
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(ext))
                throw new InvalidOperationException("Chỉ chấp nhận file ảnh (jpg, png, webp).");

            if (file.Length > 5 * 1024 * 1024)
                throw new InvalidOperationException("Kích thước file ảnh không được vượt quá 5MB.");
        }
    }
}
