using ChargeSlot.Api.Data;
using ChargeSlot.Api.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore.Storage;
using ChargeSlot.Api.Helpers;

namespace ChargeSlot.Api.Repositories.Implementation
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly ChargeSlotDbContext _context;

        public UnitOfWork(ChargeSlotDbContext context)
        {
            _context = context;
        }

        public async Task<int> CompleteAsync()
        {
            return await _context.SaveChangesAsync();
        }

        public async Task<IDbContextTransaction> BeginTransactionAsync()
        {
            return await _context.Database.BeginTransactionAsync();
        }

        public async Task<int> ExecuteSqlRawSafeAsync(string sql, params object[] parameters)
        {
            return await _context.Database.ExecuteSqlRawSafeAsync(sql, parameters);
        }

        public void Dispose()
        {
            _context.Dispose();
        }
    }
}
