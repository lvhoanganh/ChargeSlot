using ChargeSlot.Api.Data;
using ChargeSlot.Api.DTOs.Wallet;
using ChargeSlot.Api.Enums;
using ChargeSlot.Api.Models;
using ChargeSlot.Api.Models.Identity;
using ChargeSlot.Api.Repositories.Interfaces;
using ChargeSlot.Api.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

using ChargeSlot.Api.Helpers;
namespace ChargeSlot.Api.Services.Implementation
{
    public class WalletService : IWalletService
    {
        private readonly IWalletRepository _walletRepo;
        private readonly IBookingRepository _bookingRepo;
        private readonly IPaymentRepository _paymentRepo;
        private readonly IChargingSlotRepository _slotRepo;
        private readonly IVnPayService _vnPayService;
        private readonly INotificationService _notificationService;
        private readonly ChargeSlotDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public WalletService(
            IWalletRepository walletRepo,
            IBookingRepository bookingRepo,
            IPaymentRepository paymentRepo,
            IChargingSlotRepository slotRepo,
            IVnPayService vnPayService,
            INotificationService notificationService,
            ChargeSlotDbContext db,
            UserManager<ApplicationUser> userManager)
        {
            _walletRepo = walletRepo;
            _bookingRepo = bookingRepo;
            _paymentRepo = paymentRepo;
            _slotRepo = slotRepo;
            _vnPayService = vnPayService;
            _notificationService = notificationService;
            _db = db;
            _userManager = userManager;
        }

        /// <summary>
        /// Lấy hoặc tạo ví cho user
        /// </summary>
        public async Task<WalletDto> GetOrCreateWalletAsync(int userId)
        {
            var wallet = await GetOrCreateWalletInternalAsync(userId);
            return MapToDto(wallet);
        }

        /// <summary>
        /// Nạp tiền vào ví qua VNPay → trả về URL redirect
        /// </summary>
        public async Task<string> TopUpViaVnPayAsync(int userId, decimal amount, HttpContext context)
        {
            var wallet = await GetOrCreateWalletInternalAsync(userId);
            var orderInfo = $"Nap tien vi ChargeSlot - {amount:N0} VND";

            // Build return URL chính xác cho top-up (khác với booking payment)
            var request = context.Request;
            var topUpReturnUrl = $"{request.Scheme}://{request.Host}/api/Wallet/top-up/vnpay-return";

            var paymentUrl = _vnPayService.CreatePaymentUrl(
                wallet.Id * -1, // dùng số âm để phân biệt top-up vs booking payment
                amount,
                orderInfo,
                context,
                topUpReturnUrl); // FIX Bug 1: dùng URL riêng cho top-up
            return paymentUrl;
        }

