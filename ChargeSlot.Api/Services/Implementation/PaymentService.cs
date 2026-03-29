using ChargeSlot.Api.Data;
using ChargeSlot.Api.Enums;
using ChargeSlot.Api.Models;
using ChargeSlot.Api.Repositories.Interfaces;
using ChargeSlot.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using ChargeSlot.Api.Helpers;
using Microsoft.Extensions.Configuration;
using ChargeSlot.Api.DTOs.Payment;
namespace ChargeSlot.Api.Services.Implementation
{
    public class PaymentService : IPaymentService
    {
        private readonly IBookingRepository _bookingRepo;
        private readonly IPaymentRepository _paymentRepo;
        private readonly IChargingSlotRepository _slotRepo;
        private readonly INotificationService _notificationService;
        private readonly IWalletRepository _walletRepo;
        private readonly ChargeSlotDbContext _db;
        private readonly ILogger<PaymentService> _logger;
        private readonly IConfiguration _configuration;

        public PaymentService(
            IBookingRepository bookingRepo,
            IPaymentRepository paymentRepo,
            IChargingSlotRepository slotRepo,
            INotificationService notificationService,
            IWalletRepository walletRepo,
            ChargeSlotDbContext db,
            ILogger<PaymentService> logger,
            IConfiguration configuration)
        {
            _bookingRepo = bookingRepo;
            _paymentRepo = paymentRepo;
            _slotRepo = slotRepo;
            _notificationService = notificationService;
            _walletRepo = walletRepo;
            _db = db;
            _logger = logger;
            _configuration = configuration;
        }

        /// <summary>
        /// Flow hoàn tất thanh toán: set Paid + lock slot + notify Driver.
        /// </summary>
        private async Task CompletePaymentAsync(Booking booking, Payment payment)
        {
            payment.Status = PaymentStatus.Completed;
            payment.PaidAt = DateTimeHelper.VietnamNow();
            await _paymentRepo.UpdateAsync(payment);

            booking.Status = BookingStatus.Paid;
            await _bookingRepo.UpdateAsync(booking);

            // Cộng tiền vào ESCROW wallet (VNPay đã thu tiền thực tế từ Driver)
            var escrowWallet = await _db.Wallets.FirstAsync(w => w.SystemCode == "ESCROW");
            var clearingWallet = await _db.Wallets.FirstAsync(w => w.SystemCode == "CLEARING");
            escrowWallet.AvailableBalance += booking.TotalAmount;
            await _db.SaveChangesAsync();

            // Ghi ledger double-entry: DEBIT từ CLEARING (VNPay gateway), CREDIT vào ESCROW
            var ledgerTx = new LedgerTransaction
            {
                ReferenceType = "BookingPayment",
                ReferenceId = booking.Id,
                Memo = $"Thanh toán booking #{booking.Id} qua SePay (VietQR) - {booking.TotalAmount:N0}đ → ESCROW",
                CreatedByUserId = booking.DriverUserId,
                CreatedAt = DateTimeHelper.VietnamNow(),
                Entries = new List<LedgerEntry>
                {
                    new LedgerEntry { WalletId = clearingWallet.Id, Direction = LedgerDirection.Debit, Amount = booking.TotalAmount, CreatedAt = DateTimeHelper.VietnamNow() },
                    new LedgerEntry { WalletId = escrowWallet.Id, Direction = LedgerDirection.Credit, Amount = booking.TotalAmount, CreatedAt = DateTimeHelper.VietnamNow() }
                }
            };
            await _walletRepo.AddLedgerTransactionAsync(ledgerTx);

            // Lock charging slot
            var slot = await _slotRepo.GetByIdAsync(booking.SlotId, tracking: true);
            if (slot != null)
            {
                slot.Status = SlotStatus.Booked;
                _slotRepo.Update(slot);
                await _slotRepo.SaveChangesAsync();
            }

            // Notify Driver
            await _notificationService.SendAsync(
                booking.DriverUserId,
                "Thanh toán thành công",
                $"Thanh toán {booking.TotalAmount:N0}đ cho slot {booking.ChargingSlot?.SlotName} — trạm {booking.ChargingSlot?.ChargingStation?.Name} ({booking.StartTime:HH:mm} - {booking.EndTime:HH:mm dd/MM}) thành công. Slot đã được giữ cho bạn.",
                NotificationType.Payment);

            // Notify Owner: Driver đã thanh toán
            var ownerUserId = booking.ChargingSlot?.ChargingStation?.OwnerUserId;
            if (ownerUserId.HasValue)
            {
                await _notificationService.SendAsync(
                    ownerUserId.Value,
                    "Khách đã thanh toán",
                    $"Slot {booking.ChargingSlot?.SlotName} — trạm {booking.ChargingSlot?.ChargingStation?.Name} ({booking.StartTime:HH:mm} - {booking.EndTime:HH:mm dd/MM}) đã được thanh toán {booking.TotalAmount:N0}đ. Chờ Driver check-in.",
                    NotificationType.Payment);
            }
        }

