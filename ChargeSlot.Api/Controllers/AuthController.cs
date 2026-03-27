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

        private readonly IOtpService _otpService;

        public AuthController(
            IAuthService authService,
            IOtpService otpService)
        {
            _authService = authService;
            _otpService = otpService;
        }


        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto dto)
        {
            try
            {
                await _authService.RegisterAsync(dto);
                return Ok(new { message = "Register success" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
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
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
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
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
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
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto dto)
        {
            try
            {
                await _authService.ResetPasswordAsync(
                    dto.PhoneNumber,
                    dto.NewPassword
                );

                return Ok(new { message = "Password reset successful" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("register/send-otp")]
        public async Task<IActionResult> SendOtp([FromBody] SendOtpDto dto)
        {
            try
            {
                await _otpService.SendOtpRegister(
                    dto.PhoneNumber,
                    OtpPurpose.Register
                );
                return Ok(new { message = "OTP sent" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("register/verify-otp")]
        public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpDto dto)
        {
            try
            {
                await _otpService.VerifyOtpAsync(
                    dto.PhoneNumber,
                    dto.Otp,
                    OtpPurpose.Register
                   );
                return Ok(new { message = "OTP verified" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("forgot-password/send-otp")]
        public async Task<IActionResult> SendOtpForReset([FromBody] SendOtpDto dto)
        {
            try
            {
                await _otpService.SendOtpAsync(
                    dto.PhoneNumber,
                    OtpPurpose.ResetPassword
                );

                return Ok(new { message = "OTP sent for reset password" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("forgot-password/verify-otp")]
        public async Task<IActionResult> VerifyOtpForReset([FromBody] VerifyOtpDto dto)
        {
            try
            {
                await _otpService.VerifyOtpAsync(
                    dto.PhoneNumber,
                    dto.Otp,
                    OtpPurpose.ResetPassword
                );

                return Ok(new { message = "OTP verified for reset password" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
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
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

    }
}

