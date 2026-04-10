using ChargeSlot.Api.Constants;
using ChargeSlot.Api.DTOs.Admin;
using ChargeSlot.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ChargeSlot.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = RoleConstants.Admin)]
    public class AdminConfigController : ControllerBase
    {
        private readonly ISystemConfigService _configService;

        public AdminConfigController(ISystemConfigService configService)
        {
            _configService = configService;
        }

        // GET: api/AdminConfig
        [HttpGet]
        public async Task<IActionResult> GetConfigs()
        {
            try
            {
                var configs = await _configService.GetCurrentConfigsAsync();
                return Ok(configs);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi máy chủ.", details = ex.Message });
            }
        }

        // PUT: api/AdminConfig
        [HttpPut]
        public async Task<IActionResult> UpdateConfigs([FromBody] UpdateSystemConfigsDto dto)
        {
            try
            {
                var adminUserIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(adminUserIdStr) || !int.TryParse(adminUserIdStr, out int adminUserId))
                    return Unauthorized("Không thể nhận diện user hiện tại.");

                await _configService.UpdateConfigsAsync(dto, adminUserId);
                return Ok(new { message = "Cập nhật cấu hình thành công!" });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi máy chủ.", details = ex.Message });
            }
        }
        
        // POST: api/AdminConfig/seed
        [HttpPost("seed")]
        public async Task<IActionResult> SeedConfigs()
        {
            try
            {
                await _configService.SeedDefaultConfigsAsync();
                return Ok(new { message = "Đã khởi tạo các cấu hình còn thiếu thành công." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi máy chủ.", details = ex.Message });
            }
        }
    }
}
