using ChargeSlot.Api.Constants;
using ChargeSlot.Api.DTOs.Slot;
using ChargeSlot.Api.DTOs.Station;
using ChargeSlot.Api.DTOs.Admin;
using ChargeSlot.Api.DTOs;
using ChargeSlot.Api.Enums;
using ChargeSlot.Api.Models;
using ChargeSlot.Api.Models.Identity;
using ChargeSlot.Api.Repositories.Interfaces;
using ChargeSlot.Api.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using ChargeSlot.Api.Helpers;

namespace ChargeSlot.Api.Services.Implementation
{
    public class ChargingStationService : IChargingStationService
    {
        private readonly IChargingStationRepository _stationRepo;
        private readonly IOwnerRepository _ownerRepo;
        private readonly IContractRepository _contractRepo;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IFileStorageService _fileStorageService;
        private readonly IStationPricingRepository _pricingRepo;
        private readonly IExtraServiceRepository _extraServiceRepo;
        private readonly IStationUnavailableDateRepository _unavailableDateRepo;
        private readonly IBookingRepository _bookingRepo;
        private readonly INotificationService _notificationService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IServiceProvider _serviceProvider;

        public ChargingStationService(
            IChargingStationRepository stationRepo,
            IOwnerRepository ownerRepo,
            IContractRepository contractRepo,
            UserManager<ApplicationUser> userManager,
            IFileStorageService fileStorageService,
            IStationPricingRepository pricingRepo,
            IExtraServiceRepository extraServiceRepo,
            IStationUnavailableDateRepository unavailableDateRepo,
            IBookingRepository bookingRepo,
            INotificationService notificationService,
            IUnitOfWork unitOfWork,
            IServiceProvider serviceProvider)
        {
            _stationRepo = stationRepo;
            _ownerRepo = ownerRepo;
            _contractRepo = contractRepo;
            _userManager = userManager;
            _fileStorageService = fileStorageService;
            _pricingRepo = pricingRepo;
            _extraServiceRepo = extraServiceRepo;
            _unavailableDateRepo = unavailableDateRepo;
            _bookingRepo = bookingRepo;
            _notificationService = notificationService;
            _unitOfWork = unitOfWork;
            _serviceProvider = serviceProvider;
        }

        // ─────────────── CRUD ───────────────

        public async Task<ChargingStationDto?> GetByIdAsync(int id, int ownerUserId)
        {
            var station = await _stationRepo.GetByIdAsync(id);
            if (station == null || station.OwnerUserId != ownerUserId)
                return null;

            return MapToDto(station);
        }

        public async Task<List<ChargingStationDto>> GetAllByOwnerAsync(int ownerUserId)
        {
            var stations = await _stationRepo.GetAllByOwnerAsync(ownerUserId);
            return stations.Select(MapToDto).ToList();
        }

        public async Task<PagedResultDto<ChargingStationDto>> GetAllByOwnerPagedAsync(int ownerUserId, int page, int pageSize)
        {
            var result = await _stationRepo.GetAllByOwnerPagedAsync(ownerUserId, page, pageSize);
            return new PagedResultDto<ChargingStationDto>
            {
                Page = page,
                PageSize = pageSize,
                TotalItems = result.TotalCount,
                Items = result.Items.Select(MapToDto).ToList()
            };
        }

        public async Task<ChargingStationDto> CreateAsync(int ownerUserId, CreateChargingStationDto dto)
        {
            var ownerExists = await _ownerRepo.GetByUserIdAsync(ownerUserId);
            if (ownerExists == null)
            {
                throw new InvalidOperationException("Chưa tìm thấy hồ sơ Chủ trạm. Vui lòng xác thực danh tính (KYC) trước khi tạo trạm sạc.");
            }

            if (ownerExists.KycStatus != KycStatus.Approved && ownerExists.KycStatus != KycStatus.PendingUpdate)
            {
                throw new InvalidOperationException("Hồ sơ doanh nghiệp chưa được duyệt. Vui lòng xác thực danh tính (KYC) và chờ Admin kiểm duyệt trước khi tạo trạm sạc.");
            }

            var contract = await _contractRepo.GetByOwnerAsync(ownerUserId);
            if (contract == null || contract.Status != ContractStatus.Signed)
            {
                throw new InvalidOperationException("Vốn pháp lý chưa đủ: Bạn cần Đọc và Ký hợp đồng hợp tác bằng chữ ký điện tử trước khi bắt đầu tạo hệ thống trạm sạc.");
            }
            var station = new ChargingStation
            {
                OwnerUserId = ownerUserId,
                Name = dto.Name,
                Address = dto.Address,
                Description = dto.Description,
                Latitude = dto.Latitude,
                Longitude = dto.Longitude,
                LayoutImageUrl = dto.LayoutImageUrl,
                LayoutWidth = dto.LayoutWidth,
                LayoutHeight = dto.LayoutHeight,
                ApprovalStatus = ApprovalStatus.Draft,
                OperationalStatus = OperationalStatus.Inactive,
                CreatedAt = DateTimeHelper.VietnamNow()
            };

            // Add operating hours
            if (dto.OperatingHours?.Count > 0)
            {
                foreach (var h in dto.OperatingHours)
                {
                    station.OperatingHours.Add(new StationOperatingHours
                    {
                        DayOfWeek = h.DayOfWeek,
                        IsClosed = h.IsClosed,
                        OpenTime = h.OpenTime,
                        CloseTime = h.CloseTime
                    });
                }
            }

            // Add images
            if (dto.ImageUrls?.Count > 0)
            {
                foreach (var url in dto.ImageUrls)
                {
                    station.Images.Add(new StationImage
                    {
                        ImageUrl = url,
                        CreatedAt = DateTimeHelper.VietnamNow()
                    });
                }
            }

            // Add slots
            if (dto.Slots?.Count > 0)
            {
                foreach (var s in dto.Slots)
                {
                    station.ChargingSlots.Add(new ChargingSlot
                    {
                        SlotName = s.SlotName,
                        PositionX = s.PositionX,
                        PositionY = s.PositionY,
                        Status = SlotStatus.Inactive,
                        CreatedAt = DateTimeHelper.VietnamNow()
                    });
                }
            }

            await _stationRepo.AddAsync(station);
            await _unitOfWork.CompleteAsync();

            return MapToDto(station);
        }

