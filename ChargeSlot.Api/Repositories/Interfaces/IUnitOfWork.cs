using Microsoft.EntityFrameworkCore.Storage;

namespace ChargeSlot.Api.Repositories.Interfaces
{
    public interface IUnitOfWork : IDisposable
    {
        Task<int> CompleteAsync();
        Task<IDbContextTransaction> BeginTransactionAsync();
        Task<int> ExecuteSqlRawSafeAsync(string sql, params object[] parameters);
    }
}
