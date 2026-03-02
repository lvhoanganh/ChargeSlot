using System.Security.Claims;
using ChargeSlot.Api.DTOs.Profile;
using ChargeSlot.Api.Helpers;
using ChargeSlot.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChargeSlot.Api.Controllers
{
    [ApiController]
    [Route("api/owner/profile")]
    public class OwnerProfileController : ControllerBase
    {
        private readonly IOwnerProfileService _ownerProfileService;

        public OwnerProfileController(IOwnerProfileService ownerProfileService)
        {
            _ownerProfileService = ownerProfileService;
        }

        private int GetUserId()
        {
            var id = User.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? throw new InvalidOperationException("UserId missing in token");
            return int.Parse(id);
        }

        [HttpGet]
        [Authorize(Roles = RoleConstants.Owner)]
        public async Task<ActionResult<OwnerProfileDto>> GetMyProfile()
        {
            var userId = GetUserId();
            var profile = await _ownerProfileService.GetByUserIdAsync(userId);
            if (profile == null) return NotFound();

            return profile;
        }

        [HttpPut]
        [Authorize(Roles = RoleConstants.Owner)]
        public async Task<IActionResult> UpsertMyProfile([FromBody] OwnerProfileDto dto)
        {
            var userId = GetUserId();
            await _ownerProfileService.UpsertForUserAsync(userId, dto);
            return NoContent();
        }

        [HttpGet("{userId:int}")]
        [Authorize(Roles = RoleConstants.Admin)]
        public async Task<ActionResult<OwnerProfileDto>> GetByUserId(int userId)
        {
            var profile = await _ownerProfileService.GetByUserIdAsync(userId);
            if (profile == null) return NotFound();

            return profile;
        }

        [HttpPut("{userId:int}")]
        [Authorize(Roles = RoleConstants.Admin)]
        public async Task<IActionResult> UpdateByUserId(int userId, [FromBody] OwnerProfileDto dto)
        {
            await _ownerProfileService.UpsertForUserAsync(userId, dto);
            return NoContent();
        }

        [HttpDelete("{userId:int}")]
        [Authorize(Roles = RoleConstants.Admin)]
        public async Task<IActionResult> DeleteByUserId(int userId)
        {
            await _ownerProfileService.DeleteForUserAsync(userId);
            return NoContent();
        }
    }
}

