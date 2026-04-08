using ChargeSlot.Api.Data;
using ChargeSlot.Api.Models;
using ChargeSlot.Api.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ChargeSlot.Api.Repositories.Implementation
{
    public class LedgerTransactionRepository : ILedgerTransactionRepository
    {
        private readonly ChargeSlotDbContext _context;

        public LedgerTransactionRepository(ChargeSlotDbContext context)
        {
            _context = context;
        }

        public void Add(LedgerTransaction transaction)
        {
            _context.LedgerTransactions.Add(transaction);
        }

        public async Task<bool> HasTransactionWithMemoAsync(string memoSubstring)
        {
            return await _context.LedgerTransactions
                .AnyAsync(t => t.Memo != null && t.Memo.Contains(memoSubstring));
        }

        public async Task<(List<LedgerEntry> Items, int TotalCount)> GetAdminWalletTransactionsAsync(int walletId, ChargeSlot.Api.DTOs.Admin.Overview.TransactionFilterDto filter)
        {
            var query = _context.LedgerEntries
                .Include(e => e.LedgerTransaction)
                .Where(e => e.WalletId == walletId)
                .AsNoTracking()
                .AsQueryable();

            if (!string.IsNullOrEmpty(filter.TransactionType))
            {
                if (Enum.TryParse<ChargeSlot.Api.Enums.LedgerDirection>(filter.TransactionType, true, out var dirEnum))
                {
                    query = query.Where(e => e.Direction == dirEnum);
                }
            }

            if (filter.FromDate.HasValue)
            {
                query = query.Where(e => e.CreatedAt >= filter.FromDate.Value);
            }
            if (filter.ToDate.HasValue)
            {
                query = query.Where(e => e.CreatedAt <= filter.ToDate.Value);
            }

            int totalCount = await query.CountAsync();

            var items = await query
                .OrderByDescending(e => e.CreatedAt)
                .Skip((filter.Page - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .ToListAsync();

            return (items, totalCount);
        }
    }
}
