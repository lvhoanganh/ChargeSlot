using ChargeSlot.Api.Enums;
using ChargeSlot.Api.Models;
using ChargeSlot.Api.Repositories.Interfaces;
using ChargeSlot.Api.Services.Interfaces;

namespace ChargeSlot.Api.Services.Implementation
{
    public class PaymentService : IPaymentService
    {
        private readonly IBookingRepository _bookingRepo;
        private readonly IPaymentRepository _paymentRepo;
        private readonly IChargingSlotRepository _slotRepo;
        private readonly IVnPayService _vnPayService;
        private readonly INotificationService _notificationService;
        private readonly IWalletRepository _walletRepo;
        private readonly ILogger<PaymentService> _logger;

        public PaymentService(
            IBookingRepository bookingRepo,
            IPaymentRepository paymentRepo,
            IChargingSlotRepository slotRepo,
            IVnPayService vnPayService,
            INotificationService notificationService,
            IWalletRepository walletRepo,
            ILogger<PaymentService> logger)
        {
            _bookingRepo = bookingRepo;
            _paymentRepo = paymentRepo;
            _slotRepo = slotRepo;
            _vnPayService = vnPayService;
            _notificationService = notificationService;
            _walletRepo = walletRepo;
            _logger = logger;
        }

        /// <summary>
        /// Step 17: Create payment request → Generate VNPay URL
        /// Step 21: Driver Make payment (redirect to VNPay)
        /// </summary>
        public async Task<string> CreatePaymentUrlAsync(int bookingId, int driverUserId, HttpContext context)
        {
            var booking = await _bookingRepo.GetByIdWithDetailsAsync(bookingId)
                ?? throw new InvalidOperationException("Booking không tồn tại.");

            if (booking.DriverUserId != driverUserId)
                throw new UnauthorizedAccessException("Bạn không có quyền thanh toán booking này.");

            if (booking.Status != BookingStatus.PendingPayment)
                throw new InvalidOperationException("Booking không ở trạng thái chờ thanh toán.");

            // Kiểm tra đã hết hạn chưa
            if (booking.PaymentExpiresAt.HasValue && booking.PaymentExpiresAt.Value <= DateTime.UtcNow)
                throw new InvalidOperationException("Đã hết thời gian thanh toán.");

            // Tạo hoặc lấy Payment record
            var payment = await _paymentRepo.GetByBookingIdAsync(bookingId);
            if (payment == null)
            {
                payment = new Payment
                {
                    BookingId = bookingId,
                    Amount = booking.TotalAmount,
                    PaymentMethod = PaymentMethod.BankTransfer,
                    Status = PaymentStatus.Pending,
                    CreatedAt = DateTime.UtcNow
                };
                await _paymentRepo.CreateAsync(payment);
            }

            var orderInfo = $"Thanh toan dat cho sac #{bookingId}";
            var paymentUrl = _vnPayService.CreatePaymentUrl(bookingId, booking.TotalAmount, orderInfo, context);

            return paymentUrl;
        }