        /// <summary>
        /// Hoàn tiền vào ví Driver khi không thể recover booking (slot conflict).
        /// </summary>
        private async Task RefundToDriverWalletAsync(Booking booking, Payment payment)
        {
            payment.Status = PaymentStatus.Refunded;
            payment.PaidAt = DateTimeHelper.VietnamNow();
            await _paymentRepo.UpdateAsync(payment);

            // Tạo hoặc lấy ví driver
            var driverWallet = await _walletRepo.GetByUserIdAsync(booking.DriverUserId);
            if (driverWallet == null)
            {
                driverWallet = new Wallet
                {
                    UserId = booking.DriverUserId,
                    WalletType = WalletType.Driver,
                    AvailableBalance = 0,
                    FrozenBalance = 0,
                    CreatedAt = DateTimeHelper.VietnamNow()
                };
                await _walletRepo.CreateAsync(driverWallet);
            }

            driverWallet.AvailableBalance += booking.TotalAmount;
            await _walletRepo.UpdateAsync(driverWallet);

            // Ghi ledger double-entry: DEBIT từ CLEARING (VNPay refund), CREDIT vào ví Driver
            var clearingWallet = await _db.Wallets.FirstAsync(w => w.SystemCode == "CLEARING");
            var ledgerTx = new LedgerTransaction
            {
                ReferenceType = "PaymentRaceRefund",
                ReferenceId = booking.Id,
                Memo = $"Hoàn tiền booking #{booking.Id} do hết hạn thanh toán nhưng chuyển khoản đã thành công - {booking.TotalAmount:N0}đ → Ví Driver",
                CreatedByUserId = null,
                CreatedAt = DateTimeHelper.VietnamNow(),
                Entries = new List<LedgerEntry>
                {
                    new LedgerEntry
                    {
                        WalletId = clearingWallet.Id,
                        Direction = LedgerDirection.Debit,
                        Amount = booking.TotalAmount,
                        CreatedAt = DateTimeHelper.VietnamNow()
                    },
                    new LedgerEntry
                    {
                        WalletId = driverWallet.Id,
                        Direction = LedgerDirection.Credit,
                        Amount = booking.TotalAmount,
                        CreatedAt = DateTimeHelper.VietnamNow()
                    }
                }
            };
            await _walletRepo.AddLedgerTransactionAsync(ledgerTx);

            // Notify Driver
            await _notificationService.SendAsync(
                booking.DriverUserId,
                "Hoàn tiền tự động",
                $"Yêu cầu đặt chỗ tại slot {booking.ChargingSlot?.SlotName} — trạm {booking.ChargingSlot?.ChargingStation?.Name} đã hết hạn nhưng bạn đã chuyển khoản thành công. {booking.TotalAmount:N0}đ đã hoàn vào ví của bạn.",
                NotificationType.Payment);
        }

