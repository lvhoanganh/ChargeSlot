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
        private readonly ChargeSlotDbContext _db;

        public BankAccountController(ChargeSlotDbContext db)
        {
            _db = db;
        }

        private int GetUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        /// <summary>Xem danh sách tài khoản ngân hàng của mình.</summary>
        [HttpGet]
        public async Task<IActionResult> GetMyBankAccounts()
        {
            var list = await _db.BankAccounts
                .Where(b => b.UserId == GetUserId())
                .OrderByDescending(b => b.IsDefault)
                .ThenByDescending(b => b.CreatedAt)
                .Select(b => new BankAccountDto
                {
                    Id = b.Id,
                    BankName = b.BankName,
                    BankAccountNumber = b.BankAccountNumber,
                    BankAccountHolder = b.BankAccountHolder,
                    IsDefault = b.IsDefault,
                    CreatedAt = b.CreatedAt
                })
                .ToListAsync();
            return Ok(list);
        }

        /// <summary>Thêm tài khoản ngân hàng mới.</summary>
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateBankAccountDto dto)
        {
            var userId = GetUserId();

            using var transaction = await _db.Database.BeginTransactionAsync();
            try
            {
                // Nếu đặt làm mặc định → bỏ mặc định các TK cũ
                if (dto.IsDefault)
                {
                    var existing = await _db.BankAccounts
                        .Where(b => b.UserId == userId && b.IsDefault)
                        .ToListAsync();
                    existing.ForEach(b => b.IsDefault = false);
                }

                var account = new Models.BankAccount
                {
                    UserId = userId,
                    BankName = dto.BankName,
                    BankAccountNumber = dto.BankAccountNumber,
                    BankAccountHolder = dto.BankAccountHolder,
                    IsDefault = dto.IsDefault,
                    CreatedAt = DateTimeHelper.VietnamNow()
                };
                _db.BankAccounts.Add(account);
                await _db.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new BankAccountDto
                {
                    Id = account.Id,
                    BankName = account.BankName,
                    BankAccountNumber = account.BankAccountNumber,
                    BankAccountHolder = account.BankAccountHolder,
                    IsDefault = account.IsDefault,
                    CreatedAt = account.CreatedAt
                });
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        /// <summary>Đặt tài khoản ngân hàng làm mặc định.</summary>
        [HttpPut("{id}/set-default")]
        public async Task<IActionResult> SetDefault(int id)
        {
            var userId = GetUserId();

            using var transaction = await _db.Database.BeginTransactionAsync();
            try
            {
                var account = await _db.BankAccounts.FirstOrDefaultAsync(b => b.Id == id && b.UserId == userId);
                if (account == null) return NotFound(new { message = "Tài khoản ngân hàng không tồn tại." });

                // Bỏ mặc định tất cả
                var all = await _db.BankAccounts.Where(b => b.UserId == userId).ToListAsync();
                all.ForEach(b => b.IsDefault = false);
                account.IsDefault = true;
                await _db.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new { message = "Đã đặt làm tài khoản mặc định." });
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        /// <summary>Xóa tài khoản ngân hàng.</summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var userId = GetUserId();
            var account = await _db.BankAccounts.FirstOrDefaultAsync(b => b.Id == id && b.UserId == userId);
            if (account == null) return NotFound(new { message = "Tài khoản ngân hàng không tồn tại." });

            // Kiểm tra còn PayoutRequest pending không
            var hasPending = await _db.PayoutRequests.AnyAsync(p => p.BankAccountId == id && p.Status == Enums.PayoutStatus.Pending);
            if (hasPending)
                return BadRequest(new { message = "Không thể xóa — đang có yêu cầu rút tiền chờ duyệt." });

            _db.BankAccounts.Remove(account);
            await _db.SaveChangesAsync();
            return Ok(new { message = "Đã xóa tài khoản ngân hàng." });
        }
    }
}
