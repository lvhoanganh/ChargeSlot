using ChargeSlot.Api.Data;
using ChargeSlot.Api.Models;
using ChargeSlot.Api.Enums;
using ChargeSlot.Api.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ChargeSlot.Api.Repositories.Implementation
{
    public class BankAccountRepository : IBankAccountRepository
    {
        private readonly ChargeSlotDbContext _context;

        public BankAccountRepository(ChargeSlotDbContext context)
        {
            _context = context;
        }

        public async Task<List<BankAccount>> GetByUserIdAsync(int userId)
        {
            return await _context.BankAccounts
                .Where(b => b.UserId == userId)
                .OrderByDescending(b => b.IsDefault)
                .ThenByDescending(b => b.CreatedAt)
                .ToListAsync();
        }

        public async Task<(List<BankAccount> Items, int TotalCount)> GetByUserIdPagedAsync(int userId, int page, int pageSize)
        {
            var query = _context.BankAccounts.Where(b => b.UserId == userId);
            int totalCount = await query.CountAsync();
            var items = await query
                .OrderByDescending(b => b.IsDefault)
                .ThenByDescending(b => b.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
            return (items, totalCount);
        }

        public async Task<BankAccount?> GetByIdAsync(int id, int userId)
        {
            return await _context.BankAccounts
                .FirstOrDefaultAsync(b => b.Id == id && b.UserId == userId);
        }

        public async Task<List<BankAccount>> GetDefaultByUserIdAsync(int userId)
        {
            return await _context.BankAccounts
                .Where(b => b.UserId == userId && b.IsDefault)
                .ToListAsync();
        }

        public async Task<bool> HasPendingWithdrawRequestsAsync(int userId, string bankName, string accountNumber)
        {
            return await _context.Set<WithdrawRequest>().AnyAsync(w =>
                w.UserId == userId
                && w.BankAccountNumber == accountNumber
                && w.BankName == bankName
                && (w.Status == WithdrawStatus.Pending || w.Status == WithdrawStatus.Approved || w.Status == WithdrawStatus.TransferCompleted));
        }

        public void Add(BankAccount account)
        {
            _context.BankAccounts.Add(account);
        }

        public void Remove(BankAccount account)
        {
            _context.BankAccounts.Remove(account);
        }

        public void Update(BankAccount account)
        {
            _context.BankAccounts.Update(account);
        }


        public async Task<BankAccount?> GetByIdAsync(int id)
        {
            return await _context.BankAccounts.FindAsync(id);
        }
    }
}
