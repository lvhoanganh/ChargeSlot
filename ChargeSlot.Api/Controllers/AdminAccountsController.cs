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
            [FromQuery] int pageSize = 20)
        {
            try
            {
                var result = await _adminAccountService.GetAccountsAsync(
                    search, role, status, page, pageSize);

                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // PATCH: api/AdminAccounts/{id}/toggle-ban
        [HttpPatch("{id:int}/toggle-ban")]
        public async Task<IActionResult> ToggleBan([FromRoute] int id)
        {
            try
            {
                var adminIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!int.TryParse(adminIdStr, out var adminId))
                    return BadRequest(new { message = "Invalid token." });

                var newStatus = await _adminAccountService.ToggleBanStatusAsync(id, adminId);

                return Ok(new
                {
                    userId = id,
                    status = newStatus
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
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
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

    }
}