        /// <summary>
        /// Tạo link thanh toán VietQR (qua SePay)
        /// </summary>
        public async Task<string> CreateSePayQrUrlAsync(int bookingId, int driverUserId)
        {
            var booking = await _bookingRepo.GetByIdWithDetailsAsync(bookingId)
                ?? throw new InvalidOperationException("Booking không tồn tại.");

            if (booking.DriverUserId != driverUserId)
                throw new UnauthorizedAccessException("Bạn không có quyền thanh toán booking này.");

            if (booking.Status != BookingStatus.PendingPayment)
                throw new InvalidOperationException("Booking không ở trạng thái chờ thanh toán.");

            if (booking.PaymentExpiresAt.HasValue && booking.PaymentExpiresAt.Value <= DateTimeHelper.VietnamNow())
                throw new InvalidOperationException("Đã hết thời gian thanh toán.");

            var payment = await _paymentRepo.GetByBookingIdAsync(bookingId);
            if (payment == null)
            {
                payment = new Payment
                {
                    BookingId = bookingId,
                    Amount = booking.TotalAmount,
                    PaymentMethod = PaymentMethod.BankTransfer,
                    Status = PaymentStatus.Pending,
                    CreatedAt = DateTimeHelper.VietnamNow()
                };
                await _paymentRepo.CreateAsync(payment);
            }

            var accountNumber = _configuration["SePay:AccountNumber"] ?? "YOUR_BANK_ACCOUNT";
            var bankCode = _configuration["SePay:BankCode"] ?? "YOUR_BANK_CODE"; // VD: MB, VCB
            var amount = (int)booking.TotalAmount;
            
            // Format: CS{bookingId}
            var description = $"CS{bookingId}";

            // URL tạo ảnh QR bằng vietqr.io (miễn phí, nhanh chóng)
            var qrUrl = $"https://img.vietqr.io/image/{bankCode}-{accountNumber}-compact.png?amount={amount}&addInfo={description}";

            return qrUrl;
        }

        /// <summary>
        /// Xử lý Webhook từ SePay bắn về khi tiền vô tài khoản
        /// </summary>
        public async Task<bool> ProcessSePayWebhookAsync(SePayWebhookRequest request)
        {
            var content = request.transactionContent.ToUpper();
            var words = content.Split(new[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
            
            int bookingId = 0;
            int topUpUserId = 0;

            foreach (var word in words)
            {
                if (word.StartsWith("CS") && int.TryParse(word.Substring(2), out var bId))
                {
                    bookingId = bId;
                    break;
                }
                if (word.StartsWith("W") && int.TryParse(word.Substring(1), out var uId))
                {
                    topUpUserId = uId;
                    break;
                }
            }

            if (topUpUserId > 0)
            {
                return await ProcessTopUpWebhookAsync(topUpUserId, request);
            }
            if (bookingId > 0)
            {
                return await ProcessBookingWebhookAsync(bookingId, request);
            }

            _logger.LogWarning($"SePay Webhook: Không tìm thấy mã booking hay top-up (CSxxx/Wxxx) trong nội dung '{request.transactionContent}'");
            return false;
        }

        private async Task<bool> ProcessTopUpWebhookAsync(int userId, SePayWebhookRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.referenceCode))
            {
                _logger.LogWarning("SePay Webhook: referenceCode is empty for TopUp");
                return false;
            }

            if (request.amountIn <= 0)
            {
                return false;
            }

            var alreadyProcessed = await _db.LedgerTransactions
                .AnyAsync(t => t.ReferenceType == "TopUp" && t.Memo!.Contains(request.referenceCode));
            if (alreadyProcessed) return true;

            using var transaction = await _db.Database.BeginTransactionAsync();
            try
            {
                var wallet = await _walletRepo.GetByUserIdAsync(userId);
                if (wallet == null)
                {
                    wallet = new Wallet
                    {
                        UserId = userId,
                        WalletType = WalletType.Driver,
                        AvailableBalance = 0,
                        FrozenBalance = 0,
                        CreatedAt = DateTimeHelper.VietnamNow()
                    };
                    await _walletRepo.CreateAsync(wallet);
                }

                wallet.AvailableBalance += request.amountIn;
                await _walletRepo.UpdateAsync(wallet);

                var clearingWallet = await _db.Wallets.FirstAsync(w => w.SystemCode == "CLEARING");
                var ledgerTx = new LedgerTransaction
                {
                    ReferenceType = "TopUp",
                    ReferenceId = wallet.Id,
                    Memo = $"Nạp tiền {request.amountIn:N0} VND qua SePay/VietQR | TxnRef: {request.referenceCode}",
                    CreatedByUserId = userId,
                    CreatedAt = DateTimeHelper.VietnamNow(),
                    Entries = new List<LedgerEntry>
                    {
                        new LedgerEntry { WalletId = clearingWallet.Id, Direction = LedgerDirection.Debit, Amount = request.amountIn, CreatedAt = DateTimeHelper.VietnamNow() },
                        new LedgerEntry { WalletId = wallet.Id, Direction = LedgerDirection.Credit, Amount = request.amountIn, CreatedAt = DateTimeHelper.VietnamNow() }
                    }
                };
                await _walletRepo.AddLedgerTransactionAsync(ledgerTx);

                await transaction.CommitAsync();

                await _notificationService.SendAsync(
                    userId,
                    "Nạp tiền thành công",
                    $"Đã nạp {request.amountIn:N0} VND vào ví qua VietQR. Số dư: {wallet.AvailableBalance:N0} VND.",
                    NotificationType.Payment);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi xử lý SePay TopUp Webhook");
                await transaction.RollbackAsync();
                return false;
            }
        }

