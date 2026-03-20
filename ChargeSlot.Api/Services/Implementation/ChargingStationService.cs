using ChargeSlot.Api.Constants;
using ChargeSlot.Api.Data;
using ChargeSlot.Api.DTOs.Slot;
using ChargeSlot.Api.DTOs.Station;
using ChargeSlot.Api.Enums;
using ChargeSlot.Api.Models;
using ChargeSlot.Api.Models.Identity;
using ChargeSlot.Api.Repositories.Interfaces;
using ChargeSlot.Api.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ChargeSlot.Api.Services.Implementation
{
    public class ChargingStationService : IChargingStationService
    {
        private readonly IChargingStationRepository _stationRepo;
        private readonly ChargeSlotDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ChargingStationService(
            IChargingStationRepository stationRepo,
            ChargeSlotDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _stationRepo = stationRepo;
            _context = context;
            _userManager = userManager;
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
            // Ensure Owner profile record exists (FK: ChargingStation.OwnerUserId → Owner.UserId)
            var ownerExists = await _context.Owner.AnyAsync(o => o.UserId == ownerUserId);
            if (!ownerExists)
            {
                // Auto-create Owner profile from ApplicationUser data
                var user = await _context.Users.FindAsync(ownerUserId);
                if (user == null)
                    throw new InvalidOperationException("User not found.");

                _context.Owner.Add(new Owner
                {
                    UserId = ownerUserId,
                    BusinessName = user.FullName,
                    TaxCode = "N/A",
                    CreatedAt = DateTime.UtcNow
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
                LayoutImageUrl = dto.LayoutImageUrl,
                LayoutWidth = dto.LayoutWidth,
                LayoutHeight = dto.LayoutHeight,
                ApprovalStatus = ApprovalStatus.Draft,
                OperationalStatus = OperationalStatus.Inactive,
                CreatedAt = DateTime.UtcNow
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
                        CreatedAt = DateTime.UtcNow
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
                        CreatedAt = DateTime.UtcNow
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
                    CreatedAt = DateTime.UtcNow
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
                CreatedAt = DateTime.UtcNow
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
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }

            // Save station first to get ID (needed for image folder + slot IDs)
            await _stationRepo.AddAsync(station);
            await _stationRepo.SaveChangesAsync();

            // Upload images to disk: wwwroot/uploads/stations/{stationId}/
            if (dto.Images?.Length > 0)
            {
                var uploadDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "stations", station.Id.ToString());
                Directory.CreateDirectory(uploadDir);

                foreach (var file in dto.Images)
                {
                    if (file.Length > 0)
                    {
                        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
                        var fileName = $"{Guid.NewGuid():N}{ext}";
                        var filePath = Path.Combine(uploadDir, fileName);

                        using (var stream = new FileStream(filePath, FileMode.Create))
                        {
                            await file.CopyToAsync(stream);
                        }

                        // Build public URL
                        var imageUrl = $"/uploads/stations/{station.Id}/{fileName}";
                        station.Images.Add(new StationImage
                        {
                            StationId = station.Id,
                            ImageUrl = imageUrl,
                            CreatedAt = DateTime.UtcNow
                        });
                    }
                }
                await _stationRepo.SaveChangesAsync();
            }

            // Station-level pricing → tạo SlotPricing cho TẤT CẢ slots
            if (dto.StationPricing?.Count > 0 && station.ChargingSlots.Count > 0)
            {
                var now = DateTime.UtcNow;
                foreach (var slot in station.ChargingSlots)
                {
                    foreach (var p in dto.StationPricing)
                    {
                        if (!TimeOnly.TryParse(p.StartTime, out var startTime) ||
                            !TimeOnly.TryParse(p.EndTime, out var endTime))
                            continue;

                        _context.Set<SlotPricing>().Add(new SlotPricing
                        {
                            SlotId = slot.Id,
                            StartTime = startTime,
                            EndTime = endTime,
                            PricePerHour = p.PricePerHour,
                            Priority = 1,
                            EffectiveFrom = now,
                            IsActive = true,
                            CreatedAt = now
                        });
                    }
                }
                await _context.SaveChangesAsync();
            }

            // Reload to get full data
            var created = await _stationRepo.GetByIdAsync(station.Id) ?? station;
            return MapToDto(created);
        }

        public async Task UpdateAsync(int id, int ownerUserId, UpdateChargingStationDto dto)
        {
            var station = await _stationRepo.GetByIdAsync(id, tracking: true, includeDetails: true);
            if (station == null)
                throw new KeyNotFoundException($"Station {id} not found.");
            if (station.OwnerUserId != ownerUserId)
                throw new UnauthorizedAccessException("You do not own this station.");

            // Only allow edits when Draft or Rejected
            if (station.ApprovalStatus != ApprovalStatus.Draft &&
                station.ApprovalStatus != ApprovalStatus.Rejected)
            {
                throw new InvalidOperationException(
                    $"Cannot edit station in '{station.ApprovalStatus}' status. Only Draft or Rejected stations can be edited.");
            }

            station.Name = dto.Name;
            station.Address = dto.Address;
            station.Description = dto.Description;
            station.Latitude = dto.Latitude;
            station.Longitude = dto.Longitude;
            station.LayoutImageUrl = dto.LayoutImageUrl;
            station.LayoutWidth = dto.LayoutWidth;
            station.LayoutHeight = dto.LayoutHeight;
            station.UpdatedAt = DateTime.UtcNow;

            // Replace operating hours
            if (dto.OperatingHours != null)
            {
                _context.StationOperatingHours.RemoveRange(station.OperatingHours);
                station.OperatingHours.Clear();

                foreach (var h in dto.OperatingHours)
                {
                    station.OperatingHours.Add(new StationOperatingHours
                    {
                        StationId = station.Id,
                        DayOfWeek = h.DayOfWeek,
                        IsClosed = h.IsClosed,
                        OpenTime = h.OpenTime,
                        CloseTime = h.CloseTime
                    });
                }
            }

            // Replace images
            if (dto.ImageUrls != null)
            {
                _context.StationImages.RemoveRange(station.Images);
                station.Images.Clear();

                foreach (var url in dto.ImageUrls)
                {
                    station.Images.Add(new StationImage
                    {
                        StationId = station.Id,
                        ImageUrl = url,
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }

            await _stationRepo.SaveChangesAsync();
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
            station.SubmittedAt = DateTime.UtcNow;
            station.AdminNote = null; // clear previous rejection note
            station.UpdatedAt = DateTime.UtcNow;

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
                    CreatedAt = DateTime.UtcNow
                });
            }

            await _stationRepo.SaveChangesAsync();
        }

        // ─────────────── ADMIN ───────────────

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

            station.ReviewedAt = DateTime.UtcNow;
            station.ReviewedByUserId = adminUserId;
            station.AdminNote = dto.AdminNote;
            station.UpdatedAt = DateTime.UtcNow;

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
                    slot.UpdatedAt = DateTime.UtcNow;
                }

                // Notify Owner
                _context.Notifications.Add(new Notification
                {
                    UserId = station.OwnerUserId,
                    Title = "Trạm sạc đã được phê duyệt",
                    Content = $"Trạm sạc \"{station.Name}\" đã được phê duyệt và công bố trên hệ thống.",
                    Type = NotificationType.StationApproval,
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow
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
                    CreatedAt = DateTime.UtcNow
                });
            }

            await _stationRepo.SaveChangesAsync();
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
                    UpdatedAt = s.UpdatedAt,
                    PricingTiers = s.SlotPricings?.Where(p => p.IsActive).Select(p => new SlotPricingDto
                    {
                        Id = p.Id,
                        SlotId = p.SlotId,
                        DayOfWeek = p.DayOfWeek,
                        StartTime = p.StartTime,
                        EndTime = p.EndTime,
                        PricePerHour = p.PricePerHour,
                        Priority = p.Priority,
                        EffectiveFrom = p.EffectiveFrom,
                        EffectiveTo = p.EffectiveTo,
                        IsActive = p.IsActive,
                        CreatedAt = p.CreatedAt
                    }).OrderBy(p => p.StartTime).ToList()
                }).ToList()
            };
        }
    }
}
