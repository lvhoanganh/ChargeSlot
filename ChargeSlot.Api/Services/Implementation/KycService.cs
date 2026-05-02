using ChargeSlot.Api.Constants;
using ChargeSlot.Api.DTOs.Kyc;
using ChargeSlot.Api.Enums;
using ChargeSlot.Api.Helpers;
using ChargeSlot.Api.Models;
using ChargeSlot.Api.Models.Identity;
using ChargeSlot.Api.Repositories.Interfaces;
using ChargeSlot.Api.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace ChargeSlot.Api.Services.Implementation
{
    public class KycService : IKycService
    {
        private readonly IOwnerRepository _ownerRepo;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IFileStorageService _fileService;
        private readonly INotificationService _notificationService;
        private readonly IContractService _contractService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<KycService> _logger;

        public KycService(IOwnerRepository ownerRepo, IUnitOfWork unitOfWork, IFileStorageService fileService, INotificationService notificationService, IContractService contractService, UserManager<ApplicationUser> userManager, ILogger<KycService> logger)
        {
            _ownerRepo = ownerRepo;
            _unitOfWork = unitOfWork;
            _fileService = fileService;
            _notificationService = notificationService;
            _contractService = contractService;
            _userManager = userManager;
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
            var owner = await _ownerRepo.GetByUserIdAsync(ownerUserId, tracking: false)
                ?? throw new InvalidOperationException("Hồ sơ chủ trạm chưa được tạo.");
            return MapToDto(owner);
        }

        public async Task<OwnerKycProfileDto> SubmitKycAsync(int ownerUserId, SubmitKycDto dto)
        {
            var owner = await _ownerRepo.GetByUserIdAsync(ownerUserId, tracking: true)
                ?? throw new InvalidOperationException("Hồ sơ chủ trạm chưa được tạo.");

            if (owner.KycStatus == KycStatus.Pending || owner.KycStatus == KycStatus.PendingUpdate)
                throw new InvalidOperationException("Hồ sơ đang chờ duyệt, không thể nộp lại.");

            // Validate uploads
            var fileExts = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            ValidateImage(dto.BusinessLicenseImage, fileExts);

            string folder = $"kyc/{ownerUserId}";

            // Nếu Owner đang Approved → giữ lại data cũ để rollback nếu bị reject
            bool isUpdate = owner.KycStatus == KycStatus.Approved;
            if (isUpdate)
            {
                // Lưu snapshot data cũ vào các trường Prev_*
                owner.PrevIdCardNumber = owner.IdCardNumber;
                owner.PrevIdCardDate = owner.IdCardDate;
                owner.PrevBusinessName = owner.BusinessName;
                owner.PrevBusinessLicenseNumber = owner.BusinessLicenseNumber;
                owner.PrevBusinessLicenseUrl = owner.BusinessLicenseUrl;
                owner.PrevTaxCode = owner.TaxCode;
                owner.PrevAddress = owner.Address;
            }

            // Upload ảnh mới
            owner.BusinessLicenseUrl = await _fileService.UploadAsync(dto.BusinessLicenseImage, folder);

            // Cập nhật thông tin mới
            owner.IdCardNumber = dto.IdCardNumber;
            owner.IdCardDate = dto.IdCardDate;
            owner.BusinessName = dto.BusinessName;
            owner.BusinessLicenseNumber = dto.BusinessLicenseNumber;
            owner.TaxCode = dto.TaxCode;
            owner.Address = dto.Address;

            // Set status
            owner.KycStatus = isUpdate ? KycStatus.PendingUpdate : KycStatus.Pending;
            owner.KycRejectReason = null;
            owner.KycSubmittedAt = DateTimeHelper.VietnamNow();

            _ownerRepo.Update(owner);
            await _unitOfWork.CompleteAsync();

            _logger.LogInformation("Owner {OwnerUserId} đã nộp hồ sơ KYC ({Type}).", ownerUserId, isUpdate ? "cập nhật" : "mới");

            // Gửi thông báo cho tất cả Admin
            var adminUsers = await _userManager.GetUsersInRoleAsync(RoleConstants.Admin);
            var notifyTitle = isUpdate ? "Hồ sơ KYC cập nhật chờ duyệt" : "Hồ sơ KYC mới chờ duyệt";
            var notifyContent = isUpdate
                ? $"Chủ trạm (ID: {ownerUserId}) đã nộp yêu cầu cập nhật hồ sơ KYC. Vui lòng kiểm duyệt."
                : $"Chủ trạm (ID: {ownerUserId}) đã nộp hồ sơ KYC mới. Vui lòng kiểm duyệt.";
            foreach (var admin in adminUsers)
                await _notificationService.SendAsync(
                    admin.Id,
                    notifyTitle,
                    notifyContent,
                    NotificationType.System);

            return MapToDto(owner);
        }

        public async Task<List<OwnerKycProfileDto>> GetPendingKycsAsync()
        {
            var owners = await _ownerRepo.GetPendingKycAsync();

            return owners.Select(MapToDto).ToList();
        }

        public async Task<ChargeSlot.Api.DTOs.PagedResultDto<OwnerKycProfileDto>> GetPendingKycsPagedAsync(int page, int pageSize)
        {
            var result = await _ownerRepo.GetPendingKycPagedAsync(page, pageSize);
            return new ChargeSlot.Api.DTOs.PagedResultDto<OwnerKycProfileDto>
            {
                Page = page,
                PageSize = pageSize,
                TotalItems = result.TotalCount,
                Items = result.Items.Select(MapToDto).ToList()
            };
        }

        public async Task<List<OwnerKycProfileDto>> GetAllKycsAsync(string? status = null)
        {
            var owners = await _ownerRepo.GetAllKycsAsync(status);

            return owners.Select(MapToDto).ToList();
        }

        public async Task<ChargeSlot.Api.DTOs.PagedResultDto<OwnerKycProfileDto>> GetAllKycsPagedAsync(string? status, int page, int pageSize)
        {
            var result = await _ownerRepo.GetAllKycsPagedAsync(status, page, pageSize);
            return new ChargeSlot.Api.DTOs.PagedResultDto<OwnerKycProfileDto>
            {
                Page = page,
                PageSize = pageSize,
                TotalItems = result.TotalCount,
                Items = result.Items.Select(MapToDto).ToList()
            };
        }

        public async Task<OwnerKycProfileDto> ReviewKycAsync(int adminUserId, int targetOwnerUserId, ReviewKycDto dto)
        {
            var owner = await _ownerRepo.GetByUserIdAsync(targetOwnerUserId, tracking: true)
                ?? throw new InvalidOperationException("Không tìm thấy hồ sơ chủ trạm.");

            if (owner.KycStatus != KycStatus.Pending && owner.KycStatus != KycStatus.PendingUpdate)
                throw new InvalidOperationException("Hồ sơ này không ở trạng thái chờ duyệt.");

            bool isUpdate = owner.KycStatus == KycStatus.PendingUpdate;

            if (dto.IsApproved)
            {
                // Duyệt → giữ data mới, xóa snapshot cũ
                owner.KycStatus = KycStatus.Approved;
                owner.KycRejectReason = null;
                ClearPrevSnapshot(owner);

                // Tạo hợp đồng tự động (idempotent — nếu đã có Pending/Signed thì cập nhật PII hoặc bỏ qua)
                // Lưu ý: CreateContractAsync KHÔNG gọi CompleteAsync bên trong
                await _contractService.CreateContractAsync(targetOwnerUserId);
            }
            else
            {
                if (isUpdate)
                {
                    // Từ chối bản update → khôi phục data cũ, giữ Approved
                    owner.IdCardNumber = owner.PrevIdCardNumber;
                    owner.IdCardDate = owner.PrevIdCardDate;
                    owner.BusinessName = owner.PrevBusinessName!;
                    owner.BusinessLicenseNumber = owner.PrevBusinessLicenseNumber;
                    owner.BusinessLicenseUrl = owner.PrevBusinessLicenseUrl;
                    owner.TaxCode = owner.PrevTaxCode!;
                    owner.Address = owner.PrevAddress;
                    ClearPrevSnapshot(owner);

                    owner.KycStatus = KycStatus.Approved; // Giữ quyền hoạt động
                }
                else
                {
                    // Từ chối lần đầu → Rejected bình thường
                    owner.KycStatus = KycStatus.Rejected;
                }
                owner.KycRejectReason = dto.RejectReason ?? "Không đủ điều kiện";
            }

            owner.KycReviewedAt = DateTimeHelper.VietnamNow();
            owner.KycReviewedByUserId = adminUserId;

            _ownerRepo.Update(owner);
            await _unitOfWork.CompleteAsync();

            // Gửi thông báo cho Chủ trạm
            string subject, content;
            if (dto.IsApproved)
            {
                subject = isUpdate ? "Hồ sơ cập nhật đã được duyệt" : "Hồ sơ của bạn đã được duyệt";
                content = isUpdate
                    ? "Thông tin KYC cập nhật của bạn đã được Admin phê duyệt."
                    : "Chúc mừng! Hồ sơ KYC đã được duyệt. Hợp đồng hợp tác đã được tạo — vui lòng đọc và ký hợp đồng để bắt đầu hoạt động.";
            }
            else
            {
                subject = isUpdate ? "Yêu cầu cập nhật KYC bị từ chối" : "Hồ sơ xác minh danh tính bị từ chối";
                content = isUpdate
                    ? $"Yêu cầu cập nhật KYC bị từ chối. Thông tin cũ đã được khôi phục. Lý do: {owner.KycRejectReason}"
                    : $"Vui lòng nộp lại hồ sơ. Lý do từ chối: {owner.KycRejectReason}";
            }

            await _notificationService.SendAsync(
                targetOwnerUserId,
                subject,
                content,
                NotificationType.System
            );

            return MapToDto(owner);
        }

        /// <summary>
        /// Xóa snapshot data cũ sau khi review xong
        /// </summary>
        private void ClearPrevSnapshot(Owner owner)
        {
            owner.PrevIdCardNumber = null;
            owner.PrevIdCardDate = null;
            owner.PrevBusinessName = null;
            owner.PrevBusinessLicenseNumber = null;
            owner.PrevBusinessLicenseUrl = null;
            owner.PrevTaxCode = null;
            owner.PrevAddress = null;
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
