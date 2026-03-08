using System.Security.Claims;
using ChargeSlot.Api.Constants;
using ChargeSlot.Api.DTOs.Profile;
using ChargeSlot.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChargeSlot.Api.Controllers
{
    [ApiController]
    [Route("api/driver/profile")]
    public class DriverProfileController : ControllerBase
    {
        private readonly IDriverProfileService _driverProfileService;

        public DriverProfileController(IDriverProfileService driverProfileService)
        {
            _driverProfileService = driverProfileService;
        }

        private int GetUserId()
        {
            var id = User.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? throw new InvalidOperationException("UserId missing in token");
            return int.Parse(id);
        }

        [HttpGet]
        [Authorize(Roles = RoleConstants.Driver)]
        public async Task<ActionResult<DriverProfileDto>> GetMyProfile()
        {
            var userId = GetUserId();
            var profile = await _driverProfileService.GetByUserIdAsync(userId);
            if (profile == null) return NotFound();
            return profile;
        }

        [HttpPut]
        [Authorize(Roles = RoleConstants.Driver)]
        public async Task<IActionResult> UpsertMyProfile([FromBody] DriverProfileDto dto)
        {
            var userId = GetUserId();
            await _driverProfileService.UpsertForUserAsync(userId, dto);
            return NoContent();
        }

        [HttpGet("{userId:int}")]
        [Authorize(Roles = RoleConstants.Admin)]
        public async Task<ActionResult<DriverProfileDto>> GetByUserId(int userId)
        {
            var profile = await _driverProfileService.GetByUserIdAsync(userId);
            if (profile == null) return NotFound();

            return profile;
        }

        [HttpPut("{userId:int}")]
        [Authorize(Roles = RoleConstants.Admin)]
        public async Task<IActionResult> UpdateByUserId(int userId, [FromBody] DriverProfileDto dto)
        {
            await _driverProfileService.UpsertForUserAsync(userId, dto);
            return NoContent();
        }

        [HttpDelete("{userId:int}")]
        [Authorize(Roles = RoleConstants.Admin)]
        public async Task<IActionResult> DeleteByUserId(int userId)
        {
            await _driverProfileService.DeleteForUserAsync(userId);
            return NoContent();
        }
    }
}