        /// <summary>
        /// Tạo trạm từ multipart/form-data: upload ảnh, tạo slots, operating hours, station-level pricing.
        /// </summary>
        public async Task<ChargingStationDto> CreateFromFormAsync(int ownerUserId, CreateStationFormDto dto)
        {
            // Ensure Owner profile record exists
            var ownerExists = await _ownerRepo.GetByUserIdAsync(ownerUserId);
            if (ownerExists == null)
            {
                var user = await _userManager.FindByIdAsync(ownerUserId.ToString())
                    ?? throw new InvalidOperationException("User not found.");
                ownerExists = new Owner
                {
                    UserId = ownerUserId,
                    BusinessName = user.FullName,
                    TaxCode = "N/A",
                    CreatedAt = DateTimeHelper.VietnamNow()
                };
                await _ownerRepo.AddAsync(ownerExists);
                await _unitOfWork.CompleteAsync();
            }

            if (ownerExists.KycStatus != KycStatus.Approved && ownerExists.KycStatus != KycStatus.PendingUpdate)
            {
                throw new InvalidOperationException("Hồ sơ doanh nghiệp chưa được duyệt. Vui lòng xác thực danh tính (KYC) và chờ Admin kiểm duyệt trước khi tạo trạm sạc.");
            }

            var contract = await _contractRepo.GetByOwnerAsync(ownerUserId);
            if (contract == null || contract.Status != ContractStatus.Signed)
            {
                throw new InvalidOperationException("Vốn pháp lý chưa đủ: Bạn cần Đọc và Ký hợp đồng hợp tác bằng chữ ký điện tử trước khi bắt đầu tạo hệ thống trạm sạc.");
            }

            var station = new ChargingStation
            {
                OwnerUserId = ownerUserId,
                Name = dto.Name,
                Address = dto.Address,
                Description = dto.Description,
                Latitude = dto.Latitude,
                Longitude = dto.Longitude,
                LayoutWidth = dto.LayoutWidth,
                LayoutHeight = dto.LayoutHeight,
                ApprovalStatus = ApprovalStatus.Draft,
                OperationalStatus = OperationalStatus.Inactive,
                CreatedAt = DateTimeHelper.VietnamNow()
            };

            // Operating Hours
            if (dto.OperatingHours?.Count > 0)
            {
                foreach (var h in dto.OperatingHours)
                {
                    station.OperatingHours.Add(new StationOperatingHours
                    {
                        DayOfWeek = (byte)h.DayOfWeek,
                        IsClosed = h.IsClosed,
                        OpenTime = !string.IsNullOrEmpty(h.OpenTime) ? TimeOnly.Parse(h.OpenTime) : null,
                        CloseTime = !string.IsNullOrEmpty(h.CloseTime) ? TimeOnly.Parse(h.CloseTime) : null
                    });
                }
            }

            // Slots
            if (dto.Slots?.Count > 0)
            {
                foreach (var s in dto.Slots)
                {
                    station.ChargingSlots.Add(new ChargingSlot
                    {
                        SlotName = s.SlotName,
                        PositionX = (decimal?)(s.PositionX ?? 0),
                        PositionY = (decimal?)(s.PositionY ?? 0),
                        Status = SlotStatus.Inactive,
                        CreatedAt = DateTimeHelper.VietnamNow()
                    });
                }
            }

            // Save station first to get ID (needed for image folder + slot IDs)
            await _stationRepo.AddAsync(station);
            await _unitOfWork.CompleteAsync();

            // Upload images to Firebase Storage
            if (dto.Images?.Length > 0)
            {
                foreach (var file in dto.Images)
                {
                    if (file.Length > 0)
                    {
                        var imageUrl = await _fileStorageService.UploadAsync(file, $"stations/{station.Id}");
                        station.Images.Add(new StationImage
                        {
                            StationId = station.Id,
                            ImageUrl = imageUrl,
                            CreatedAt = DateTimeHelper.VietnamNow()
                        });
                    }
                }
                await _unitOfWork.CompleteAsync();
            }

            // Station-level pricing
            if (dto.StationPricing?.Count > 0)
            {
                var now = DateTimeHelper.VietnamNow();
                foreach (var p in dto.StationPricing)
                {
                    if (!TimeOnly.TryParse(p.StartTime, out var startTime) ||
                        !TimeOnly.TryParse(p.EndTime, out var endTime))
                        continue;

                    _pricingRepo.Add(new StationPricing
                    {
                        StationId = station.Id,
                        StartTime = startTime,
                        EndTime = endTime,
                        PricePerHour = p.PricePerHour,
                        Priority = 1,
                        EffectiveFrom = now,
                        IsActive = true,
                        CreatedAt = now
                    });
                }
                await _unitOfWork.CompleteAsync();
            }

            // Reload to get full data
            var created = await _stationRepo.GetByIdAsync(station.Id) ?? station;
            return MapToDto(created);
        }