        /// <summary>
        /// Xử lý callback VNPay cho top-up (có idempotency + transaction)
        /// </summary>
        public async Task ProcessTopUpCallbackAsync(IQueryCollection query)
        {
            var (isValid, responseCode, txnRef) = _vnPayService.ValidateCallback(query);
            if (!isValid || responseCode != "00") return;

            // Parse walletId from txnRef (format: {walletId * -1}_{ticks})
            var walletIdStr = txnRef.Split('_').FirstOrDefault();
            if (!int.TryParse(walletIdStr, out var negativeWalletId)) return;
            var walletId = negativeWalletId * -1;
            if (walletId <= 0) return;

            // FIX Bug 2: Idempotency — kiểm tra txnRef đã xử lý chưa
            var alreadyProcessed = await _db.LedgerTransactions
                .AnyAsync(t => t.ReferenceType == "TopUp" && t.Memo!.Contains(txnRef));
            if (alreadyProcessed) return;

            // FIX Bug 3: Transaction — wrap tất cả trong DB transaction
            using var transaction = await _db.Database.BeginTransactionAsync();
            try
            {
                var wallet = await _walletRepo.GetByIdAsync(walletId);
                if (wallet == null) return;

                // Parse amount từ VNPay (vnp_Amount / 100)
                var amountStr = query["vnp_Amount"].ToString();
                if (!long.TryParse(amountStr, out var vnpAmount)) return;
                var amount = vnpAmount / 100m;

                // Cộng tiền vào ví
                wallet.AvailableBalance += amount;
                await _walletRepo.UpdateAsync(wallet);

                // Ghi ledger double-entry: DEBIT từ EXTERNAL (VNPay), CREDIT vào ví user
                var clearingWallet = await _db.Wallets.FirstAsync(w => w.SystemCode == "CLEARING");
                var ledgerTx = new LedgerTransaction
                {
                    ReferenceType = "TopUp",
                    ReferenceId = wallet.Id,
                    Memo = $"Nạp tiền {amount:N0} VND qua VNPay | TxnRef: {txnRef}",
                    CreatedByUserId = wallet.UserId,
                    CreatedAt = DateTimeHelper.VietnamNow(),
                    Entries = new List<LedgerEntry>
                    {
                        new LedgerEntry
                        {
                            WalletId = clearingWallet.Id,
                            Direction = LedgerDirection.Debit,
                            Amount = amount,
                            CreatedAt = DateTimeHelper.VietnamNow()
                        },
                        new LedgerEntry
                        {
                            WalletId = wallet.Id,
                            Direction = LedgerDirection.Credit,
                            Amount = amount,
                            CreatedAt = DateTimeHelper.VietnamNow()
                        }
                    }
                };
                await _walletRepo.AddLedgerTransactionAsync(ledgerTx);

                await transaction.CommitAsync();

                // Notify user (ngoài transaction — không cần rollback nếu notification lỗi)
                if (wallet.UserId.HasValue)
                {
                    await _notificationService.SendAsync(
                        wallet.UserId.Value,
                        "Nạp tiền thành công",
                        $"Đã nạp {amount:N0} VND vào ví. Số dư hiện tại: {wallet.AvailableBalance:N0} VND.",
                        NotificationType.Payment);
                }
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        /// <summary>
        /// Thanh toán booking bằng số dư ví
        /// </summary>
        public async Task<WalletDto> PayBookingByWalletAsync(int userId, int bookingId)
        {
            // Dùng transaction để đảm bảo tính nhất quán tài chính
            using var transaction = await _db.Database.BeginTransactionAsync();
            try
            {
            var wallet = await GetOrCreateWalletInternalAsync(userId);

            var booking = await _bookingRepo.GetByIdWithDetailsAsync(bookingId)
                ?? throw new InvalidOperationException("Booking không tồn tại.");

            if (booking.DriverUserId != userId)
                throw new UnauthorizedAccessException("Bạn không có quyền thanh toán booking này.");

            if (booking.Status != BookingStatus.PendingPayment)
                throw new InvalidOperationException("Booking không ở trạng thái chờ thanh toán.");

            if (booking.PaymentExpiresAt.HasValue && booking.PaymentExpiresAt.Value <= DateTimeHelper.VietnamNow())
                throw new InvalidOperationException("Đã hết thời gian thanh toán.");

            if (wallet.AvailableBalance < booking.TotalAmount)
                throw new InvalidOperationException(
                    $"Số dư ví không đủ. Cần {booking.TotalAmount:N0} VND, hiện có {wallet.AvailableBalance:N0} VND.");

            // Trừ tiền ví Driver
            wallet.AvailableBalance -= booking.TotalAmount;
            await _walletRepo.UpdateAsync(wallet);

            // Cộng tiền vào ESCROW (query by SystemCode thay vì hardcode ID)
            var escrowWallet = await _db.Wallets.FirstAsync(w => w.SystemCode == "ESCROW");
            escrowWallet.AvailableBalance += booking.TotalAmount;
            await _walletRepo.UpdateAsync(escrowWallet);

            // Ghi ledger: DEBIT từ ví Driver, CREDIT vào ESCROW
            var ledgerTx = new LedgerTransaction
            {
                ReferenceType = "BookingPayment",
                ReferenceId = bookingId,
                Memo = $"Thanh toán booking #{bookingId} bằng ví - {booking.TotalAmount:N0}đ → ESCROW",
                CreatedByUserId = userId,
                CreatedAt = DateTimeHelper.VietnamNow(),
                Entries = new List<LedgerEntry>
                {
                    new LedgerEntry
                    {
                        WalletId = wallet.Id,
                        Direction = LedgerDirection.Debit,
                        Amount = booking.TotalAmount,
                        CreatedAt = DateTimeHelper.VietnamNow()
                    },
                    new LedgerEntry
                    {
                        WalletId = escrowWallet?.Id ?? 1,
                        Direction = LedgerDirection.Credit,
                        Amount = booking.TotalAmount,
                        CreatedAt = DateTimeHelper.VietnamNow()
                    }
                }
            };
            await _walletRepo.AddLedgerTransactionAsync(ledgerTx);

            // Tạo Payment record
            var payment = await _paymentRepo.GetByBookingIdAsync(bookingId);
            if (payment == null)
            {
                payment = new Payment
                {
                    BookingId = bookingId,
                    Amount = booking.TotalAmount,
                    PaymentMethod = PaymentMethod.Wallet,
                    Status = PaymentStatus.Completed,
                    PaidAt = DateTimeHelper.VietnamNow(),
                    GatewayTxnRef = $"WALLET_{wallet.Id}_{DateTimeHelper.VietnamNow().Ticks}",
                    CreatedAt = DateTimeHelper.VietnamNow()
                };
                await _paymentRepo.CreateAsync(payment);
            }
            else
            {
                payment.Status = PaymentStatus.Completed;
                payment.PaymentMethod = PaymentMethod.Wallet;
                payment.PaidAt = DateTimeHelper.VietnamNow();
                await _paymentRepo.UpdateAsync(payment);
            }

            // Set booking = Paid
            booking.Status = BookingStatus.Paid;
            await _bookingRepo.UpdateAsync(booking);

            // Trừ stock cho ExtraServices (nếu có)
            if (booking.BookingExtraServices != null && booking.BookingExtraServices.Count > 0)
            {
                foreach (var bes in booking.BookingExtraServices)
                {
                    var svc = await _db.Set<ExtraService>().FindAsync(bes.ServiceId);
                    if (svc != null && svc.TotalStock.HasValue)
                    {
                        if (svc.TotalStock.Value < bes.Quantity)
                            throw new InvalidOperationException(
                                $"Dịch vụ '{svc.ServiceName}' đã hết hàng.");
                        svc.TotalStock -= bes.Quantity;
                    }
                }
                await _db.SaveChangesAsync();
            }

            // Lock slot
            var slot = await _slotRepo.GetByIdAsync(booking.SlotId, tracking: true);
            if (slot != null)
            {
                slot.Status = SlotStatus.Booked;
                _slotRepo.Update(slot);
                await _slotRepo.SaveChangesAsync();
            }

            // Notify
            await _notificationService.SendAsync(
                userId,
                "Thanh toán thành công",
                $"Thanh toán {booking.TotalAmount:N0}đ bằng ví cho slot {booking.ChargingSlot?.SlotName} — trạm {booking.ChargingSlot?.ChargingStation?.Name} ({booking.StartTime:HH:mm} - {booking.EndTime:HH:mm dd/MM}) thành công. Slot đã được giữ cho bạn.",
                NotificationType.Payment);

            // Notify Owner: Driver đã thanh toán
            var ownerUserId = booking.ChargingSlot?.ChargingStation?.OwnerUserId;
            if (ownerUserId.HasValue)
            {
                await _notificationService.SendAsync(
                    ownerUserId.Value,
                    "Khách đã thanh toán",
                    $"Slot {booking.ChargingSlot?.SlotName} — trạm {booking.ChargingSlot?.ChargingStation?.Name} ({booking.StartTime:HH:mm} - {booking.EndTime:HH:mm dd/MM}) đã được thanh toán {booking.TotalAmount:N0}đ bằng ví. Chờ Driver check-in.",
                    NotificationType.Payment);
            }

            await transaction.CommitAsync();
            return MapToDto(wallet);
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        /// <summary>
        /// Rút tiền từ ví (tạo yêu cầu + freeze tiền)
        /// </summary>
        public async Task<WithdrawRequestDto> WithdrawAsync(int userId, WithdrawDto dto)
        {
            var wallet = await GetOrCreateWalletInternalAsync(userId);

            if (wallet.AvailableBalance < dto.Amount)
                throw new InvalidOperationException(
                    $"Số dư không đủ. Hiện có {wallet.AvailableBalance:N0} VND.");

            // Freeze tiền → chờ Admin duyệt
            wallet.AvailableBalance -= dto.Amount;
            wallet.FrozenBalance += dto.Amount;
            await _walletRepo.UpdateAsync(wallet);

            // Tạo WithdrawRequest với snapshot thông tin ngân hàng
            var request = new WithdrawRequest
            {
                UserId = userId,
                WalletId = wallet.Id,
                Amount = dto.Amount,
                BankName = dto.BankName,
                BankAccountNumber = dto.BankAccountNumber,
                BankAccountHolder = dto.BankAccountHolder,
                UserNote = dto.UserNote,
                Status = WithdrawStatus.Pending,
                RequestedAt = DateTimeHelper.VietnamNow()
            };
            _db.Set<WithdrawRequest>().Add(request);
            await _db.SaveChangesAsync();

            // Ghi ledger double-entry: DEBIT từ ví user (available → frozen)
            var clearingWallet = await _db.Wallets.FirstAsync(w => w.SystemCode == "CLEARING");
            var ledgerTx = new LedgerTransaction
            {
                ReferenceType = "WithdrawRequest",
                ReferenceId = request.Id,
                Memo = $"Yêu cầu rút {dto.Amount:N0} VND → {dto.BankName} - {dto.BankAccountNumber}",
                CreatedByUserId = userId,
                CreatedAt = DateTimeHelper.VietnamNow(),
                Entries = new List<LedgerEntry>
                {
                    new LedgerEntry
                    {
                        WalletId = wallet.Id,
                        Direction = LedgerDirection.Debit,
                        Amount = dto.Amount,
                        CreatedAt = DateTimeHelper.VietnamNow()
                    },
                    new LedgerEntry
                    {
                        WalletId = clearingWallet.Id,
                        Direction = LedgerDirection.Credit,
                        Amount = dto.Amount,
                        CreatedAt = DateTimeHelper.VietnamNow()
                    }
                }
            };
            await _walletRepo.AddLedgerTransactionAsync(ledgerTx);

            // Load user name
            var user = await _userManager.FindByIdAsync(userId.ToString());

            await _notificationService.SendAsync(
                userId,
                "Yêu cầu rút tiền",
                $"Yêu cầu rút {dto.Amount:N0} VND → {dto.BankName} ({dto.BankAccountNumber}) đã được gửi. Vui lòng chờ Admin xử lý.",
                NotificationType.Wallet);

            // Notify all Admins về yêu cầu rút tiền mới
            var adminUsers = await _userManager.GetUsersInRoleAsync(Constants.RoleConstants.Admin);
            foreach (var admin in adminUsers)
            {
                await _notificationService.SendAsync(
                    admin.Id,
                    "Yêu cầu rút tiền mới",
                    $"User {user?.FullName ?? userId.ToString()} yêu cầu rút {dto.Amount:N0} VND → {dto.BankName} ({dto.BankAccountNumber}). Vui lòng xử lý.",
                    NotificationType.Wallet);
            }

            // Load user name for DTO
            return MapToWithdrawDto(request, user?.FullName);
        }

        /// <summary>
        /// User xem danh sách yêu cầu rút tiền của mình
        /// </summary>
        public async Task<List<WithdrawRequestDto>> GetUserWithdrawRequestsAsync(int userId)
        {
            var requests = await _db.Set<WithdrawRequest>()
                .Where(r => r.UserId == userId)
                .OrderByDescending(r => r.RequestedAt)
                .ToListAsync();

            var user = await _userManager.FindByIdAsync(userId.ToString());
            return requests.Select(r => MapToWithdrawDto(r, user?.FullName)).ToList();
        }

        /// <summary>
        /// Admin xem tất cả yêu cầu rút tiền pending
        /// </summary>
        public async Task<List<WithdrawRequestDto>> GetAllPendingWithdrawsAsync()
        {
            var requests = await _db.Set<WithdrawRequest>()
                .Include(r => r.User)
                .Where(r => r.Status == WithdrawStatus.Pending)
                .OrderBy(r => r.RequestedAt)
                .ToListAsync();

            return requests.Select(r => MapToWithdrawDto(r, r.User?.FullName)).ToList();
        }

        /// <summary>
        /// Admin duyệt / từ chối yêu cầu rút tiền
        /// </summary>
        public async Task<WithdrawRequestDto> ProcessWithdrawAsync(int adminUserId, int requestId, ProcessWithdrawDto dto)
        {
            var request = await _db.Set<WithdrawRequest>()
                .Include(r => r.User)
                .Include(r => r.Wallet)
                .FirstOrDefaultAsync(r => r.Id == requestId)
                ?? throw new InvalidOperationException("Yêu cầu rút tiền không tồn tại.");

            if (request.Status != WithdrawStatus.Pending)
                throw new InvalidOperationException("Yêu cầu đã được xử lý trước đó.");

            var wallet = request.Wallet;

            if (dto.Approve)
            {
                // Approve: trừ frozen, tiền đã được chuyển thực tế (ngoài hệ thống)
                wallet.FrozenBalance -= request.Amount;
                request.Status = WithdrawStatus.Approved;

                // Ghi ledger: DEBIT CLEARING → out (tiền rời hệ thống)
                var clearingWallet = await _db.Wallets.FirstAsync(w => w.SystemCode == "CLEARING");
                clearingWallet.AvailableBalance -= request.Amount;

                var ledgerTx = new LedgerTransaction
                {
                    ReferenceType = "WithdrawApproved",
                    ReferenceId = request.Id,
                    Memo = $"Admin duyệt rút {request.Amount:N0} VND → {request.BankName} - {request.BankAccountNumber}",
                    CreatedByUserId = adminUserId,
                    CreatedAt = DateTimeHelper.VietnamNow(),
                    Entries = new List<LedgerEntry>
                    {
                        new LedgerEntry
                        {
                            WalletId = clearingWallet.Id,
                            Direction = LedgerDirection.Debit,
                            Amount = request.Amount,
                            CreatedAt = DateTimeHelper.VietnamNow()
                        }
                    }
                };
                _db.LedgerTransactions.Add(ledgerTx);
                _db.Wallets.Update(clearingWallet);

                await _notificationService.SendAsync(
                    request.UserId,
                    "Rút tiền thành công",
                    $"Yêu cầu rút {request.Amount:N0} VND → {request.BankName} ({request.BankAccountNumber}) đã được duyệt." +
                    (string.IsNullOrEmpty(dto.AdminNote) ? "" : $" Ghi chú: {dto.AdminNote}"),
                    NotificationType.Payment);
            }
            else
            {
                // Reject: trả lại tiền frozen → available
                wallet.FrozenBalance -= request.Amount;
                wallet.AvailableBalance += request.Amount;
                request.Status = WithdrawStatus.Rejected;

                await _notificationService.SendAsync(
                    request.UserId,
                    "Yêu cầu rút tiền bị từ chối",
                    $"Yêu cầu rút {request.Amount:N0} VND đã bị từ chối. Tiền đã được hoàn lại ví." +
                    (string.IsNullOrEmpty(dto.AdminNote) ? "" : $" Lý do: {dto.AdminNote}"),
                    NotificationType.System);
            }

            request.ProcessedAt = DateTimeHelper.VietnamNow();
            request.ProcessedByUserId = adminUserId;
            request.AdminNote = dto.AdminNote;

            _db.Wallets.Update(wallet);
            _db.Set<WithdrawRequest>().Update(request);
            await _db.SaveChangesAsync();

            return MapToWithdrawDto(request, request.User?.FullName);
        }

        /// <summary>
        /// Lịch sử giao dịch ví
        /// </summary>
        public async Task<List<TransactionHistoryDto>> GetTransactionHistoryAsync(int userId)
        {
            var wallet = await GetOrCreateWalletInternalAsync(userId);
            var entries = await _walletRepo.GetTransactionHistoryAsync(wallet.Id);

            return entries.Select(e => new TransactionHistoryDto
            {
                Id = e.Id,
                Type = e.LedgerTransaction?.ReferenceType ?? "",
                Direction = e.Direction.ToString(),
                Amount = e.Amount,
                Memo = e.LedgerTransaction?.Memo,
                CreatedAt = e.CreatedAt
            }).ToList();
        }

        private async Task<Wallet> GetOrCreateWalletInternalAsync(int userId)
        {
            var wallet = await _walletRepo.GetByUserIdAsync(userId);
            if (wallet == null)
            {
                // Detect WalletType từ role thực tế của user
                var user = await _userManager.FindByIdAsync(userId.ToString());
                var walletType = WalletType.Driver;
                if (user != null)
                {
                    var roles = await _userManager.GetRolesAsync(user);
                    if (roles.Contains(Constants.RoleConstants.Owner))
                        walletType = WalletType.Owner;
                }

                wallet = new Wallet
                {
                    UserId = userId,
                    WalletType = walletType,
                    AvailableBalance = 0,
                    FrozenBalance = 0,
                    CreatedAt = DateTimeHelper.VietnamNow()
                };
                await _walletRepo.CreateAsync(wallet);
            }
            return wallet;
        }

        private static WalletDto MapToDto(Wallet w)
        {
            return new WalletDto
            {
                Id = w.Id,
                AvailableBalance = w.AvailableBalance,
                FrozenBalance = w.FrozenBalance,
                WalletType = w.WalletType.ToString(),
                CreatedAt = w.CreatedAt
            };
        }

        private static WithdrawRequestDto MapToWithdrawDto(WithdrawRequest r, string? userFullName)
        {
            return new WithdrawRequestDto
            {
                Id = r.Id,
                UserId = r.UserId,
                UserFullName = userFullName,
                Amount = r.Amount,
                BankName = r.BankName,
                BankAccountNumber = r.BankAccountNumber,
                BankAccountHolder = r.BankAccountHolder,
                Status = r.Status.ToString(),
                RequestedAt = r.RequestedAt,
                ProcessedAt = r.ProcessedAt,
                AdminNote = r.AdminNote,
                UserNote = r.UserNote
            };
        }
    }
}
