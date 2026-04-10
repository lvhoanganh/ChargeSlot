using ChargeSlot.Api.Data;
using ChargeSlot.Api.Enums;
using ChargeSlot.Api.Models;
using ChargeSlot.Api.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ChargeSlot.Api.Repositories.Implementation
{
    public class ChargingStationRepository : IChargingStationRepository
    {
        private readonly ChargeSlotDbContext _context;

        public ChargingStationRepository(ChargeSlotDbContext context)
        {
            _context = context;
        }

        public async Task<ChargingStation?> GetByIdAsync(int id, bool tracking = false, bool includeDetails = true)
        {
            var query = _context.ChargingStations.AsQueryable();

            if (!tracking)
                query = query.AsNoTracking();

            if (includeDetails)
            {
                query = query
                    .Include(s => s.Images)
                    .Include(s => s.OperatingHours)
                    .Include(s => s.ChargingSlots)
                    .Include(s => s.StationPricings);
            }

            return await query.FirstOrDefaultAsync(s => s.Id == id);
        }

        public async Task<List<ChargingStation>> GetAllByOwnerAsync(int ownerUserId)
        {
            return await _context.ChargingStations
                .AsNoTracking()
                .Include(s => s.Images)
                .Include(s => s.OperatingHours)
                .Include(s => s.ChargingSlots)
                .Include(s => s.StationPricings)
                .Where(s => s.OwnerUserId == ownerUserId)
                .OrderByDescending(s => s.CreatedAt)
                .ToListAsync();
        }

        public async Task<List<ChargingStation>> GetByApprovalStatusAsync(ApprovalStatus status)
        {
            return await _context.ChargingStations
                .AsNoTracking()
                .Include(s => s.Images)
                .Include(s => s.OperatingHours)
                .Include(s => s.ChargingSlots)
                .Include(s => s.StationPricings)
                .Where(s => s.ApprovalStatus == status)
                .OrderByDescending(s => s.CreatedAt)
                .ToListAsync();
        }

        public async Task<List<ChargingStation>> GetPublicStationsAsync(string? keyword, decimal? minRating)
        {
            var query = _context.ChargingStations
                .Include(s => s.Images)
                .Include(s => s.OperatingHours)
                .Include(s => s.ChargingSlots)
                .Include(s => s.StationPricings)
                .Include(s => s.ExtraServices)
                .Include(s => s.UnavailableDates)
                .Where(s => s.ApprovalStatus == ApprovalStatus.Approved
                    && s.OperationalStatus == OperationalStatus.Active
                    && s.Owner.User.Status == ChargeSlot.Api.Constants.UserStatusConstants.Active);

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var kw = keyword.ToLower();
                query = query.Where(s => s.Name.ToLower().Contains(kw) || s.Address.ToLower().Contains(kw));
            }

            if (minRating.HasValue)
            {
                query = query.Where(s => s.AverageRating >= minRating.Value);
            }

            return await query.ToListAsync();
        }

        public async Task<List<ChargingStation>> GetPublicStationsWithCoordinatesAsync()
        {
            return await _context.ChargingStations
                .Include(s => s.Images)
                .Include(s => s.ChargingSlots)
                .Where(s => s.ApprovalStatus == ApprovalStatus.Approved
                    && s.OperationalStatus == OperationalStatus.Active
                    && s.Latitude.HasValue && s.Longitude.HasValue)
                .ToListAsync();
        }

        public async Task AddAsync(ChargingStation station)
        {
            await _context.ChargingStations.AddAsync(station);
        }

        public void Update(ChargingStation station)
        {
            _context.ChargingStations.Update(station);
        }

        public void Remove(ChargingStation station)
        {
            _context.ChargingStations.Remove(station);
        }

        public void RemoveOperatingHours(IEnumerable<StationOperatingHours> hours)
        {
            _context.StationOperatingHours.RemoveRange(hours);
        }

        public void RemoveImages(IEnumerable<StationImage> images)
        {
            _context.StationImages.RemoveRange(images);
        }

        public void RemoveSlots(IEnumerable<ChargingSlot> slots)
        {
            _context.ChargingSlots.RemoveRange(slots);
        }

        public async Task<List<ChargingStation>> GetTopRatedStationsAsync(int limit)
        {
            return await _context.ChargingStations
                .Include(s => s.Images)
                .Include(s => s.ChargingSlots)
                .Where(s => s.ApprovalStatus == ApprovalStatus.Approved
                    && s.OperationalStatus == OperationalStatus.Active
                    && s.TotalReviews > 0)
                .OrderByDescending(s => s.AverageRating)
                .ThenByDescending(s => s.TotalReviews)
                .Take(limit)
                .ToListAsync();
        }

        public async Task<List<ChargingStation>> GetAllByOwnerTrackingAsync(int ownerUserId)
        {
            return await _context.ChargingStations
                .Where(s => s.OwnerUserId == ownerUserId)
                .ToListAsync();
        }

        public async Task<(List<ChargingStation> Items, int Total)> GetAdminStationsPagedAsync(string? status, string? search, int page, int pageSize)
        {
            var query = _context.ChargingStations
                .Include(s => s.Owner).ThenInclude(o => o.User)
                .Include(s => s.Images)
                .Include(s => s.OperatingHours)
                .Include(s => s.ChargingSlots)
                .Include(s => s.StationPricings)
                .AsNoTracking()
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(status))
            {
                var sValue = status.Trim();
                if (Enum.TryParse<ApprovalStatus>(sValue, true, out var approvalStatus))
                {
                    query = query.Where(s => s.ApprovalStatus == approvalStatus);
                }
                else if (Enum.TryParse<OperationalStatus>(sValue, true, out var opStatus))
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

            return (items, total);
        }
        public async Task<List<ChargingStation>> GetBannedExpiredAsync(DateTime now)
        {
            return await _context.ChargingStations
                .Where(s => s.BannedUntil != null && s.BannedUntil <= now)
                .ToListAsync();
        }
    }
}