        public Task<ChargingStationDto> CreateFromFormAsync(int ownerUserId, CreateStationFormDto dto, HttpRequest request)
            => throw new NotImplementedException();

        public Task<ChargingStationDto> UpdateFromFormAsync(int id, int ownerUserId, UpdateStationFormDto dto, HttpRequest request)
            => throw new NotImplementedException();

        /// <summary>
        /// Cập nhật trạm từ multipart/form-data: upload ảnh mới, giữ ảnh cũ, thay đổi operating hours.
        /// Cho phép sửa ở MỌI trạng thái (không hạn chế Draft/Rejected).
        /// </summary>
        public async Task<ChargingStationDto> UpdateFromFormAsync(int id, int ownerUserId, UpdateStationFormDto dto)
        {
            var station = await _stationRepo.GetByIdAsync(id, tracking: true, includeDetails: true);
            if (station == null)
                throw new KeyNotFoundException($"Station {id} not found.");
            if (station.OwnerUserId != ownerUserId)
                throw new UnauthorizedAccessException("You do not own this station.");

            // Update basic info
            station.Name = dto.Name;
            station.Address = dto.Address;
            station.Description = dto.Description;
            station.Latitude = dto.Latitude;
            station.Longitude = dto.Longitude;
            if (dto.LayoutWidth.HasValue) station.LayoutWidth = dto.LayoutWidth.Value;
            if (dto.LayoutHeight.HasValue) station.LayoutHeight = dto.LayoutHeight.Value;
            station.UpdatedAt = DateTimeHelper.VietnamNow();

            // Replace operating hours (nếu có truyền)
            if (dto.OperatingHours != null)
            {
                _stationRepo.RemoveOperatingHours(station.OperatingHours);
                station.OperatingHours.Clear();

                foreach (var h in dto.OperatingHours)
                {
                    station.OperatingHours.Add(new StationOperatingHours
                    {
                        StationId = station.Id,
                        DayOfWeek = (byte)h.DayOfWeek,
                        IsClosed = h.IsClosed,
                        OpenTime = !string.IsNullOrEmpty(h.OpenTime) ? TimeOnly.Parse(h.OpenTime) : null,
                        CloseTime = !string.IsNullOrEmpty(h.CloseTime) ? TimeOnly.Parse(h.CloseTime) : null
                    });
                }
            }

            // Handle images: keep existing + add new uploads
            // 1. Xóa ảnh cũ không nằm trong danh sách giữ lại
            var keepUrls = dto.ExistingImageUrls ?? new List<string>();
            var imagesToRemove = station.Images.Where(i => !keepUrls.Contains(i.ImageUrl)).ToList();

            // Xóa file trên Firebase Storage trước khi xóa record
            foreach (var img in imagesToRemove)
            {
                await _fileStorageService.DeleteAsync(img.ImageUrl);
            }
            _stationRepo.RemoveImages(imagesToRemove);

            // 2. Upload ảnh mới lên Firebase Storage
            if (dto.Images?.Length > 0)
            {
                foreach (var file in dto.Images)
                {
                    if (file.Length > 0)
                    {
                        var imageUrl = await _fileStorageService.UploadAsync(file, $"stations/{station.Id}");
                        station.Images.Add(new StationImage
                        {
                            StationId = station.Id,
                            ImageUrl = imageUrl,
                            CreatedAt = DateTimeHelper.VietnamNow()
                        });
                    }
                }
            }

            await _unitOfWork.CompleteAsync();

            // Reload to get full data
            var updated = await _stationRepo.GetByIdAsync(station.Id) ?? station;
            return MapToDto(updated);
        }

        public async Task DeleteAsync(int id, int ownerUserId)
        {
            var station = await _stationRepo.GetByIdAsync(id, tracking: true, includeDetails: true);
            if (station == null)
                throw new KeyNotFoundException($"Station {id} not found.");
            if (station.OwnerUserId != ownerUserId)
                throw new UnauthorizedAccessException("You do not own this station.");

            // Only allow delete when Draft or Rejected
            if (station.ApprovalStatus != ApprovalStatus.Draft &&
                station.ApprovalStatus != ApprovalStatus.Rejected)
            {
                throw new InvalidOperationException(
                    $"Cannot delete station in '{station.ApprovalStatus}' status.");
            }

            // Check for active bookings on any slot
            var slotIds = station.ChargingSlots.Select(s => s.Id).ToList();
            var activeStatuses = new[]
            {
                BookingStatus.WaitingOwner, BookingStatus.PendingPayment,
                BookingStatus.Paid, BookingStatus.CheckedIn
            };
            var activeBookings = await _bookingRepo.GetActiveBookingsByStationIdsAsync(
                new List<int> { id }, activeStatuses);
            if (activeBookings.Any())
                throw new InvalidOperationException("Không thể xóa trạm có booking đang hoạt động.");

            // Remove images from Firebase Storage
            foreach (var img in station.Images)
            {
                await _fileStorageService.DeleteAsync(img.ImageUrl);
            }

            // Remove child entities
            _stationRepo.RemoveOperatingHours(station.OperatingHours);
            _stationRepo.RemoveImages(station.Images);
            _stationRepo.RemoveSlots(station.ChargingSlots);

            _stationRepo.Remove(station);
            await _unitOfWork.CompleteAsync();
        }

        // ─────────────── APPROVAL FLOW ───────────────

