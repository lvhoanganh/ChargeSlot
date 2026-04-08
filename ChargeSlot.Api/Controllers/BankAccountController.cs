using ChargeSlot.Api.Data;
using ChargeSlot.Api.DTOs.BankAccount;
using ChargeSlot.Api.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ChargeSlot.Api.Controllers
{
    // TODO: Refactor – move business logic to a dedicated BankAccountService
    [ApiController]
    [Route("api/bank-accounts")]
    [Authorize]
    public class BankAccountController : ControllerBase
    {
        private readonly ChargeSlot.Api.Services.Interfaces.IBankAccountService _bankAccountService;

        public BankAccountController(ChargeSlot.Api.Services.Interfaces.IBankAccountService bankAccountService)
        {
            _bankAccountService = bankAccountService;
        }

        private int GetUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        /// <summary>Xem danh sách tài khoản ngân hàng của mình.</summary>
        [HttpGet]
        public async Task<IActionResult> GetMyBankAccounts()
        {
            var list = await _bankAccountService.GetMyBankAccountsAsync(GetUserId());
            return Ok(list);
        }

        /// <summary>Thêm tài khoản ngân hàng mới.</summary>
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateBankAccountDto dto)
        {
            var userId = GetUserId();

            try
            {
                var result = await _bankAccountService.CreateAsync(userId, dto);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Lỗi khi tạo tài khoản: {ex.Message}" });
            }
        }

        /// <summary>Đặt tài khoản ngân hàng làm mặc định.</summary>
        [HttpPut("{id}/set-default")]
        public async Task<IActionResult> SetDefault(int id)
        {
            var userId = GetUserId();

            try
            {
                await _bankAccountService.SetDefaultAsync(id, userId);
                return Ok(new { message = "Đã đặt làm tài khoản mặc định." });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Lỗi: {ex.Message}" });
            }
        }

        /// <summary>Xóa tài khoản ngân hàng.</summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var userId = GetUserId();
            try
            {
                await _bankAccountService.DeleteAsync(id, userId);
                return Ok(new { message = "Đã xóa tài khoản ngân hàng." });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
