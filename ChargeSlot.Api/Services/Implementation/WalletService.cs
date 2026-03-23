using ChargeSlot.Api.DTOs.Wallet;
using ChargeSlot.Api.Enums;
using ChargeSlot.Api.Models;
using ChargeSlot.Api.Repositories.Interfaces;
using ChargeSlot.Api.Services.Interfaces;

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

        public WalletService(
            IWalletRepository walletRepo,
            IBookingRepository bookingRepo,
            IPaymentRepository paymentRepo,
            IChargingSlotRepository slotRepo,
            IVnPayService vnPayService,
            INotificationService notificationService)
        {
            _walletRepo = walletRepo;
            _bookingRepo = bookingRepo;
            _paymentRepo = paymentRepo;
            _slotRepo = slotRepo;
            _vnPayService = vnPayService;
            _notificationService = notificationService;
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
            // Dùng walletId + prefix "TOPUP" để phân biệt với payment booking
            var paymentUrl = _vnPayService.CreatePaymentUrl(
                wallet.Id * -1, // dùng số âm để phân biệt top-up vs booking payment
                amount,
                orderInfo,
                context);
            return paymentUrl;
        }

        /// <summary>
        /// Xử lý callback VNPay cho top-up
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

            var wallet = await _walletRepo.GetByIdAsync(walletId);
            if (wallet == null) return;

            // Parse amount từ VNPay (vnp_Amount / 100)
            var amountStr = query["vnp_Amount"].ToString();
            if (!long.TryParse(amountStr, out var vnpAmount)) return;
            var amount = vnpAmount / 100m;

            // Cộng tiền vào ví
            wallet.AvailableBalance += amount;
            await _walletRepo.UpdateAsync(wallet);

            // Ghi ledger: CREDIT vào ví
            var ledgerTx = new LedgerTransaction
            {
                ReferenceType = "TopUp",
                ReferenceId = wallet.Id,
                Memo = $"Nạp tiền {amount:N0} VND qua VNPay",
                CreatedByUserId = wallet.UserId,
                CreatedAt = DateTime.UtcNow,
                Entries = new List<LedgerEntry>
                {
                    new LedgerEntry
                    {
                        WalletId = wallet.Id,
                        Direction = LedgerDirection.Credit,
                        Amount = amount,
                        CreatedAt = DateTime.UtcNow
                    }
                }
            };
            await _walletRepo.AddLedgerTransactionAsync(ledgerTx);

            // Notify user
            if (wallet.UserId.HasValue)
            {
                await _notificationService.SendAsync(
                    wallet.UserId.Value,
                    "Nạp tiền thành công",
                    $"Đã nạp {amount:N0} VND vào ví. Số dư hiện tại: {wallet.AvailableBalance:N0} VND.",
                    NotificationType.Payment);
            }
        }

        /// <summary>
        /// Thanh toán booking bằng số dư ví
        /// </summary>
        public async Task<WalletDto> PayBookingByWalletAsync(int userId, int bookingId)
        {
            var wallet = await GetOrCreateWalletInternalAsync(userId);

            var booking = await _bookingRepo.GetByIdWithDetailsAsync(bookingId)
                ?? throw new InvalidOperationException("Booking không tồn tại.");

            if (booking.DriverUserId != userId)
                throw new UnauthorizedAccessException("Bạn không có quyền thanh toán booking này.");

            if (booking.Status != BookingStatus.PendingPayment)
                throw new InvalidOperationException("Booking không ở trạng thái chờ thanh toán.");

            if (booking.PaymentExpiresAt.HasValue && booking.PaymentExpiresAt.Value <= DateTime.UtcNow)
                throw new InvalidOperationException("Đã hết thời gian thanh toán.");

            if (wallet.AvailableBalance < booking.TotalAmount)
                throw new InvalidOperationException(
                    $"Số dư ví không đủ. Cần {booking.TotalAmount:N0} VND, hiện có {wallet.AvailableBalance:N0} VND.");

            // Trừ tiền ví Driver
            wallet.AvailableBalance -= booking.TotalAmount;
            await _walletRepo.UpdateAsync(wallet);

            // Cộng tiền vào ESCROW
            var escrowWallet = await _walletRepo.GetByIdAsync(1); // ESCROW wallet ID = 1
            if (escrowWallet != null)
            {
                escrowWallet.AvailableBalance += booking.TotalAmount;
                await _walletRepo.UpdateAsync(escrowWallet);
            }

            // Ghi ledger: DEBIT từ ví Driver, CREDIT vào ESCROW
            var ledgerTx = new LedgerTransaction
            {
                ReferenceType = "BookingPayment",
                ReferenceId = bookingId,
                Memo = $"Thanh toán booking #{bookingId} bằng ví - {booking.TotalAmount:N0}đ → ESCROW",
                CreatedByUserId = userId,
                CreatedAt = DateTime.UtcNow,
                Entries = new List<LedgerEntry>
                {
                    new LedgerEntry
                    {
                        WalletId = wallet.Id,
                        Direction = LedgerDirection.Debit,
                        Amount = booking.TotalAmount,
                        CreatedAt = DateTime.UtcNow
                    },
                    new LedgerEntry
                    {
                        WalletId = escrowWallet?.Id ?? 1,
                        Direction = LedgerDirection.Credit,
                        Amount = booking.TotalAmount,
                        CreatedAt = DateTime.UtcNow
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
                    PaidAt = DateTime.UtcNow,
                    GatewayTxnRef = $"WALLET_{wallet.Id}_{DateTime.UtcNow.Ticks}",
                    CreatedAt = DateTime.UtcNow
                };
                await _paymentRepo.CreateAsync(payment);
            }
            else
            {
                payment.Status = PaymentStatus.Completed;
                payment.PaymentMethod = PaymentMethod.Wallet;
                payment.PaidAt = DateTime.UtcNow;
                await _paymentRepo.UpdateAsync(payment);
            }

            // Set booking = Paid
            booking.Status = BookingStatus.Paid;
            await _bookingRepo.UpdateAsync(booking);

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

            return MapToDto(wallet);
        }

        /// <summary>
        /// Rút tiền từ ví (tạo yêu cầu)
        /// </summary>
        public async Task<WalletDto> WithdrawAsync(int userId, decimal amount)
        {
            var wallet = await GetOrCreateWalletInternalAsync(userId);

            if (wallet.AvailableBalance < amount)
                throw new InvalidOperationException(
                    $"Số dư không đủ. Hiện có {wallet.AvailableBalance:N0} VND.");

            // Freeze tiền → chờ Admin duyệt
            wallet.AvailableBalance -= amount;
            wallet.FrozenBalance += amount;
            await _walletRepo.UpdateAsync(wallet);

            // Ghi ledger
            var ledgerTx = new LedgerTransaction
            {
                ReferenceType = "Withdraw",
                ReferenceId = 0,
                Memo = $"Yêu cầu rút {amount:N0} VND",
                CreatedByUserId = userId,
                CreatedAt = DateTime.UtcNow,
                Entries = new List<LedgerEntry>
                {
                    new LedgerEntry
                    {
                        WalletId = wallet.Id,
                        Direction = LedgerDirection.Debit,
                        Amount = amount,
                        CreatedAt = DateTime.UtcNow
                    }
                }
            };
            await _walletRepo.AddLedgerTransactionAsync(ledgerTx);

            await _notificationService.SendAsync(
                userId,
                "Yêu cầu rút tiền",
                $"Yêu cầu rút {amount:N0} VND đã được gửi. Vui lòng chờ Admin xử lý.",
                NotificationType.System);

            return MapToDto(wallet);
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
                wallet = new Wallet
                {
                    UserId = userId,
                    WalletType = WalletType.Driver, // Default, sẽ detect từ role sau
                    AvailableBalance = 0,
                    FrozenBalance = 0,
                    CreatedAt = DateTime.UtcNow
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
    }
}
