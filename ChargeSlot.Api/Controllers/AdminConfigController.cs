using ChargeSlot.Api.Constants;
using ChargeSlot.Api.Data;
using ChargeSlot.Api.DTOs.Admin;
using ChargeSlot.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using ChargeSlot.Api.Helpers;
namespace ChargeSlot.Api.Controllers
{
    /// <summary>
    /// Admin: quản lý cấu hình hệ thống (SystemConfig).
    /// </summary>
    // TODO: Refactor – move business logic to a dedicated AdminConfigService
    [ApiController]
    [Route("api/admin/config")]
    [Authorize(Roles = RoleConstants.Admin)]
    public class AdminConfigController : ControllerBase
    {
        private readonly ChargeSlotDbContext _db;

        public AdminConfigController(ChargeSlotDbContext db)
        {
            _db = db;
        }

        /// <summary>Lấy tất cả cấu hình hệ thống.</summary>
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var configs = await _db.SystemConfigs
                .OrderBy(c => c.Key)
                .Select(c => new
                {
                    c.Key,
                    c.Value,
                    c.Description,
                    c.UpdatedAt
                })
                .ToListAsync();

            return Ok(configs);
        }

        /// <summary>Cập nhật giá trị config.</summary>
        [HttpPut("{key}")]
        public async Task<IActionResult> Update(string key, [FromBody] UpdateConfigDto dto)
        {
            var config = await _db.SystemConfigs.FindAsync(key);
            if (config == null)
                return NotFound(new { message = $"Config '{key}' không tồn tại." });

            config.Value = dto.Value;
            config.UpdatedAt = DateTimeHelper.VietnamNow();
            await _db.SaveChangesAsync();

            return Ok(new { config.Key, config.Value, config.Description, config.UpdatedAt });
        }
    }
}
