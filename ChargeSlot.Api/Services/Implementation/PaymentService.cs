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

        public PaymentService(
            IBookingRepository bookingRepo,
            IPaymentRepository paymentRepo,
            IChargingSlotRepository slotRepo,
            IVnPayService vnPayService,
            INotificationService notificationService)
        {
            _bookingRepo = bookingRepo;
            _paymentRepo = paymentRepo;
            _slotRepo = slotRepo;
            _vnPayService = vnPayService;
            _notificationService = notificationService;
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
        /// →   Yes (confirmed): Set Paid + Lock slot (Step 25, 27)
        /// → No (failed/expired): Set Expired + Release slot (Step 24, 26)
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
            if (payment.Status != PaymentStatus.Pending) return true;

            payment.GatewayTxnRef = txnRef;

            if (responseCode == "00") // Thanh toán thành công
            {
                // Step 25: Set booking status = Paid
                payment.Status = PaymentStatus.Completed;
                payment.PaidAt = DateTime.UtcNow;
                await _paymentRepo.UpdateAsync(payment);

                booking.Status = BookingStatus.Paid;
                await _bookingRepo.UpdateAsync(booking);

                // Step 27: Lock charging slot
                var slot = await _slotRepo.GetByIdAsync(booking.SlotId, tracking: true);
                if (slot != null)
                {
                    slot.Status = SlotStatus.Booked;
                    _slotRepo.Update(slot);
                    await _slotRepo.SaveChangesAsync();
                }

                // Step 28: Notify Driver - Receive booking confirmation
                await _notificationService.SendAsync(
                    booking.DriverUserId,
                    "Thanh toán thành công",
                    $"Đặt chỗ #{bookingId} đã được thanh toán. Slot đã được giữ cho bạn.",
                    NotificationType.Payment);

                return true;
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
    }
}
