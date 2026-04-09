using ChargeSlot.Api.Constants;
using ChargeSlot.Api.DTOs.Admin.Overview;
using ChargeSlot.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace ChargeSlot.Api.Controllers
{
    [ApiController]
    [Route("api/admin/finance")]
    [Authorize(Roles = RoleConstants.Admin)]
    public class AdminFinanceController : ControllerBase
    {
        private readonly IWalletService _walletService;

        public AdminFinanceController(IWalletService walletService)
        {
            _walletService = walletService;
        }

        /// <summary>
        /// Xem thông tin ví của toàn dân và hệ thống (Lọc động + Phân trang)
        /// </summary>
        [HttpGet("wallets")]
        public async Task<IActionResult> GetAllWallets([FromQuery] WalletFilterDto filter)
        {
            var result = await _walletService.GetAdminAllWalletsAsync(filter);
            return Ok(result);
        }

        /// <summary>
        /// Dòm chi tiết sổ tay tài chính (Ledger) của một ví bất kỳ
        /// </summary>
        [HttpGet("wallets/{walletId}/transactions")]
        public async Task<IActionResult> GetWalletTransactions(int walletId, [FromQuery] TransactionFilterDto filter)
        {
            var result = await _walletService.GetAdminWalletTransactionsAsync(walletId, filter);
            return Ok(result);
        }

        /// <summary>
        /// Xem siêu chi tiết cấu trúc dòng tiền của 1 giao dịch (Sổ cái)
        /// </summary>
        [HttpGet("transactions/{transactionId}")]
        public async Task<IActionResult> GetTransactionDetail(long transactionId)
        {
            var result = await _walletService.GetAdminTransactionDetailAsync(transactionId);
            if (result == null) return NotFound(new { message = "Không tìm thấy giao dịch với ID này." });
            return Ok(result);
        }
    }
}