        public async Task SubmitForApprovalAsync(int id, int ownerUserId)
        {
            var station = await _stationRepo.GetByIdAsync(id, tracking: true, includeDetails: true);
            if (station == null)
                throw new KeyNotFoundException($"Station {id} not found.");
            if (station.OwnerUserId != ownerUserId)
                throw new UnauthorizedAccessException("You do not own this station.");

            // Can only submit from Draft or Rejected
            if (station.ApprovalStatus != ApprovalStatus.Draft &&
                station.ApprovalStatus != ApprovalStatus.Rejected)
            {
                throw new InvalidOperationException(
                    $"Cannot submit station in '{station.ApprovalStatus}' status. Only Draft or Rejected stations can be submitted.");
            }

            // Validate station data
            var errors = ValidateForSubmission(station);
            if (errors.Count > 0)
            {
                throw new InvalidOperationException(
                    $"Station validation failed: {string.Join("; ", errors)}");
            }

            // Transition to PendingApproval
            station.ApprovalStatus = ApprovalStatus.PendingApproval;
            station.SubmittedAt = DateTimeHelper.VietnamNow();
            station.AdminNote = null; // clear previous rejection note
            station.UpdatedAt = DateTimeHelper.VietnamNow();

            // Notify all Admin users
            var adminUsers = await _userManager.GetUsersInRoleAsync(RoleConstants.Admin);
            foreach (var admin in adminUsers)
                await _notificationService.SendAsync(
                    admin.Id,
                    "Trạm sạc mới chờ duyệt",
                    $"Trạm sạc \"{station.Name}\" đã được gửi yêu cầu phê duyệt.",
                    NotificationType.StationApproval);

            await _unitOfWork.CompleteAsync();
        }

        // ─────────────── UNAVAILABLE DATES ───────────────

        public async Task<List<UnavailableDateDto>> GetUnavailableDatesAsync(int stationId)
        {
            var dates = await _unavailableDateRepo.GetByStationIdAsync(stationId);

            return dates.Select(x => new UnavailableDateDto
            {
                Id = x.Id,
                StationId = x.StationId,
                Date = x.Date,
                Reason = x.Reason
            }).ToList();
        }

        public async Task<List<UnavailableDateDto>> AddUnavailableDatesAsync(int stationId, int ownerUserId, AddUnavailableDatesDto dto)
        {
            var station = await _stationRepo.GetByIdAsync(stationId);
            if (station == null) throw new KeyNotFoundException("Trạm không tồn tại");
            if (station.OwnerUserId != ownerUserId) throw new UnauthorizedAccessException("Bạn không phải chủ trạm");

            if (dto.Dates == null || dto.Dates.Count == 0) return new List<UnavailableDateDto>();

            // Distinct dates to process
            var requestedDates = dto.Dates.Distinct().OrderBy(d => d).ToList();

            // Check for overlaps with active bookings
            var activeStatuses = new[]
            {
                BookingStatus.WaitingOwner, BookingStatus.PendingPayment,
                BookingStatus.Paid, BookingStatus.CheckedIn
            };

            // Only check bookings that end after VietnamNow
            var now = DateTimeHelper.VietnamNow();
            var activeBookingsRaw = await _bookingRepo.GetActiveBookingsByStationIdsAsync(
                new List<int> { stationId }, activeStatuses);
            var activeBookings = activeBookingsRaw
                .Where(b => b.EndTime > now)
                .Select(b => new { b.Id, b.StartTime, b.EndTime })
                .ToList();

            var conflicts = new List<string>();

            // Map booking start/end times to Vietnam timezone Dates
            foreach (var b in activeBookings)
            {
                // We compare the request dates which are assumed to be Vietnam local DateOnly
                var bStartDt = b.StartTime;
                var bEndDt = b.EndTime;
                var bStartDate = DateOnly.FromDateTime(bStartDt);
                var bEndDate = DateOnly.FromDateTime(bEndDt);

                // If any requested date falls between bStartDate and bEndDate inclusive, it's a conflict
                foreach (var rd in requestedDates)
                {
                    if (rd >= bStartDate && rd <= bEndDate)
                    {
                        conflicts.Add(rd.ToString("yyyy-MM-dd"));
                    }
                }
            }

            var distinctConflicts = conflicts.Distinct().OrderBy(c => c).ToList();
            if (distinctConflicts.Count > 0)
            {
                throw new Exceptions.BookingConflictException("Có booking tồn tại trong các ngày được chọn.", distinctConflicts);
            }

            // If no conflicts, save the non-duplicate ones
            var existingDates = await _unavailableDateRepo.GetDatesByStationAsync(stationId);

            var addedRecords = new List<StationUnavailableDate>();
            foreach (var rd in requestedDates)
            {
                if (!existingDates.Contains(rd))
                {
                    var record = new StationUnavailableDate
                    {
                        StationId = stationId,
                        Date = rd,
                        Reason = dto.Reason,
                        CreatedAt = DateTimeHelper.VietnamNow()
                    };
                    _unavailableDateRepo.Add(record);
                    addedRecords.Add(record);
                }
            }

            await _unitOfWork.CompleteAsync();

            return addedRecords.Select(x => new UnavailableDateDto
            {
                Id = x.Id,
                StationId = x.StationId,
                Date = x.Date,
                Reason = x.Reason
            }).ToList();
        }

