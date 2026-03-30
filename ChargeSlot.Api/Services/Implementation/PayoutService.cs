using ChargeSlot.Api.Data;
using ChargeSlot.Api.DTOs.Payout;
using ChargeSlot.Api.Enums;
using ChargeSlot.Api.Helpers;
using ChargeSlot.Api.Models;
using ChargeSlot.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ChargeSlot.Api.Services.Implementation
{
    public class PayoutService : IPayoutService
    {
        private readonly ChargeSlotDbContext _db;
        private readonly INotificationService _notificationService;

        public PayoutService(ChargeSlotDbContext db, INotificationService notificationService)
        {
            _db = db;
            _notificationService = notificationService;
        }

        /// <summary>Owner tạo yêu cầu rút tiền từ ví Owner (ESCROW đã settle vào ví Owner).</summary>
        public async Task<PayoutRequestDto> CreatePayoutAsync(int ownerUserId, CreatePayoutDto dto)
        {
            // FIX: Transaction bảo vệ freeze + tạo request
            using var transaction = await _db.Database.BeginTransactionAsync();
            try
            {
                var owner = await _db.Owner.Include(o => o.User).FirstOrDefaultAsync(o => o.UserId == ownerUserId)
                    ?? throw new InvalidOperationException("Owner profile không tồn tại.");

                var bank = await _db.BankAccounts.FirstOrDefaultAsync(b => b.Id == dto.BankAccountId && b.UserId == ownerUserId)
                    ?? throw new InvalidOperationException("Tài khoản ngân hàng không tồn tại hoặc không thuộc về bạn.");

                var wallet = await _db.Wallets.FirstOrDefaultAsync(w => w.UserId == ownerUserId)
                    ?? throw new InvalidOperationException("Ví không tồn tại.");

                if (wallet.AvailableBalance < dto.Amount)
                    throw new InvalidOperationException(
                        $"Số dư không đủ. Hiện có {wallet.AvailableBalance:N0} VND.");

                wallet.AvailableBalance -= dto.Amount;
                wallet.FrozenBalance += dto.Amount;

                var request = new PayoutRequest
                {
                    OwnerUserId = ownerUserId,
                    BankAccountId = dto.BankAccountId,
                    Amount = dto.Amount,
                    Status = PayoutStatus.Pending,
                    Note = dto.Note,
                    RequestedAt = DateTimeHelper.VietnamNow()
                };
                _db.PayoutRequests.Add(request);
                await _db.SaveChangesAsync();

                await transaction.CommitAsync();

                await _notificationService.SendAsync(
                    ownerUserId,
                    "Yêu cầu rút tiền",
                    $"Yêu cầu rút {dto.Amount:N0} VND → {bank.BankName} ({bank.BankAccountNumber}) đã được gửi. Chờ Admin duyệt.",
                    NotificationType.System);

                return MapToDto(request, owner.User?.FullName, bank);
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        /// <summary>Owner xem danh sách yêu cầu rút tiền của mình.</summary>
        public async Task<List<PayoutRequestDto>> GetByOwnerAsync(int ownerUserId)
        {
            var owner = await _db.Owner.Include(o => o.User).FirstOrDefaultAsync(o => o.UserId == ownerUserId);
            var requests = await _db.PayoutRequests
                .Include(r => r.BankAccount)
                .Where(r => r.OwnerUserId == ownerUserId)
                .OrderByDescending(r => r.RequestedAt)
                .ToListAsync();

            return requests.Select(r => MapToDto(r, owner?.User?.FullName, r.BankAccount)).ToList();
        }

        /// <summary>Admin xem tất cả yêu cầu pending.</summary>
        public async Task<List<PayoutRequestDto>> GetAllPendingAsync()
        {
            var requests = await _db.PayoutRequests
                .Include(r => r.Owner).ThenInclude(o => o.User)
                .Include(r => r.BankAccount)
                .Where(r => r.Status == PayoutStatus.Pending)
                .OrderBy(r => r.RequestedAt)
                .ToListAsync();

            return requests.Select(r => MapToDto(r, r.Owner?.User?.FullName, r.BankAccount)).ToList();
        }

        /// <summary>Admin duyệt / từ chối.</summary>
        public async Task<PayoutRequestDto> ProcessPayoutAsync(int adminUserId, int requestId, ProcessPayoutDto dto)
        {
            // FIX: Transaction bảo vệ approve/reject + wallet update
            using var transaction = await _db.Database.BeginTransactionAsync();
            try
            {
                var request = await _db.PayoutRequests
                    .Include(r => r.Owner).ThenInclude(o => o.User)
                    .Include(r => r.BankAccount)
                    .FirstOrDefaultAsync(r => r.Id == requestId)
                    ?? throw new InvalidOperationException("Yêu cầu rút tiền không tồn tại.");

                if (request.Status != PayoutStatus.Pending)
                    throw new InvalidOperationException("Yêu cầu đã được xử lý trước đó.");

                var wallet = await _db.Wallets.FirstOrDefaultAsync(w => w.UserId == request.OwnerUserId)
                    ?? throw new InvalidOperationException("Ví Owner không tồn tại.");

                if (dto.Approve)
                {
                    wallet.FrozenBalance -= request.Amount;
                    request.Status = PayoutStatus.Processed;
                    
                    // Create Ledger Transaction for withdrawal
                    _db.Set<LedgerTransaction>().Add(new LedgerTransaction
                    {
                        ReferenceType = "Withdrawal",
                        ReferenceId = request.Id,
                        Memo = $"Rút {request.Amount:N0}đ về ngân hàng {request.BankAccount?.BankName}",
                        CreatedAt = DateTimeHelper.VietnamNow(),
                        Entries = new List<LedgerEntry>
                        {
                            new LedgerEntry 
                            { 
                                WalletId = wallet.Id, 
                                Direction = LedgerDirection.Debit, 
                                Amount = request.Amount, 
                                CreatedAt = DateTimeHelper.VietnamNow() 
                            }
                        }
                    });
                }
                else
                {
                    wallet.FrozenBalance -= request.Amount;
                    wallet.AvailableBalance += request.Amount;
                    request.Status = PayoutStatus.Rejected;
                }

                request.ProcessedAt = DateTimeHelper.VietnamNow();
                request.ProcessedByUserId = adminUserId;
                if (!string.IsNullOrEmpty(dto.Note)) request.Note = dto.Note;

                await _db.SaveChangesAsync();
                await transaction.CommitAsync();

                // Notifications (ngoài transaction)
                if (dto.Approve)
                {
                    await _notificationService.SendAsync(
                        request.OwnerUserId,
                        "Rút tiền thành công",
                        $"Yêu cầu rút {request.Amount:N0} VND đã được duyệt." +
                        (string.IsNullOrEmpty(dto.Note) ? "" : $" Ghi chú: {dto.Note}"),
                        NotificationType.Payment);
                }
                else
                {
                    await _notificationService.SendAsync(
                        request.OwnerUserId,
                        "Yêu cầu rút tiền bị từ chối",
                        $"Yêu cầu rút {request.Amount:N0} VND bị từ chối. Tiền đã hoàn lại ví." +
                        (string.IsNullOrEmpty(dto.Note) ? "" : $" Lý do: {dto.Note}"),
                        NotificationType.System);
                }

                return MapToDto(request, request.Owner?.User?.FullName, request.BankAccount);
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        private static PayoutRequestDto MapToDto(PayoutRequest r, string? ownerName, Models.BankAccount? bank)
        {
            return new PayoutRequestDto
            {
                Id = r.Id,
                OwnerUserId = r.OwnerUserId,
                OwnerName = ownerName,
                Amount = r.Amount,
                BankName = bank?.BankName ?? "",
                BankAccountNumber = bank?.BankAccountNumber ?? "",
                BankAccountHolder = bank?.BankAccountHolder ?? "",
                Status = r.Status.ToString(),
                RequestedAt = r.RequestedAt,
                ProcessedAt = r.ProcessedAt,
                ProcessedByUserId = r.ProcessedByUserId,
                Note = r.Note
            };
        }
    }
}
