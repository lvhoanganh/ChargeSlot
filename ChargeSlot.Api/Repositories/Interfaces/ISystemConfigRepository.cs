using ChargeSlot.Api.Models;

namespace ChargeSlot.Api.Repositories.Interfaces
{
    public interface ISystemConfigRepository
    {
        Task<SystemConfig?> GetByKeyAsync(string key);
        Task<List<string>> GetAllKeysAsync();
        void Add(SystemConfig config);
        void Update(SystemConfig config);
    }
}
