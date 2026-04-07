using ChargeSlot.Api.DTOs.Auth;
using ChargeSlot.Api.Enums;
using ChargeSlot.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ChargeSlot.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto dto)
        {
            try
            {
                await _authService.RegisterAsync(dto);
                return Ok(new { message = "Đăng ký thành công! Vui lòng kiểm tra email để xác thực tài khoản." });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi đăng ký." });
            }
        }

        /// <summary>Lấy thông tin tài khoản hiện tại (email, role, avatar, trạng thái verify...).</summary>
        [Authorize]
        [HttpGet("me")]
        public async Task<IActionResult> GetCurrentUser()
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
                var userInfo = await _authService.GetCurrentUserInfoAsync(userId);
                return Ok(userInfo);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi lấy thông tin tài khoản." });
            }
        }

        /// <summary>Cập nhật thông tin tài khoản cơ bản (ví dụ: họ và tên).</summary>
        [Authorize]
        [HttpPut("me")]
        public async Task<IActionResult> UpdateCurrentUser([FromBody] UpdateUserInfoDto dto)
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
                await _authService.UpdateCurrentUserInfoAsync(userId, dto);
                return Ok(new { message = "Cập nhật thông tin thành công." });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi cập nhật thông tin tài khoản." });
            }
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            try
            {
                var result = await _authService.LoginAsync(dto);
                return Ok(result);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi đăng nhập." });
            }
        }

        /// <summary>Get new access token using a valid refresh token.</summary>
        [HttpPost("refresh-token")]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenDto dto)
        {
            try
            {
                var result = await _authService.RefreshTokenAsync(dto.RefreshToken);
                return Ok(result);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi refresh token." });
            }
        }

        /// <summary>Revoke a refresh token (logout).</summary>
        [Authorize]
        [HttpPost("revoke-token")]
        public async Task<IActionResult> RevokeToken([FromBody] RefreshTokenDto dto)
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
                await _authService.RevokeTokenAsync(dto.RefreshToken, userId);
                return Ok(new { message = "Token revoked." });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi revoke token." });
            }
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto dto)
        {
            try
            {
                await _authService.ResetPasswordAsync(
                    dto.PhoneNumber,
                    dto.NewPassword,
                    dto.FirebaseIdToken
                );

                return Ok(new { message = "Password reset successful" });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi reset mật khẩu." });
            }
        }

        /// <summary>Đổi mật khẩu (cần đăng nhập, nhập mật khẩu cũ).</summary>
        [Authorize]
        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto dto)
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
                await _authService.ChangePasswordAsync(userId, dto.CurrentPassword, dto.NewPassword);
                return Ok(new { message = "Đổi mật khẩu thành công." });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi đổi mật khẩu." });
            }
        }

        /// <summary>Kiểm tra SĐT đã được đăng ký chưa (dùng trước khi gửi OTP để tránh lãng phí).</summary>
        [HttpGet("check-phone")]
        public async Task<IActionResult> CheckPhoneExists([FromQuery] string phoneNumber)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(phoneNumber))
                    return BadRequest(new { message = "Vui lòng cung cấp số điện thoại." });

                var exists = await _authService.CheckPhoneExistsAsync(phoneNumber);
                return Ok(new { exists });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi kiểm tra số điện thoại." });
            }
        }

        /// <summary>Xác thực email sau khi user click link trong email.</summary>
        [HttpPost("verify-email")]
        public async Task<IActionResult> VerifyEmail([FromBody] DTOs.Auth.VerifyEmailDto dto)
        {
            try
            {
                await _authService.VerifyEmailAsync(dto.UserId, dto.Token);
                return Ok(new { message = "Xác thực email thành công! Bạn có thể đăng nhập." });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi xác thực email." });
            }
        }

        /// <summary>User cũ thêm email (cần đăng nhập). Hệ thống sẽ gửi link verify tới email.</summary>
        [Authorize]
        [HttpPost("add-email")]
        public async Task<IActionResult> AddEmail([FromBody] DTOs.Auth.AddEmailDto dto)
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
                await _authService.AddEmailAsync(userId, dto.Email);
                return Ok(new { message = "Link xác thực đã được gửi đến email của bạn." });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi thêm email." });
            }
        }

        /// <summary>Gửi lại link xác thực email (không cần đăng nhập, dùng userId).</summary>
        [HttpPost("resend-verification")]
        public async Task<IActionResult> ResendVerification([FromBody] DTOs.Auth.ResendVerificationDto dto)
        {
            try
            {
                await _authService.ResendVerificationEmailAsync(dto.UserId);
                return Ok(new { message = "Link xác thực đã được gửi lại." });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi gửi lại link xác thực." });
            }
        }
    }
}
