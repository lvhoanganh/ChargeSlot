using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using System.Threading;
using System.Threading.Tasks;

namespace ChargeSlot.Api.Helpers
{
    public static class DatabaseFacadeExtensions
    {
        public static async Task<int> ExecuteSqlRawSafeAsync(
            this DatabaseFacade databaseFacade,
            string sql,
            params object[] parameters)
        {
            if (databaseFacade.ProviderName == "Microsoft.EntityFrameworkCore.InMemory")
            {
                return 0;
            }
            return await databaseFacade.ExecuteSqlRawAsync(sql, parameters);
        }
    }
}
