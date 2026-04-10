using ChargeSlot.Api.Data;
using ChargeSlot.Api.Models;
using ChargeSlot.Api.Enums;
using ChargeSlot.Api.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ChargeSlot.Api.Repositories.Implementation
{
    public class WalletRepository : IWalletRepository
    {
        private readonly ChargeSlotDbContext _db;

        public WalletRepository(ChargeSlotDbContext db)
        {
            _db = db;
        }

        public async Task<Wallet?> GetByIdAsync(int id)
        {
            return await _db.Wallets.FindAsync(id);
        }

        public async Task<Wallet?> GetByUserIdAsync(int userId)
        {
            return await _db.Wallets
                .FirstOrDefaultAsync(w => w.UserId == userId);
        }

        public async Task<Wallet?> GetBySystemCodeAsync(string systemCode)
        {
            return await _db.Wallets
                .FirstOrDefaultAsync(w => w.SystemCode == systemCode);
        }

        public void Add(Wallet wallet)
        {
            _db.Wallets.Add(wallet);
        }

        public void Update(Wallet wallet)
        {
            _db.Wallets.Update(wallet);
        }

        public async Task<List<LedgerEntry>> GetTransactionHistoryAsync(int walletId, int take = 50)
        {
            return await _db.LedgerEntries
                .Include(e => e.LedgerTransaction)
                .Where(e => e.WalletId == walletId)
                .OrderByDescending(e => e.CreatedAt)
                .Take(take)
                .ToListAsync();
        }

        public void AddLedgerTransaction(LedgerTransaction transaction)
        {
            _db.LedgerTransactions.Add(transaction);
        }
        public async Task<(List<Wallet> Items, int TotalCount)> GetAdminAllWalletsAsync(ChargeSlot.Api.DTOs.Admin.Overview.WalletFilterDto filter)
        {
            var query = _db.Wallets
                .Include(w => w.User)
                .AsNoTracking()
                .AsQueryable();

            if (!string.IsNullOrEmpty(filter.WalletType))
            {
                if (Enum.TryParse<WalletType>(filter.WalletType, true, out var typeEnum))
                {
                    query = query.Where(w => w.WalletType == typeEnum);
                }
            }

            if (filter.UserId.HasValue)
            {
                query = query.Where(w => w.UserId == filter.UserId.Value);
            }

            if (!string.IsNullOrEmpty(filter.SystemCode))
            {
                query = query.Where(w => w.SystemCode == filter.SystemCode);
            }

            if (filter.FromDate.HasValue)
            {
                query = query.Where(w => w.CreatedAt >= filter.FromDate.Value);
            }
            if (filter.ToDate.HasValue)
            {
                query = query.Where(w => w.CreatedAt <= filter.ToDate.Value);
            }

            int totalCount = await query.CountAsync();

            var items = await query
                .OrderByDescending(w => w.CreatedAt)
                .Skip((filter.Page - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .ToListAsync();

            return (items, totalCount);
        }
    }
}