        public async Task RemoveUnavailableDatesAsync(int stationId, int ownerUserId, RemoveUnavailableDatesDto dto)
        {
            var station = await _stationRepo.GetByIdAsync(stationId);
            if (station == null) throw new KeyNotFoundException("Trạm không tồn tại");
            if (station.OwnerUserId != ownerUserId) throw new UnauthorizedAccessException("Bạn không phải chủ trạm");

            if (dto.Ids == null || dto.Ids.Count == 0) return;

            var records = await _unavailableDateRepo.GetByIdsAsync(stationId, dto.Ids);

            if (records.Count > 0)
            {
                _unavailableDateRepo.RemoveRange(records);
                await _unitOfWork.CompleteAsync();
            }
        }

        // ─────────────── ADMIN ───────────────

        // ─────────────── STATUS ───────────────

        public async Task<ChargingStationDto> UpdateOperationalStatusAsync(int id, int ownerUserId, string operationalStatus)
        {
            var station = await _stationRepo.GetByIdAsync(id, tracking: true, includeDetails: true);
            if (station == null) throw new KeyNotFoundException("Trạm sạc không tồn tại.");
            if (station.OwnerUserId != ownerUserId) throw new UnauthorizedAccessException("Bạn không phải chủ trạm.");

            if (station.ApprovalStatus != ApprovalStatus.Approved)
                throw new InvalidOperationException("Chỉ có thể thay đổi trạng thái hoạt động khi station đã được Approved.");

            if (!Enum.TryParse<OperationalStatus>(operationalStatus, true, out var newStatus))
                throw new InvalidOperationException("OperationalStatus không hợp lệ. Sử dụng: Active, Inactive.");

            if (newStatus == OperationalStatus.Inactive)
            {
                var now = DateTimeHelper.VietnamNow();
                var activeStatuses = new[]
                {
                    BookingStatus.WaitingOwner, BookingStatus.PendingPayment,
                    BookingStatus.Paid, BookingStatus.CheckedIn
                };
                var activeBookingsRaw = await _bookingRepo.GetActiveBookingsByStationIdsAsync(
                    new List<int> { id }, activeStatuses);
                
                var futureBookings = activeBookingsRaw.Where(b => b.EndTime > now).ToList();

                if (futureBookings.Any())
                {
                    // Kiểm tra xem có xe đang sạc dở không
                    var activeSessions = futureBookings.Where(b => b.Status == BookingStatus.CheckedIn).ToList();
                    if (activeSessions.Any())
                        throw new InvalidOperationException("Trạm đang có xe cắm sạc (CheckedIn). Không thể tắt trạm khẩn cấp. Vui lòng đợi xe sạc xong hoặc dừng phiên sạc thủ công.");

                    // Emergency Mass Cancel logic
                    var startOfMonth = new DateTime(now.Year, now.Month, 1);
                    bool hasUsedThisMonth = station.LastEmergencyCancelAt >= startOfMonth;

                    station.LastEmergencyCancelAt = now;

                    using var scope = _serviceProvider.CreateScope();
                    var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();

                    // Thực hiện hủy tất cả bookings
                    foreach (var booking in futureBookings)
                    {
                        var reason = hasUsedThisMonth 
                            ? "Trạm sạc bị hệ thống khóa do lạm dụng Hủy khẩn cấp."
                            : "Trạm sạc đóng cửa khẩn cấp.";
                        await bookingService.CancelSystemBookingAsync(booking.Id, reason);
                    }

                    if (hasUsedThisMonth)
                    {
                        // Phạt lần 2 trong tháng: Ban 30 ngày
                        station.BannedUntil = now.AddDays(30);
                        station.OperationalStatus = OperationalStatus.Inactive;
                        
                        await _notificationService.SendAsync(
                            ownerUserId,
                            "Trạm sạc bị đình chỉ",
                            "Trạm của bạn đã bị đình chỉ 30 ngày do lạm dụng Hủy Khẩn Cấp quá 1 lần/tháng. Các đặt chỗ hiện tại đã bị hủy.",
                            NotificationType.System);
                    }
                    else
                    {
                        // Cảnh báo lần 1
                        station.OperationalStatus = newStatus;
                        
                        await _notificationService.SendAsync(
                            ownerUserId,
                            "Kích hoạt Hủy Khẩn Cấp",
                            "Trạm của bạn đã kích hoạt Hủy Khẩn Cấp. Bạn đã dùng hết hạn mức 1 lần/tháng. Nếu tái phạm trong tháng này, trạm sẽ bị đình chỉ hoạt động 30 ngày.",
                            NotificationType.System);
                    }
                }
                else
                {
                    station.OperationalStatus = newStatus;
                }
            }
            else
            {
                station.OperationalStatus = newStatus;
            }

            station.UpdatedAt = DateTimeHelper.VietnamNow();
            await _unitOfWork.CompleteAsync();

            return MapToDto(station);
        }

        // ─────────────── PRICING ───────────────

        public async Task<List<StationPricingDto>> GetPricingAsync(int stationId, int ownerUserId)
        {
            var station = await _stationRepo.GetByIdAsync(stationId);
            if (station == null) throw new KeyNotFoundException("Trạm sạc không tồn tại.");
            if (station.OwnerUserId != ownerUserId) throw new UnauthorizedAccessException("Bạn không phải chủ trạm.");

            var pricings = await _pricingRepo.GetByStationIdAsync(stationId);
            return pricings.Select(MapPricingDto).ToList();
        }

