using ChargeSlot.Api.Constants;
using ChargeSlot.Api.Data;
using ChargeSlot.Api.DTOs.Slot;
using ChargeSlot.Api.DTOs.Station;
using ChargeSlot.Api.DTOs.Admin;
using ChargeSlot.Api.Enums;
using ChargeSlot.Api.Models;
using ChargeSlot.Api.Models.Identity;
using ChargeSlot.Api.Repositories.Interfaces;
using ChargeSlot.Api.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

using ChargeSlot.Api.Helpers;
namespace ChargeSlot.Api.Services.Implementation
{
    public class ChargingStationService : IChargingStationService
    {
        private readonly IChargingStationRepository _stationRepo;
        private readonly ChargeSlotDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IFileStorageService _fileStorageService;

        public ChargingStationService(
            IChargingStationRepository stationRepo,
            ChargeSlotDbContext context,
            UserManager<ApplicationUser> userManager,
            IFileStorageService fileStorageService)
        {
            _stationRepo = stationRepo;
            _context = context;
            _userManager = userManager;
            _fileStorageService = fileStorageService;
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

        public async Task<ChargingStationDto> CreateAsync(int ownerUserId, CreateChargingStationDto dto)
        {
            var ownerExists = await _context.Owner.FirstOrDefaultAsync(o => o.UserId == ownerUserId);
            if (ownerExists == null)
            {
                throw new InvalidOperationException("Chưa tìm thấy hồ sơ Chủ trạm. Vui lòng xác thực danh tính (KYC) trước khi tạo trạm sạc.");
            }

            if (ownerExists.KycStatus != KycStatus.Approved)
            {
                throw new InvalidOperationException("Hồ sơ doanh nghiệp chưa được duyệt. Vui lòng xác thực danh tính (KYC) và chờ Admin kiểm duyệt trước khi tạo trạm sạc.");
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
            await _stationRepo.SaveChangesAsync();

            return MapToDto(station);
        }

        /// <summary>
        /// Tạo trạm từ multipart/form-data: upload ảnh, tạo slots, operating hours, station-level pricing.
        /// </summary>
        public async Task<ChargingStationDto> CreateFromFormAsync(int ownerUserId, CreateStationFormDto dto, HttpRequest request)
        {
            // Ensure Owner profile record exists
            var ownerExists = await _context.Owner.AnyAsync(o => o.UserId == ownerUserId);
            if (!ownerExists)
            {
                var user = await _context.Users.FindAsync(ownerUserId)
                    ?? throw new InvalidOperationException("User not found.");
                _context.Owner.Add(new Owner
                {
                    UserId = ownerUserId,
                    BusinessName = user.FullName,
                    TaxCode = "N/A",
                    CreatedAt = DateTimeHelper.VietnamNow()
                });
                await _context.SaveChangesAsync();
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
            await _stationRepo.SaveChangesAsync();

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
                await _stationRepo.SaveChangesAsync();
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

                    _context.Set<StationPricing>().Add(new StationPricing
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
                await _context.SaveChangesAsync();
            }

            // Reload to get full data
            var created = await _stationRepo.GetByIdAsync(station.Id) ?? station;
            return MapToDto(created);
        }



        /// <summary>
        /// Cập nhật trạm từ multipart/form-data: upload ảnh mới, giữ ảnh cũ, thay đổi operating hours.
        /// Cho phép sửa ở MỌI trạng thái (không hạn chế Draft/Rejected).
        /// </summary>
        public async Task<ChargingStationDto> UpdateFromFormAsync(int id, int ownerUserId, UpdateStationFormDto dto, HttpRequest request)
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
                _context.StationOperatingHours.RemoveRange(station.OperatingHours);
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
            _context.StationImages.RemoveRange(imagesToRemove);

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

            await _stationRepo.SaveChangesAsync();

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
                BookingStatus.Paid, BookingStatus.CheckedIn, BookingStatus.InProgress
            };
            var hasActiveBookings = await _context.Bookings
                .AnyAsync(b => slotIds.Contains(b.SlotId) && activeStatuses.Contains(b.Status));
            if (hasActiveBookings)
                throw new InvalidOperationException("Không thể xóa trạm có booking đang hoạt động.");

            // Remove images from Firebase Storage
            foreach (var img in station.Images)
            {
                await _fileStorageService.DeleteAsync(img.ImageUrl);
            }

            // Remove child entities
            _context.StationOperatingHours.RemoveRange(station.OperatingHours);
            _context.StationImages.RemoveRange(station.Images);
            _context.ChargingSlots.RemoveRange(station.ChargingSlots);

            _stationRepo.Remove(station);
            await _stationRepo.SaveChangesAsync();
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
            {
                _context.Notifications.Add(new Notification
                {
                    UserId = admin.Id,
                    Title = "Trạm sạc mới chờ duyệt",
                    Content = $"Trạm sạc \"{station.Name}\" đã được gửi yêu cầu phê duyệt.",
                    Type = NotificationType.StationApproval,
                    IsRead = false,
                    CreatedAt = DateTimeHelper.VietnamNow()
                });
            }

            await _stationRepo.SaveChangesAsync();
        }

        // ─────────────── UNAVAILABLE DATES ───────────────

        public async Task<List<UnavailableDateDto>> GetUnavailableDatesAsync(int stationId)
        {
            var dates = await _context.StationUnavailableDates
                .Where(x => x.StationId == stationId)
                .OrderBy(x => x.Date)
                .ToListAsync();

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
                BookingStatus.Paid, BookingStatus.CheckedIn, BookingStatus.InProgress
            };

            var slotIds = await _context.ChargingSlots
                .Where(s => s.StationId == stationId)
                .Select(s => s.Id)
                .ToListAsync();

            // Only check bookings that end after VietnamNow
            var now = DateTimeHelper.VietnamNow();
            var activeBookings = await _context.Bookings
                .Where(b => slotIds.Contains(b.SlotId) && activeStatuses.Contains(b.Status) && b.EndTime > now)
                .Select(b => new { b.Id, b.StartTime, b.EndTime })
                .ToListAsync();

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
            var existingDates = await _context.StationUnavailableDates
                .Where(x => x.StationId == stationId)
                .Select(x => x.Date)
                .ToListAsync();

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
                    _context.StationUnavailableDates.Add(record);
                    addedRecords.Add(record);
                }
            }