        private async Task<bool> ProcessBookingWebhookAsync(int bookingId, SePayWebhookRequest request)
        {
            using var transaction = await _db.Database.BeginTransactionAsync();
            try
            {
                var booking = await _bookingRepo.GetByIdWithDetailsAsync(bookingId);
                if (booking == null) return false;

                var payment = await _paymentRepo.GetByBookingIdAsync(bookingId);
                if (payment == null) return false;

                // Idempotency check: Nếu đã xử lý rồi hoặc đã refund do quá hạn thì bỏ qua để tránh double refund
                if (payment.Status == PaymentStatus.Completed || 
                    payment.Status == PaymentStatus.Refunded || 
                    payment.GatewayTxnRef == request.referenceCode)
                {
                    await transaction.RollbackAsync();
                    return true;
                }

                if (request.amountIn < booking.TotalAmount)
                {
                    _logger.LogWarning($"SePay Webhook: Chuyển thiếu tiền cho Booking #{bookingId}. Cần {booking.TotalAmount}, Nhận {request.amountIn}");
                    // Tùy nghiệp vụ: có thể set Failed hoặc chờ chuyển bù
                    await transaction.RollbackAsync();
                    return false;
                }

                payment.GatewayTxnRef = request.referenceCode; // Lưu mã giao dịch của ngân hàng

                if (booking.Status == BookingStatus.PendingPayment)
                {
                    await CompletePaymentAsync(booking, payment);
                }
                else if (booking.Status == BookingStatus.Expired)
                {
                     var hasConflict = await _bookingRepo.HasOverlappingBookingAsync(
                            booking.SlotId, booking.StartTime, booking.EndTime, booking.Id);

                     if (!hasConflict)
                     {
                         await CompletePaymentAsync(booking, payment);
                     }
                     else
                     {
                         // Nếu mất slot, hoàn tiền vào ví driver
                         await RefundToDriverWalletAsync(booking, payment);
                     }
                }

                await transaction.CommitAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi xử lý SePay Webhook");
                await transaction.RollbackAsync();
                return false;
            }
        }
    }
}
