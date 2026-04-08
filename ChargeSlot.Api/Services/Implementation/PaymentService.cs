using ChargeSlot.Api.Helpers;
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
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILedgerTransactionRepository _ledgerRepo;
        private readonly IExtraServiceRepository _extraServiceRepo;
        private readonly ILogger<PaymentService> _logger;
        private readonly IConfiguration _configuration;
        private readonly ISystemConfigService _configService;

        public PaymentService(
            IBookingRepository bookingRepo,
            IPaymentRepository paymentRepo,
            IChargingSlotRepository slotRepo,
            INotificationService notificationService,
            IWalletRepository walletRepo,
            IUnitOfWork unitOfWork,
            ILedgerTransactionRepository ledgerRepo,
            IExtraServiceRepository extraServiceRepo,
            ILogger<PaymentService> logger,
            IConfiguration configuration,
            ISystemConfigService configService)
        {
            _bookingRepo = bookingRepo;
            _paymentRepo = paymentRepo;
            _slotRepo = slotRepo;
            _notificationService = notificationService;
            _walletRepo = walletRepo;
            _unitOfWork = unitOfWork;
            _ledgerRepo = ledgerRepo;
            _extraServiceRepo = extraServiceRepo;
            _logger = logger;
            _configuration = configuration;
            _configService = configService;
        }

        /// <summary>
        /// Flow hoàn tất thanh toán: set Paid + lock slot + notify Driver.
        /// </summary>
        private async Task CompletePaymentAsync(Booking booking, Payment payment)
        {
            payment.Status = PaymentStatus.Completed;
            payment.PaidAt = DateTimeHelper.VietnamNow();
            _paymentRepo.Update(payment);
            await _unitOfWork.CompleteAsync();

            booking.Status = BookingStatus.Paid;
            // Snapshot CheckinDeadlineAt: StartTime + config check-in window
            if (booking.CheckinDeadlineAt == null)
            {
                var cfgs = await _configService.GetCurrentConfigsAsync();
                booking.CheckinDeadlineAt = booking.StartTime.AddMinutes(cfgs.CheckIn_Window_Minutes);
            }
            _bookingRepo.Update(booking);
            await _unitOfWork.CompleteAsync();

            // Cộng tiền vào ESCROW wallet (Atomic)
            var escrowWallet = await _walletRepo.GetBySystemCodeAsync("ESCROW");
            var clearingWallet = await _walletRepo.GetBySystemCodeAsync("CLEARING");
            await _unitOfWork.ExecuteSqlRawSafeAsync(
                "UPDATE Wallet SET AvailableBalance = AvailableBalance + {0} WHERE Id = {1}",
                booking.TotalAmount, escrowWallet!.Id);
            await _unitOfWork.CompleteAsync();

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
            _walletRepo.AddLedgerTransaction(ledgerTx);
            await _unitOfWork.CompleteAsync();

            // Trừ stock cho ExtraServices (nếu có)
            if (booking.BookingExtraServices != null && booking.BookingExtraServices.Count > 0)
            {
                foreach (var bes in booking.BookingExtraServices)
                {
                    var svc = await _extraServiceRepo.GetByIdAsync(bes.ServiceId);
                    if (svc != null && svc.TotalStock.HasValue)
                    {
                        if (svc.TotalStock.Value < bes.Quantity)
                            throw new InvalidOperationException(
                                $"Dịch vụ '{svc.ServiceName}' đã hết hàng.");
                        svc.TotalStock -= bes.Quantity;
                        _extraServiceRepo.Update(svc);
                    }
                }
                await _unitOfWork.CompleteAsync();
            }

            // Lock charging slot
            var slot = await _slotRepo.GetByIdAsync(booking.SlotId, tracking: true);
            if (slot != null)
            {
                slot.Status = SlotStatus.Booked;
                _slotRepo.Update(slot);
                await _unitOfWork.CompleteAsync();
            }
            // Notifications được gửi từ caller (ngoài transaction)
        }

        /// <summary>
        /// Hoàn tiền vào ví Driver khi không thể recover booking (slot conflict).
        /// </summary>
        private async Task RefundToDriverWalletAsync(Booking booking, Payment payment)
        {
            payment.Status = PaymentStatus.Refunded;
            payment.PaidAt = DateTimeHelper.VietnamNow();
            _paymentRepo.Update(payment);
            await _unitOfWork.CompleteAsync();

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
                _walletRepo.Add(driverWallet);
            await _unitOfWork.CompleteAsync();
            }

            driverWallet.AvailableBalance += booking.TotalAmount;
            _walletRepo.Update(driverWallet);
            await _unitOfWork.CompleteAsync();

            // Ghi ledger double-entry: DEBIT từ CLEARING (VNPay refund), CREDIT vào ví Driver
            var clearingWallet = await _walletRepo.GetBySystemCodeAsync("CLEARING");
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
            _ledgerRepo.Add(ledgerTx);
            await _unitOfWork.CompleteAsync();
            // Notifications được gửi từ caller (ngoài transaction)
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
                _paymentRepo.Add(payment);
            await _unitOfWork.CompleteAsync();
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
        /// Xử lý Webhook từ SePay bắn về khi tiền vô tài khoản.
        /// Đảm bảo: Driver KHÔNG BAO GIỜ mất tiền dù chuyển sai nội dung hoặc thiếu tiền.
        /// </summary>
        public async Task<bool> ProcessSePayWebhookAsync(SePayWebhookRequest request)
        {
            // ── Chống trùng lặp theo khuyến nghị SePay (dùng id giao dịch SePay) ──
            var sePayTxnId = request.id.ToString();
            var alreadyProcessed = await _ledgerRepo.HasTransactionWithMemoAsync($"SePay#{sePayTxnId}");
            if (alreadyProcessed)
            {
                _logger.LogInformation($"SePay Webhook: Giao dịch SePay#{sePayTxnId} đã xử lý trước đó, bỏ qua.");
                return true;
            }

            // ── Lấy nội dung chuyển khoản (SePay gửi trong trường "content") ──
            var rawContent = (request.content ?? request.description ?? "").ToUpper();
            var amount = request.transferAmount;

            if (amount <= 0)
            {
                _logger.LogWarning($"SePay Webhook: transferAmount = {amount}, bỏ qua.");
                return true;
            }

            // ── Tìm mã CS{bookingId} hoặc W{userId} trong nội dung ──
            var bookingMatch = System.Text.RegularExpressions.Regex.Match(rawContent, @"CS(\d+)");
            var topUpMatch = System.Text.RegularExpressions.Regex.Match(rawContent, @"(?<![A-Z])W(\d+)");

            if (topUpMatch.Success && int.TryParse(topUpMatch.Groups[1].Value, out var topUpUserId))
            {
                return await ProcessTopUpWebhookAsync(topUpUserId, request);
            }
            if (bookingMatch.Success && int.TryParse(bookingMatch.Groups[1].Value, out var bookingId))
            {
                return await ProcessBookingWebhookAsync(bookingId, request);
            }

            // ── Không nhận diện được mã → Nạp vào ví CLEARING, log cảnh báo ──
            _logger.LogWarning($"SePay Webhook: Không tìm thấy mã CSxxx/Wxxx trong nội dung '{rawContent}'. Tiền {amount:N0} VND ghi nhận vào CLEARING. SePay#{sePayTxnId}");
            return true;
        }

        /// <summary>
        /// Xử lý nạp tiền vào ví Driver (nội dung chuyển khoản chứa W{userId})
        /// </summary>
        private async Task<bool> ProcessTopUpWebhookAsync(int userId, SePayWebhookRequest request)
        {
            var amount = request.transferAmount;
            var sePayTxnId = request.id.ToString();

            using var transaction = await _unitOfWork.BeginTransactionAsync();
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
                    _walletRepo.Add(wallet);
            await _unitOfWork.CompleteAsync();
                }

                wallet.AvailableBalance += amount;
                _walletRepo.Update(wallet);
            await _unitOfWork.CompleteAsync();

                var clearingWallet = await _walletRepo.GetBySystemCodeAsync("CLEARING");
                var ledgerTx = new LedgerTransaction
                {
                    ReferenceType = "TopUp",
                    ReferenceId = wallet.Id,
                    Memo = $"Nạp tiền {amount:N0} VND qua SePay/VietQR | SePay#{sePayTxnId} | Ref: {request.referenceCode}",
                    CreatedByUserId = userId,
                    CreatedAt = DateTimeHelper.VietnamNow(),
                    Entries = new List<LedgerEntry>
                    {
                        new LedgerEntry { WalletId = clearingWallet.Id, Direction = LedgerDirection.Debit, Amount = amount, CreatedAt = DateTimeHelper.VietnamNow() },
                        new LedgerEntry { WalletId = wallet.Id, Direction = LedgerDirection.Credit, Amount = amount, CreatedAt = DateTimeHelper.VietnamNow() }
                    }
                };
                _ledgerRepo.Add(ledgerTx);
                await _unitOfWork.CompleteAsync();

                await transaction.CommitAsync();

                await _notificationService.SendAsync(
                    userId,
                    "Nạp tiền thành công",
                    $"Đã nạp {amount:N0} VND vào ví qua VietQR. Số dư: {wallet.AvailableBalance:N0} VND.",
                    NotificationType.Payment);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Lỗi khi xử lý SePay TopUp Webhook SePay#{sePayTxnId}");
                await transaction.RollbackAsync();
                return true; // Vẫn trả true để SePay không retry
            }
        }

        /// <summary>
        /// Xử lý thanh toán booking (nội dung chuyển khoản chứa CS{bookingId}).
        /// Nếu booking không tồn tại hoặc chuyển thiếu tiền → hoàn vào ví Driver.
        /// </summary>
        private async Task<bool> ProcessBookingWebhookAsync(int bookingId, SePayWebhookRequest request)
        {
            var amount = request.transferAmount;
            var sePayTxnId = request.id.ToString();

            // Track which path was taken for notifications after commit
            string? notifyPath = null;

            using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                var booking = await _bookingRepo.GetByIdWithDetailsAsync(bookingId);
                if (booking == null)
                {
                    // Booking không tồn tại → Nạp tiền vào ví CLEARING (Admin xử lý thủ công)
                    _logger.LogWarning($"SePay: Booking {bookingId} không tồn tại. Tiền {amount:N0} VND ghi nhận CLEARING. SePay#{sePayTxnId}");
                    await transaction.RollbackAsync();
                    return true;
                }

                var payment = await _paymentRepo.GetByBookingIdAsync(bookingId);
                if (payment == null)
                {
                    // Có booking nhưng chưa tạo payment → Nạp vào ví Driver
                    await DepositToDriverWalletAsync(booking.DriverUserId, amount, sePayTxnId,
                        $"Chuyển khoản cho Booking #{bookingId} nhưng chưa có yêu cầu thanh toán. Tiền đã nạp vào ví.");
                    await transaction.CommitAsync();
                    return true;
                }

                // ── Idempotency: nếu cùng mã giao dịch SePay thì bỏ qua (webhook gọi lại) ──
                if (payment.GatewayTxnRef == request.referenceCode)
                {
                    await transaction.RollbackAsync();
                    return true;
                }

                // ── Payment đã hoàn tất nhưng Driver chuyển thêm lần nữa → Hoàn vào ví ──
                if (payment.Status == PaymentStatus.Completed || 
                    payment.Status == PaymentStatus.Refunded)
                {
                    _logger.LogWarning($"SePay: Booking #{bookingId} đã thanh toán/hoàn tiền, nhưng nhận thêm {amount:N0} VND (SePay#{sePayTxnId}). Hoàn vào ví Driver.");
                    await DepositToDriverWalletAsync(booking.DriverUserId, amount, sePayTxnId,
                        $"Bạn đã chuyển khoản {amount:N0}đ cho Booking #{bookingId} nhưng đơn này đã được thanh toán trước đó. Tiền đã hoàn vào ví của bạn.");
                    await transaction.CommitAsync();
                    return true;
                }

                // ── Chuyển thiếu tiền → Nạp vào ví Driver thay vì bỏ qua ──
                if (amount < booking.TotalAmount)
                {
                    _logger.LogWarning($"SePay: Booking #{bookingId} cần {booking.TotalAmount:N0}, nhận {amount:N0}. Hoàn vào ví Driver.");
                    await DepositToDriverWalletAsync(booking.DriverUserId, amount, sePayTxnId,
                        $"Chuyển khoản cho Booking #{bookingId} thiếu tiền (cần {booking.TotalAmount:N0}đ, nhận {amount:N0}đ). Tiền đã nạp vào ví, vui lòng thanh toán lại bằng ví.");
                    await transaction.CommitAsync();
                    return true;
                }

                payment.GatewayTxnRef = request.referenceCode;

                if (booking.Status == BookingStatus.PendingPayment)
                {
                    await CompletePaymentAsync(booking, payment);
                    notifyPath = "completed";
                }
                else if (booking.Status == BookingStatus.Expired)
                {
                    var hasConflict = await _bookingRepo.HasOverlappingBookingAsync(
                        booking.SlotId, booking.StartTime, booking.EndTime, booking.Id);

                    if (!hasConflict)
                    {
                        await CompletePaymentAsync(booking, payment);
                        notifyPath = "completed";
                    }
                    else
                    {
                        await RefundToDriverWalletAsync(booking, payment);
                        notifyPath = "refunded";
                    }
                }
                else
                {
                    // Trạng thái booking không hợp lệ để thanh toán → Hoàn vào ví
                    await DepositToDriverWalletAsync(booking.DriverUserId, amount, sePayTxnId,
                        $"Chuyển khoản cho Booking #{bookingId} nhưng booking ở trạng thái {booking.Status}. Tiền đã nạp vào ví.");
                }

                await transaction.CommitAsync();

                // Notifications (ngoài transaction)
                if (notifyPath == "completed")
                {
                    await _notificationService.SendAsync(
                        booking.DriverUserId,
                        "Thanh toán thành công",
                        $"Thanh toán {booking.TotalAmount:N0}đ cho slot {booking.ChargingSlot?.SlotName} — trạm {booking.ChargingSlot?.ChargingStation?.Name} ({booking.StartTime:HH:mm} - {booking.EndTime:HH:mm dd/MM}) thành công. Slot đã được giữ cho bạn.",
                        NotificationType.Payment);

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
                else if (notifyPath == "refunded")
                {
                    await _notificationService.SendAsync(
                        booking.DriverUserId,
                        "Hoàn tiền tự động",
                        $"Yêu cầu đặt chỗ tại slot {booking.ChargingSlot?.SlotName} — trạm {booking.ChargingSlot?.ChargingStation?.Name} đã hết hạn nhưng bạn đã chuyển khoản thành công. {booking.TotalAmount:N0}đ đã hoàn vào ví của bạn.",
                        NotificationType.Payment);
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Lỗi khi xử lý SePay Booking Webhook SePay#{sePayTxnId}");
                await transaction.RollbackAsync();
                return true;
            }
        }

        /// <summary>
        /// Nạp tiền vào ví Driver khi không thể xử lý booking (thiếu tiền, booking không tồn tại, v.v.)
        /// Đảm bảo Driver KHÔNG BAO GIỜ mất tiền.
        /// </summary>
        private async Task DepositToDriverWalletAsync(int driverUserId, decimal amount, string sePayTxnId, string notifyMessage)
        {
            var wallet = await _walletRepo.GetByUserIdAsync(driverUserId);
            if (wallet == null)
            {
                wallet = new Wallet
                {
                    UserId = driverUserId,
                    WalletType = WalletType.Driver,
                    AvailableBalance = 0,
                    FrozenBalance = 0,
                    CreatedAt = DateTimeHelper.VietnamNow()
                };
                _walletRepo.Add(wallet);
            await _unitOfWork.CompleteAsync();
            }

            wallet.AvailableBalance += amount;
            _walletRepo.Update(wallet);
            await _unitOfWork.CompleteAsync();

            var clearingWallet = await _walletRepo.GetBySystemCodeAsync("CLEARING");
            var ledgerTx = new LedgerTransaction
            {
                ReferenceType = "BookingFallbackDeposit",
                ReferenceId = wallet.Id,
                Memo = $"Hoàn tiền {amount:N0} VND vào ví Driver (fallback) | SePay#{sePayTxnId}",
                CreatedByUserId = driverUserId,
                CreatedAt = DateTimeHelper.VietnamNow(),
                Entries = new List<LedgerEntry>
                {
                    new LedgerEntry { WalletId = clearingWallet.Id, Direction = LedgerDirection.Debit, Amount = amount, CreatedAt = DateTimeHelper.VietnamNow() },
                    new LedgerEntry { WalletId = wallet.Id, Direction = LedgerDirection.Credit, Amount = amount, CreatedAt = DateTimeHelper.VietnamNow() }
                }
            };
            _ledgerRepo.Add(ledgerTx);
            await _unitOfWork.CompleteAsync();

            await _notificationService.SendAsync(
                driverUserId,
                "Tiền đã nạp vào ví",
                notifyMessage,
                NotificationType.Payment);
        }
    }
}