            await _context.SaveChangesAsync();

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

            var records = await _context.StationUnavailableDates
                .Where(x => x.StationId == stationId && dto.Ids.Contains(x.Id))
                .ToListAsync();

            if (records.Count > 0)
            {
                _context.StationUnavailableDates.RemoveRange(records);
                await _context.SaveChangesAsync();
            }
        }

        // ─────────────── ADMIN ───────────────

        public async Task<PagedResultDto<ChargingStationDto>> GetAdminStationsAsync(string? status, string? search, int page, int pageSize)
        {
            page = page <= 0 ? 1 : page;
            pageSize = pageSize <= 0 ? 10 : pageSize;
            if (pageSize > 100) pageSize = 100;

            var query = _context.Set<ChargingStation>()
                .Include(s => s.Images)
                .Include(s => s.OperatingHours)
                .Include(s => s.ChargingSlots)
                .Include(s => s.StationPricings)
                .AsNoTracking()
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(status))
            {
                var sValue = status.Trim().ToUpper();
                // Filter by OperationalStatus 
                // e.g. "INACTIVE"
                if (Enum.TryParse<OperationalStatus>(sValue, true, out var opStatus))
                {
                    query = query.Where(s => s.OperationalStatus == opStatus);
                }
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var searchLower = search.Trim().ToLower();
                query = query.Where(s => s.Name.ToLower().Contains(searchLower) || s.Address.ToLower().Contains(searchLower));
            }

            var total = await query.CountAsync();

            var items = await query
                .OrderByDescending(s => s.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

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

        public async Task<ChargingStationDto?> GetStationDetailForAdminAsync(int id)
        {
            var station = await _stationRepo.GetByIdAsync(id);
            if (station == null) return null;
            return MapToDto(station);
        }

        public async Task ReviewStationAsync(int id, int adminUserId, ReviewStationDto dto)
        {
            var station = await _stationRepo.GetByIdAsync(id, tracking: true);
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
                var slots = await _context.ChargingSlots
                    .Where(s => s.StationId == station.Id)
                    .ToListAsync();
                foreach (var slot in slots)
                {
                    slot.Status = SlotStatus.Active;
                    slot.QrCodeToken = Guid.NewGuid().ToString("N");
                    slot.UpdatedAt = DateTimeHelper.VietnamNow();
                }

                // Notify Owner
                _context.Notifications.Add(new Notification
                {
                    UserId = station.OwnerUserId,
                    Title = "Trạm sạc đã được phê duyệt",
                    Content = $"Trạm sạc \"{station.Name}\" đã được phê duyệt và công bố trên hệ thống.",
                    Type = NotificationType.StationApproval,
                    IsRead = false,
                    CreatedAt = DateTimeHelper.VietnamNow()
                });
            }
            else
            {
                // Reject — AdminNote is required
                if (string.IsNullOrWhiteSpace(dto.AdminNote))
                    throw new InvalidOperationException("Admin note is required when rejecting a station.");

                station.ApprovalStatus = ApprovalStatus.Rejected;

                // Notify Owner with rejection reason
                _context.Notifications.Add(new Notification
                {
                    UserId = station.OwnerUserId,
                    Title = "Trạm sạc bị từ chối",
                    Content = $"Trạm sạc \"{station.Name}\" đã bị từ chối. Lý do: {dto.AdminNote}",
                    Type = NotificationType.StationApproval,
                    IsRead = false,
                    CreatedAt = DateTimeHelper.VietnamNow()
                });
            }

            await _stationRepo.SaveChangesAsync();
        }

        public async Task<string> ToggleBanStationAsync(int id, int adminUserId)
        {
            var station = await _stationRepo.GetByIdAsync(id, tracking: true);
            if (station == null) throw new KeyNotFoundException("Trạm sạc không tồn tại.");

            if (station.BannedUntil == null)
            {
                station.OperationalStatus = Enums.OperationalStatus.Inactive;
                station.BannedUntil = DateTimeHelper.VietnamNow().AddYears(100);
            }
            else
            {
                station.OperationalStatus = Enums.OperationalStatus.Active;
                station.BannedUntil = null;
                station.BanCount = 0; 
            }

            await _stationRepo.SaveChangesAsync();
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
