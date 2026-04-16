using ChargeSlot.Api.Data;
using ChargeSlot.Api.Enums;
using ChargeSlot.Api.Models;
using ChargeSlot.Api.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ChargeSlot.Api.Repositories.Implementation
{
    public class WithdrawRequestRepository : IWithdrawRequestRepository
    {
        private readonly ChargeSlotDbContext _context;

        public WithdrawRequestRepository(ChargeSlotDbContext context)
        {
            _context = context;
        }

        public async Task<bool> HasPendingRequestsAsync(int userId, string bankAccountNumber, string bankName)
        {
            return await _context.WithdrawRequests.AnyAsync(w =>
                w.UserId == userId
                && w.BankAccountNumber == bankAccountNumber
                && w.BankName == bankName
                && (w.Status == WithdrawStatus.Pending || 
                    w.Status == WithdrawStatus.Approved || 
                    w.Status == WithdrawStatus.TransferCompleted));
        }

        public void Add(WithdrawRequest request)
        {
            _context.WithdrawRequests.Add(request);
        }

        public void Update(WithdrawRequest request)
        {
            _context.WithdrawRequests.Update(request);
        }

        public async Task<List<WithdrawRequest>> GetByUserIdAsync(int userId)
        {
            return await _context.WithdrawRequests
                .Where(r => r.UserId == userId)
                .OrderByDescending(r => r.RequestedAt)
                .ToListAsync();
        }

        public async Task<(List<WithdrawRequest> Items, int TotalCount)> GetByUserIdPagedAsync(int userId, int page, int pageSize)
        {
            var query = _context.WithdrawRequests.Where(r => r.UserId == userId);
            int totalCount = await query.CountAsync();
            var items = await query.OrderByDescending(r => r.RequestedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
            return (items, totalCount);
        }

        public async Task<List<WithdrawRequest>> GetPendingAsync()
        {
            return await _context.WithdrawRequests
                .Include(r => r.User)
                .Where(r => r.Status == WithdrawStatus.Pending)
                .OrderBy(r => r.RequestedAt)
                .ToListAsync();
        }

        public async Task<(List<WithdrawRequest> Items, int TotalCount)> GetPendingPagedAsync(int page, int pageSize)
        {
            var query = _context.WithdrawRequests.Include(r => r.User).Where(r => r.Status == WithdrawStatus.Pending);
            int totalCount = await query.CountAsync();
            var items = await query.OrderBy(r => r.RequestedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
            return (items, totalCount);
        }

        public async Task<WithdrawRequest?> GetByIdWithUserAndWalletAsync(int id)
        {
            return await _context.WithdrawRequests
                .Include(r => r.User)
                .Include(r => r.Wallet)
                .FirstOrDefaultAsync(r => r.Id == id);
        }

        public async Task<WithdrawRequest?> GetByIdWithUserAsync(int id)
        {
            return await _context.WithdrawRequests
                .Include(r => r.User)
                .FirstOrDefaultAsync(r => r.Id == id);
        }

        public async Task<List<WithdrawRequest>> GetAllWithUserAsync()
        {
            return await _context.WithdrawRequests
                .Include(r => r.User)
                .OrderByDescending(r => r.RequestedAt)
                .ToListAsync();
        }

        public async Task<(List<WithdrawRequest> Items, int TotalCount)> GetAllWithUserPagedAsync(int page, int pageSize)
        {
            var query = _context.WithdrawRequests.Include(r => r.User);
            int totalCount = await query.CountAsync();
            var items = await query.OrderByDescending(r => r.RequestedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
            return (items, totalCount);
        }
        public async Task<List<int>> GetExpiredTransferCompletedIdsAsync(DateTime deadline)
        {
            return await _context.WithdrawRequests
                .Where(r => r.Status == WithdrawStatus.TransferCompleted
                         && r.TransferredAt != null
                         && r.TransferredAt <= deadline)
                .Select(r => r.Id)
                .ToListAsync();
        }

        public async Task<List<WithdrawRequest>> GetTransferCompletedForReminderAsync(DateTime reminderStart, DateTime reminderEnd)
        {
            return await _context.WithdrawRequests
                .Where(r => r.Status == WithdrawStatus.TransferCompleted
                         && r.TransferredAt.HasValue
                         && r.TransferredAt.Value > reminderStart
                         && r.TransferredAt.Value <= reminderEnd
                         && !r.ReminderSentAt.HasValue)
                .ToListAsync();
        }
    }
}