        public async Task<StationPricingDto> CreatePricingAsync(int stationId, int ownerUserId, CreateStationPricingDto dto)
        {
            var station = await _stationRepo.GetByIdAsync(stationId);
            if (station == null) throw new KeyNotFoundException("Trạm sạc không tồn tại.");
            if (station.OwnerUserId != ownerUserId) throw new UnauthorizedAccessException("Bạn không phải chủ trạm.");

            if (!TimeOnly.TryParse(dto.StartTime, out var startTime) || !TimeOnly.TryParse(dto.EndTime, out var endTime))
                throw new InvalidOperationException("StartTime/EndTime phải ở dạng HH:mm, ví dụ 08:00");

            var pricing = new StationPricing
            {
                StationId = stationId,
                DayOfWeek = dto.DayOfWeek,
                StartTime = startTime,
                EndTime = endTime,
                PricePerHour = dto.PricePerHour,
                Priority = dto.Priority,
                EffectiveFrom = dto.EffectiveFrom ?? DateTimeHelper.VietnamNow(),
                EffectiveTo = dto.EffectiveTo,
                IsActive = true,
                CreatedAt = DateTimeHelper.VietnamNow()
            };

            _pricingRepo.Add(pricing);
            await _unitOfWork.CompleteAsync();

            return MapPricingDto(pricing);
        }

        public async Task<StationPricingDto> UpdatePricingAsync(int stationId, int pricingId, int ownerUserId, UpdateStationPricingDto dto)
        {
            var station = await _stationRepo.GetByIdAsync(stationId);
            if (station == null) throw new KeyNotFoundException("Trạm sạc không tồn tại.");
            if (station.OwnerUserId != ownerUserId) throw new UnauthorizedAccessException("Bạn không phải chủ trạm.");

            var pricing = await _pricingRepo.GetByIdAsync(pricingId, stationId);
            if (pricing == null) throw new KeyNotFoundException("Pricing rule không tồn tại.");

            if (!TimeOnly.TryParse(dto.StartTime, out var startTime) || !TimeOnly.TryParse(dto.EndTime, out var endTime))
                throw new InvalidOperationException("StartTime/EndTime phải ở dạng HH:mm");

            pricing.DayOfWeek = dto.DayOfWeek;
            pricing.StartTime = startTime;
            pricing.EndTime = endTime;
            pricing.PricePerHour = dto.PricePerHour;
            pricing.Priority = dto.Priority;
            pricing.EffectiveFrom = dto.EffectiveFrom ?? pricing.EffectiveFrom;
            pricing.EffectiveTo = dto.EffectiveTo;
            pricing.IsActive = dto.IsActive;

            _pricingRepo.Update(pricing);
            await _unitOfWork.CompleteAsync();

            return MapPricingDto(pricing);
        }

        public async Task DeletePricingAsync(int stationId, int pricingId, int ownerUserId)
        {
            var station = await _stationRepo.GetByIdAsync(stationId);
            if (station == null) throw new KeyNotFoundException("Trạm sạc không tồn tại.");
            if (station.OwnerUserId != ownerUserId) throw new UnauthorizedAccessException("Bạn không phải chủ trạm.");

            var pricing = await _pricingRepo.GetByIdAsync(pricingId, stationId);
            if (pricing == null) throw new KeyNotFoundException("Pricing rule không tồn tại.");

            _pricingRepo.Remove(pricing);
            await _unitOfWork.CompleteAsync();
        }

        private static StationPricingDto MapPricingDto(StationPricing p)
        {
            return new StationPricingDto
            {
                Id = p.Id,
                StationId = p.StationId,
                DayOfWeek = p.DayOfWeek,
                StartTime = p.StartTime,
                EndTime = p.EndTime,
                PricePerHour = p.PricePerHour,
                Priority = p.Priority,
                EffectiveFrom = p.EffectiveFrom,
                EffectiveTo = p.EffectiveTo,
                IsActive = p.IsActive,
                CreatedAt = p.CreatedAt
            };
        }

        // ─────────────── EXTRA SERVICES ───────────────

        public async Task<List<ExtraServiceDto>> GetExtraServicesAsync(int stationId, int ownerUserId)
        {
            var station = await _stationRepo.GetByIdAsync(stationId);
            if (station == null) throw new KeyNotFoundException("Trạm sạc không tồn tại.");
            if (station.OwnerUserId != ownerUserId) throw new UnauthorizedAccessException("Bạn không phải chủ trạm.");

            var services = await _extraServiceRepo.GetByStationIdAsync(stationId);
            return services.Select(MapExtraServiceDto).ToList();
        }

        public async Task<ExtraServiceDto> CreateExtraServiceAsync(int stationId, int ownerUserId, CreateExtraServiceDto dto)
        {
            var station = await _stationRepo.GetByIdAsync(stationId);
            if (station == null) throw new KeyNotFoundException("Trạm sạc không tồn tại.");
            if (station.OwnerUserId != ownerUserId) throw new UnauthorizedAccessException("Bạn không phải chủ trạm.");

            if (string.IsNullOrWhiteSpace(dto.ServiceName))
                throw new InvalidOperationException("Tên dịch vụ không được để trống.");

            if (dto.Price < 0)
                throw new InvalidOperationException("Giá dịch vụ không được âm.");

            var service = new ExtraService
            {
                StationId = stationId,
                ServiceName = dto.ServiceName.Trim(),
                Description = dto.Description?.Trim(),
                Price = dto.Price,
                TotalStock = dto.TotalStock,
                IsRental = dto.IsRental,
                IsActive = true,
                CreatedAt = DateTimeHelper.VietnamNow()
            };

            _extraServiceRepo.Add(service);
            await _unitOfWork.CompleteAsync();

            return MapExtraServiceDto(service);
        }

