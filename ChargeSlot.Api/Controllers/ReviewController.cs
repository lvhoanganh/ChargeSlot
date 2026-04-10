using ChargeSlot.Api.Constants;
using ChargeSlot.Api.DTOs.Review;
using ChargeSlot.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ChargeSlot.Api.Controllers
{
    [ApiController]
    [Route("api/reviews")]
    [Authorize]
    public class ReviewController : ControllerBase
    {
        private readonly IReviewService _reviewService;

        public ReviewController(IReviewService reviewService)
        {
            _reviewService = reviewService;
        }

        private int GetUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        /// <summary>Driver đánh giá trạm sạc (1-5 sao + comment). Chỉ sau khi booking Completed.</summary>
        [HttpPost]
        [Authorize(Roles = RoleConstants.Driver)]
        public async Task<IActionResult> CreateReview([FromBody] CreateReviewDto dto)
        {
            try
            {
                var result = await _reviewService.CreateReviewAsync(GetUserId(), dto);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>Owner phản hồi đánh giá.</summary>
        [HttpPut("{reviewId}/reply")]
        [Authorize(Roles = RoleConstants.Owner)]
        public async Task<IActionResult> ReplyToReview(int reviewId, [FromBody] OwnerReplyDto dto)
        {
            try
            {
                var result = await _reviewService.ReplyToReviewAsync(GetUserId(), reviewId, dto);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
        }

        /// <summary>Danh sách đánh giá của trạm (public, mới nhất trước).</summary>
        [HttpGet("station/{stationId}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetByStation(int stationId, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            var result = await _reviewService.GetByStationAsync(stationId, page, pageSize);
            return Ok(result);
        }

        /// <summary>Tổng quan rating của trạm (breakdown theo sao).</summary>
        [HttpGet("station/{stationId}/summary")]
        [AllowAnonymous]
        public async Task<IActionResult> GetRatingSummary(int stationId)
        {
            var result = await _reviewService.GetRatingSummaryAsync(stationId);
            if (result == null) return NotFound();
            return Ok(result);
        }

        /// <summary>Top trạm sạc theo rating (public, cho trang chủ).</summary>
        [HttpGet("top-stations")]
        [AllowAnonymous]
        public async Task<IActionResult> GetTopStations([FromQuery] int limit = 10)
        {
            var result = await _reviewService.GetTopRatedStationsAsync(limit);
            return Ok(result);
        }
    }
}
