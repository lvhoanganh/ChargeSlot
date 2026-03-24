using System.Security.Claims;
using ChargeSlot.Api.DTOs.Profile;
using ChargeSlot.Api.Constants;
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

        /// <summary>Owner upload avatar (multipart/form-data).</summary>
        [HttpPost("avatar")]
        [Authorize(Roles = RoleConstants.Owner)]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadAvatar(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { message = "File không hợp lệ." });

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (ext is not (".jpg" or ".jpeg" or ".png" or ".webp"))
                return BadRequest(new { message = "Chỉ chấp nhận file ảnh (jpg, png, webp)." });

            if (file.Length > 5 * 1024 * 1024)
                return BadRequest(new { message = "File quá lớn. Tối đa 5MB." });

            var userId = GetUserId();
            var avatarUrl = await _ownerProfileService.UploadAvatarAsync(userId, file);
            return Ok(new { avatarUrl });
        }
    }
}