        public async Task<ExtraServiceDto> UpdateExtraServiceAsync(int stationId, int serviceId, int ownerUserId, UpdateExtraServiceDto dto)
        {
            var station = await _stationRepo.GetByIdAsync(stationId);
            if (station == null) throw new KeyNotFoundException("Trạm sạc không tồn tại.");
            if (station.OwnerUserId != ownerUserId) throw new UnauthorizedAccessException("Bạn không phải chủ trạm.");

            var service = await _extraServiceRepo.GetByIdAndStationIdAsync(serviceId, stationId);
            if (service == null) throw new KeyNotFoundException("Dịch vụ không tồn tại.");

            if (string.IsNullOrWhiteSpace(dto.ServiceName))
                throw new InvalidOperationException("Tên dịch vụ không được để trống.");

            if (dto.Price < 0)
                throw new InvalidOperationException("Giá dịch vụ không được âm.");

            service.ServiceName = dto.ServiceName.Trim();
            service.Description = dto.Description?.Trim();
            service.Price = dto.Price;
            service.TotalStock = dto.TotalStock;
            service.IsRental = dto.IsRental;
            service.IsActive = dto.IsActive;

            _extraServiceRepo.Update(service);
            await _unitOfWork.CompleteAsync();

            return MapExtraServiceDto(service);
        }

        public async Task DeleteExtraServiceAsync(int stationId, int serviceId, int ownerUserId)
        {
            var station = await _stationRepo.GetByIdAsync(stationId);
            if (station == null) throw new KeyNotFoundException("Trạm sạc không tồn tại.");
            if (station.OwnerUserId != ownerUserId) throw new UnauthorizedAccessException("Bạn không phải chủ trạm.");

            var service = await _extraServiceRepo.GetByIdAndStationIdAsync(serviceId, stationId);
            if (service == null) throw new KeyNotFoundException("Dịch vụ không tồn tại.");

            var hasBookings = await _extraServiceRepo.HasBookingsAsync(serviceId);
            if (hasBookings)
                throw new InvalidOperationException("Không thể xóa dịch vụ đã có booking sử dụng. Hãy tắt (IsActive = false) thay vì xóa.");

            _extraServiceRepo.Remove(service);
            await _unitOfWork.CompleteAsync();
        }

        private static ExtraServiceDto MapExtraServiceDto(ExtraService s)
        {
            return new ExtraServiceDto
            {
                Id = s.Id,
                ServiceName = s.ServiceName,
                Description = s.Description,
                Price = s.Price,
                TotalStock = s.TotalStock,
                IsRental = s.IsRental,
                IsActive = s.IsActive
            };
        }

        public async Task<PagedResultDto<ChargingStationDto>> GetAdminStationsAsync(string? status, string? search, string? ownerName, int page, int pageSize)
        {
            page = page <= 0 ? 1 : page;
            pageSize = pageSize <= 0 ? 10 : pageSize;
            if (pageSize > 100) pageSize = 100;

            var (items, total) = await _stationRepo.GetAdminStationsPagedAsync(status, search, ownerName, page, pageSize);

            var dtos = items.Select(MapToDto).ToList();

            return new PagedResultDto<ChargingStationDto>
            {
                Page = page,
                PageSize = pageSize,
                TotalItems = total,
                Items = dtos
            };
        }

        public async Task<List<ChargingStationDto>> GetPendingStationsAsync()
        {
            var stations = await _stationRepo.GetByApprovalStatusAsync(ApprovalStatus.PendingApproval);
            return stations.Select(MapToDto).ToList();
        }

        public async Task<PagedResultDto<ChargingStationDto>> GetPendingStationsPagedAsync(int page, int pageSize)
        {
            var result = await _stationRepo.GetByApprovalStatusPagedAsync(ApprovalStatus.PendingApproval, page, pageSize);
            return new PagedResultDto<ChargingStationDto>
            {
                Page = page,
                PageSize = pageSize,
                TotalItems = result.TotalCount,
                Items = result.Items.Select(MapToDto).ToList()
            };
        }

        public async Task<ChargingStationDto?> GetStationDetailForAdminAsync(int id)
        {
            var station = await _stationRepo.GetByIdAsync(id);
            if (station == null) return null;
            return MapToDto(station);
        }

        public async Task ReviewStationAsync(int id, int adminUserId, ReviewStationDto dto)
        {
            var station = await _stationRepo.GetByIdAsync(id, tracking: true, includeDetails: true);
            if (station == null)
                throw new KeyNotFoundException($"Station {id} not found.");

            if (station.ApprovalStatus != ApprovalStatus.PendingApproval)
                throw new InvalidOperationException(
                    $"Station is not pending approval. Current status: {station.ApprovalStatus}");

            station.ReviewedAt = DateTimeHelper.VietnamNow();
            station.ReviewedByUserId = adminUserId;
            station.AdminNote = dto.AdminNote;
            station.UpdatedAt = DateTimeHelper.VietnamNow();

            if (dto.IsApproved)
            {
                station.ApprovalStatus = ApprovalStatus.Approved;
                station.OperationalStatus = OperationalStatus.Active; // Publish

                // Activate all slots in this station
                var slots = station.ChargingSlots.ToList();
                foreach (var slot in slots)
                {
                    slot.Status = SlotStatus.Active;
                    slot.QrCodeToken = Guid.NewGuid().ToString("N");
                    slot.UpdatedAt = DateTimeHelper.VietnamNow();
                }

                // Notify Owner
                await _notificationService.SendAsync(
                    station.OwnerUserId,
                    "Trạm sạc đã được phê duyệt",
                    $"Trạm sạc \"{station.Name}\" đã được phê duyệt và công bố trên hệ thống.",
                    NotificationType.StationApproval);
            }
            else
            {
                // Reject — AdminNote is required
                if (string.IsNullOrWhiteSpace(dto.AdminNote))
                    throw new InvalidOperationException("Admin note is required when rejecting a station.");

                station.ApprovalStatus = ApprovalStatus.Rejected;

                // Notify Owner with rejection reason
                await _notificationService.SendAsync(
                    station.OwnerUserId,
                    "Trạm sạc bị từ chối",
                    $"Trạm sạc \"{station.Name}\" đã bị từ chối. Lý do: {dto.AdminNote}",
                    NotificationType.StationApproval);
            }

            await _unitOfWork.CompleteAsync();
        }

