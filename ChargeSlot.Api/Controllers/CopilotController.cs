using ChargeSlot.Api.DTOs.Analytics;
using ChargeSlot.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ChargeSlot.Api.Controllers
{
    [Route("api/chat")]
    [ApiController]
    public class CopilotController : ControllerBase
    {
        private readonly IAiChatbotService _chatbotService;

        public CopilotController(IAiChatbotService chatbotService)
        {
            _chatbotService = chatbotService;
        }

        [HttpPost("driver")]
        [Authorize(Roles = "Driver")]
        public async Task<IActionResult> ProcessDriverChat([FromBody] ChatbotRequestDto request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out var userId)) return Unauthorized();

            var response = await _chatbotService.ProcessDriverChatAsync(userId, request);
            return Ok(response);
        }

        [HttpPost("owner")]
        [Authorize(Roles = "Owner")]
        public async Task<IActionResult> ProcessOwnerChat([FromBody] ChatbotRequestDto request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out var userId)) return Unauthorized();

            var response = await _chatbotService.ProcessOwnerChatAsync(userId, request);
            return Ok(response);
        }

        [HttpPost("admin")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ProcessAdminChat([FromBody] ChatbotRequestDto request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var response = await _chatbotService.ProcessAdminChatAsync(request);
            return Ok(response);
        }
    }
}
