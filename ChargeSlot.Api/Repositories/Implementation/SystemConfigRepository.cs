using ChargeSlot.Api.Data;
using ChargeSlot.Api.Models;
using ChargeSlot.Api.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ChargeSlot.Api.Repositories.Implementation
{
    public class SystemConfigRepository : ISystemConfigRepository
    {
        private readonly ChargeSlotDbContext _context;

        public SystemConfigRepository(ChargeSlotDbContext context)
        {
            _context = context;
        }

        public async Task<SystemConfig?> GetByKeyAsync(string key)
        {
            return await _context.SystemConfigs.FindAsync(key);
        }

        public async Task<List<string>> GetAllKeysAsync()
        {
            return await _context.SystemConfigs.Select(x => x.Key).ToListAsync();
        }

        public void Add(SystemConfig config)
        {
            _context.SystemConfigs.Add(config);
        }

        public void Update(SystemConfig config)
        {
            _context.SystemConfigs.Update(config);
        }
    }
}