        public async Task<string> ToggleBanStationAsync(int id, int adminUserId, string? reason)
        {
            var station = await _stationRepo.GetByIdAsync(id, tracking: true);
            if (station == null) throw new KeyNotFoundException("Trạm sạc không tồn tại.");

            if (station.BannedUntil == null)
            {
                if (string.IsNullOrWhiteSpace(reason))
                    throw new InvalidOperationException("Vui lòng cung cấp lý do khi khóa trạm.");

                station.OperationalStatus = Enums.OperationalStatus.Inactive;
                // Lưu DateTime siêu xa báo hiệu khóa thủ công đến khi mở
                station.BannedUntil = DateTimeHelper.VietnamNow().AddYears(100); 
                station.AdminNote = string.IsNullOrEmpty(station.AdminNote) ? $"Khóa trạm: {reason}" : $"{station.AdminNote} | Khóa trạm: {reason}";
                
                await _notificationService.SendAsync(
                    station.OwnerUserId, 
                    "Trạm sạc bị khóa", 
                    $"Trạm sạc {station.Name} đã bị Admin khóa. Lý do: {reason}", 
                    NotificationType.System);
            }
            else
            {
                station.OperationalStatus = Enums.OperationalStatus.Active;
                station.BannedUntil = null;
                station.BanCount = 0; 
                
                await _notificationService.SendAsync(
                    station.OwnerUserId, 
                    "Trạm sạc được mở khóa", 
                    $"Trạm sạc {station.Name} đã được Admin mở khóa, có thể hoạt động trở lại.", 
                    NotificationType.System);
            }

            await _unitOfWork.CompleteAsync();
            return station.BannedUntil == null ? "Active" : "Banned";
        }

        // ─────────────── VALIDATION ───────────────

        private static List<string> ValidateForSubmission(ChargingStation station)
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(station.Name))
                errors.Add("Tên trạm không được để trống.");

            if (string.IsNullOrWhiteSpace(station.Address))
                errors.Add("Địa chỉ không được để trống.");

            if (station.Latitude == null || station.Longitude == null)
                errors.Add("Vị trí GPS (latitude/longitude) phải hợp lệ.");

            if (station.ChargingSlots == null || station.ChargingSlots.Count == 0)
                errors.Add("Trạm sạc phải có ít nhất 1 cổng sạc (slot).");

            return errors;
        }

        // ─────────────── MAPPING ───────────────

        private static ChargingStationDto MapToDto(ChargingStation station)
        {
            return new ChargingStationDto
            {
                Id = station.Id,
                OwnerUserId = station.OwnerUserId,
                OwnerName = station.Owner?.User?.FullName,
                Name = station.Name,
                Address = station.Address,
                Description = station.Description,
                Latitude = station.Latitude,
                Longitude = station.Longitude,
                LayoutImageUrl = station.LayoutImageUrl,
                LayoutWidth = station.LayoutWidth,
                LayoutHeight = station.LayoutHeight,
                ApprovalStatus = station.ApprovalStatus.ToString(),
                OperationalStatus = station.OperationalStatus.ToString(),
                AdminNote = station.AdminNote,
                CreatedAt = station.CreatedAt,
                UpdatedAt = station.UpdatedAt,
                BanCount = station.BanCount,
                BannedUntil = station.BannedUntil,
                Images = station.Images.Select(i => new StationImageDto
                {
                    Id = i.Id,
                    ImageUrl = i.ImageUrl
                }).ToList(),
                OperatingHours = station.OperatingHours.Select(h => new OperatingHoursDto
                {
                    DayOfWeek = h.DayOfWeek,
                    IsClosed = h.IsClosed,
                    OpenTime = h.OpenTime,
                    CloseTime = h.CloseTime
                }).ToList(),
                ChargingSlots = station.ChargingSlots.Select(s => new ChargingSlotDto
                {
                    Id = s.Id,
                    StationId = s.StationId,
                    SlotName = s.SlotName,
                    PositionX = s.PositionX,
                    PositionY = s.PositionY,
                    QrCodeToken = s.QrCodeToken,
                    Status = s.Status.ToString(),
                    CreatedAt = s.CreatedAt,
                    UpdatedAt = s.UpdatedAt
                }).ToList(),
                PricingTiers = station.StationPricings?.Where(p => p.IsActive).Select(p => new StationPricingDto
                {
                    Id = p.Id,
                    StationId = p.StationId,
                    DayOfWeek = p.DayOfWeek,
                    StartTime = p.StartTime,
                    EndTime = p.EndTime,
                    PricePerHour = p.PricePerHour,
                    Priority = p.Priority,
                    EffectiveFrom = p.EffectiveFrom,
                    EffectiveTo = p.EffectiveTo,
                    IsActive = p.IsActive,
                    CreatedAt = p.CreatedAt
                }).OrderBy(p => p.StartTime).ToList() ?? new(),
                AverageRating = station.AverageRating,
                TotalReviews = station.TotalReviews
            };
        }
    }
}

