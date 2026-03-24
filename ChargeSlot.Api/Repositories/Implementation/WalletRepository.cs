using ChargeSlot.Api.Data;
using ChargeSlot.Api.Models;
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

        public async Task<Wallet> CreateAsync(Wallet wallet)
        {
            _db.Wallets.Add(wallet);
            await _db.SaveChangesAsync();
            return wallet;
        }

        public async Task UpdateAsync(Wallet wallet)
        {
            _db.Wallets.Update(wallet);
            await _db.SaveChangesAsync();
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

        public async Task AddLedgerTransactionAsync(LedgerTransaction transaction)
        {
            _db.LedgerTransactions.Add(transaction);
            await _db.SaveChangesAsync();
        }
    }
}