        /// <summary>
        /// Step 22-27: Process payment callback from VNPay
        /// Handles race condition: if booking expired during VNPay processing,
        /// recover booking to Paid status or refund to driver wallet.
        /// </summary>
        public async Task<bool> ProcessVnPayCallbackAsync(IQueryCollection query)
        {
            var (isValid, responseCode, txnRef) = _vnPayService.ValidateCallback(query);

            if (!isValid) return false;

            // Parse bookingId from txnRef (format: {bookingId}_{ticks})
            var bookingIdStr = txnRef.Split('_').FirstOrDefault();
            if (!int.TryParse(bookingIdStr, out var bookingId))
                return false;

            var booking = await _bookingRepo.GetByIdWithDetailsAsync(bookingId);
            if (booking == null) return false;

            var payment = await _paymentRepo.GetByBookingIdAsync(bookingId);
            if (payment == null) return false;

            // Đã xử lý rồi
            if (payment.Status == PaymentStatus.Completed) return true;

            payment.GatewayTxnRef = txnRef;

            if (responseCode == "00") // Thanh toán thành công
            {
                if (booking.Status == BookingStatus.PendingPayment)
                {
                    // ── NORMAL FLOW: Booking vẫn đang chờ thanh toán ──
                    await CompletePaymentAsync(booking, payment);
                    return true;
                }
                else if (booking.Status == BookingStatus.Expired)
                {
                    // ── RECOVERY FLOW: ExpiryJob đã expire booking trong lúc VNPay xử lý ──
                    _logger.LogWarning(
                        "Payment race condition detected: Booking {BookingId} expired but VNPay succeeded. Recovering...",
                        bookingId);

                    // Check: slot có bị booking khác chiếm chưa?
                    var hasConflict = await _bookingRepo.HasOverlappingBookingAsync(
                        booking.SlotId, booking.StartTime, booking.EndTime, booking.Id);

                    if (!hasConflict)
                    {
                        // Slot vẫn trống → recover booking
                        await CompletePaymentAsync(booking, payment);

                        _logger.LogInformation(
                            "Booking {BookingId} recovered from Expired → Paid successfully.", bookingId);
                    }
                    else
                    {
                        // Slot đã bị booking khác chiếm → hoàn tiền vào ví Driver
                        await RefundToDriverWalletAsync(booking, payment);

                        _logger.LogWarning(
                            "Booking {BookingId} cannot be recovered (slot conflict). Refunded {Amount} to driver wallet.",
                            bookingId, booking.TotalAmount);
                    }

                    return true;
                }
                else
                {
                    // Booking ở trạng thái không mong đợi (Cancelled, Completed...)
                    _logger.LogWarning(
                        "VNPay callback for booking {BookingId} in unexpected status {Status}. Skipping.",
                        bookingId, booking.Status);
                    return true;
                }
            }
            else // Thanh toán thất bại
            {
                payment.Status = PaymentStatus.Failed;
                await _paymentRepo.UpdateAsync(payment);

                // Không thay đổi booking status ở đây
                // PaymentExpiryJob sẽ xử lý expire nếu hết hạn

                return false;
            }
        }

        /// <summary>
        /// Flow hoàn tất thanh toán: set Paid + lock slot + notify Driver.
        /// </summary>
        private async Task CompletePaymentAsync(Booking booking, Payment payment)
        {
            payment.Status = PaymentStatus.Completed;
            payment.PaidAt = DateTime.UtcNow;
            await _paymentRepo.UpdateAsync(payment);

            booking.Status = BookingStatus.Paid;
            await _bookingRepo.UpdateAsync(booking);

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
        }

        /// <summary>
        /// Hoàn tiền vào ví Driver khi không thể recover booking (slot conflict).
        /// </summary>
        private async Task RefundToDriverWalletAsync(Booking booking, Payment payment)
        {
            payment.Status = PaymentStatus.Refunded;
            payment.PaidAt = DateTime.UtcNow;
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
                    CreatedAt = DateTime.UtcNow
                };
                await _walletRepo.CreateAsync(driverWallet);
            }

            driverWallet.AvailableBalance += booking.TotalAmount;
            await _walletRepo.UpdateAsync(driverWallet);

            // Ghi ledger
            var ledgerTx = new LedgerTransaction
            {
                ReferenceType = "PaymentRaceRefund",
                ReferenceId = booking.Id,
                Memo = $"Hoàn tiền booking #{booking.Id} do hết hạn thanh toán nhưng VNPay đã trừ tiền - {booking.TotalAmount:N0}đ → Ví Driver",
                CreatedByUserId = null,
                CreatedAt = DateTime.UtcNow,
                Entries = new List<LedgerEntry>
                {
                    new LedgerEntry
                    {
                        WalletId = driverWallet.Id,
                        Direction = LedgerDirection.Credit,
                        Amount = booking.TotalAmount,
                        CreatedAt = DateTime.UtcNow
                    }
                }
            };
            await _walletRepo.AddLedgerTransactionAsync(ledgerTx);

            // Notify Driver
            await _notificationService.SendAsync(
                booking.DriverUserId,
                "Hoàn tiền tự động",
                $"Yêu cầu đặt chỗ tại slot {booking.ChargingSlot?.SlotName} — trạm {booking.ChargingSlot?.ChargingStation?.Name} đã hết hạn nhưng VNPay đã trừ tiền. {booking.TotalAmount:N0}đ đã hoàn vào ví của bạn.",
                NotificationType.Payment);
        }
    }
}
