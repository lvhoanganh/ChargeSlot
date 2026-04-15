using ChargeSlot.Api.Helpers;
using ChargeSlot.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ChargeSlot.Api.Constants;
namespace ChargeSlot.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = RoleConstants.Admin)]
    public class AdminAccountsController : ControllerBase
    {
        private readonly IAdminAccountService _adminAccountService;

        public AdminAccountsController(IAdminAccountService adminAccountService)
        {
            _adminAccountService = adminAccountService;
        }

        // GET: api/AdminAccounts?search=&role=&status=&page=1&pageSize=20
        [HttpGet]
        public async Task<IActionResult> GetAccounts(
            [FromQuery] string? search,
            [FromQuery] string? role,
            [FromQuery] string? status,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            try
            {
                var result = await _adminAccountService.GetAccountsAsync(
                    search, role, status, page, pageSize);

                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi tải danh sách tài khoản." });
            }
        }

        // GET: api/AdminAccounts/owner/{id}
        [HttpGet("owner/{id:int}")]
        public async Task<IActionResult> GetOwnerDetail(int id)
        {
            try
            {
                var result = await _adminAccountService.GetOwnerDetailAsync(id);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi tải chi tiết tài khoản." });
            }
        }

        // GET: api/AdminAccounts/driver/{id}
        [HttpGet("driver/{id:int}")]
        public async Task<IActionResult> GetDriverDetail(int id)
        {
            try
            {
                var result = await _adminAccountService.GetDriverDetailAsync(id);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi tải chi tiết tài khoản." });
            }
        }

        // POST: api/AdminAccounts/{id}/toggle-ban
        [HttpPost("{id:int}/toggle-ban")]
        public async Task<IActionResult> ToggleBan([FromRoute] int id, [FromBody] ChargeSlot.Api.DTOs.Admin.ToggleBanDto dto)
        {
            try
            {
                var adminIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!int.TryParse(adminIdStr, out var adminId))
                    return BadRequest(new { message = "Invalid token." });

                var newStatus = await _adminAccountService.ToggleBanStatusAsync(id, adminId, dto.Reason);

                return Ok(new
                {
                    userId = id,
                    status = newStatus
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi thay đổi trạng thái tài khoản." });
            }
        }

        // GET: api/AdminAccounts/statistics
        [HttpGet("statistics")]
        public async Task<IActionResult> GetStatistics()
        {
            try
            {
                var result = await _adminAccountService.GetAccountStatisticsAsync();
                return Ok(result);
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi tải thống kê." });
            }
        }

        // POST: api/AdminAccounts/secondary-password/setup
        [HttpPost("secondary-password/setup")]
        public async Task<IActionResult> SetupSecondaryPassword([FromBody] ChargeSlot.Api.DTOs.Admin.SetupSecondaryPasswordDto dto)
        {
            try
            {
                var adminUserIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(adminUserIdStr) || !int.TryParse(adminUserIdStr, out int adminUserId))
                    return Unauthorized("Không thể nhận diện user hiện tại.");

                await _adminAccountService.SetupSecondaryPasswordAsync(adminUserId, dto);
                return Ok(new { message = "Thiết lập mật khẩu cấp 2 thành công." });
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

        // POST: api/AdminAccounts/secondary-password/reset-request
        [HttpPost("secondary-password/reset-request")]
        public async Task<IActionResult> RequestResetSecondaryPassword()
        {
            try
            {
                var adminUserIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(adminUserIdStr) || !int.TryParse(adminUserIdStr, out int adminUserId))
                    return Unauthorized("Không thể nhận diện user hiện tại.");

                await _adminAccountService.RequestResetSecondaryPasswordAsync(adminUserId);
                return Ok(new { message = "Đã gửi mã OTP khôi phục qua email admin." });
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

        // POST: api/AdminAccounts/secondary-password/reset-confirm
        [HttpPost("secondary-password/reset-confirm")]
        public async Task<IActionResult> ConfirmResetSecondaryPassword([FromBody] ChargeSlot.Api.DTOs.Admin.ConfirmResetSecondaryPasswordDto dto)
        {
            try
            {
                var adminUserIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(adminUserIdStr) || !int.TryParse(adminUserIdStr, out int adminUserId))
                    return Unauthorized("Không thể nhận diện user hiện tại.");

                await _adminAccountService.ConfirmResetSecondaryPasswordAsync(adminUserId, dto);
                return Ok(new { message = "Khôi phục mật khẩu cấp 2 thành công." });
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
    }
}