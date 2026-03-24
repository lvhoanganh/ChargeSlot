using ChargeSlot.Api.Enums;
using ChargeSlot.Api.Repositories.Interfaces;
using ChargeSlot.Api.Services.Interfaces;

namespace ChargeSlot.Api.BackgroundJobs
{
    /// <summary>
    /// Step 23-26: Payment confirmed within deadline?
    /// No → Set booking status = Expired → Release slot → END
    /// Chạy mỗi 30 giây check các booking hết hạn thanh toán.
    /// Safe-check 1: nếu payment đã Completed (VNPay callback đã xử lý) → recover.
    /// Safe-check 2: nếu VNPay QueryDR xác nhận đã thanh toán → recover (callback bị mất/trễ).
    /// </summary>
    public class PaymentExpiryJob : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<PaymentExpiryJob> _logger;

        public PaymentExpiryJob(IServiceProvider serviceProvider, ILogger<PaymentExpiryJob> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var bookingRepo = scope.ServiceProvider.GetRequiredService<IBookingRepository>();
                    var slotRepo = scope.ServiceProvider.GetRequiredService<IChargingSlotRepository>();
                    var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
                    var vnPayService = scope.ServiceProvider.GetRequiredService<IVnPayService>();

                    // Lấy các booking hết hạn thanh toán (đã include Payment)
                    var expiredBookings = await bookingRepo.GetExpiredPendingPaymentsAsync();

                    foreach (var booking in expiredBookings)
                    {
                        // ── SAFE-CHECK 1: Payment.Status đã Completed (callback đến kịp) ──
                        if (booking.Payment?.Status == PaymentStatus.Completed)
                        {
                            _logger.LogInformation(
                                "Booking {BookingId} has payment completed. Recovering to Paid instead of expiring.",
                                booking.Id);

                            booking.Status = BookingStatus.Paid;
                            await bookingRepo.UpdateAsync(booking);
                            await LockSlotIfNeeded(slotRepo, booking);
                            continue;
                        }

                        // ── SAFE-CHECK 2: Gọi VNPay QueryDR nếu có GatewayTxnRef ──
                        if (booking.Payment != null &&
                            !string.IsNullOrEmpty(booking.Payment.GatewayTxnRef) &&
                            booking.Payment.Status == PaymentStatus.Pending)
                        {
                            var (isPaid, queryResponseCode) = await vnPayService.QueryTransactionAsync(
                                booking.Payment.GatewayTxnRef,
                                booking.Payment.Amount,
                                booking.Payment.CreatedAt);

                            if (isPaid)
                            {
                                _logger.LogWarning(
                                    "VNPay QueryDR confirms Booking {BookingId} was PAID (callback missed). Recovering...",
                                    booking.Id);

                                booking.Payment.Status = PaymentStatus.Completed;
                                booking.Payment.PaidAt = Helpers.DateTimeHelper.VietnamNow();
                                booking.Status = BookingStatus.Paid;
                                await bookingRepo.UpdateAsync(booking);
                                await LockSlotIfNeeded(slotRepo, booking);

                                await notificationService.SendAsync(
                                    booking.DriverUserId,
                                    "Thanh toán đã xác nhận",
                                    $"Thanh toán {booking.TotalAmount:N0}đ cho slot {booking.ChargingSlot?.SlotName} đã được xác nhận thành công.",
                                    NotificationType.Payment);
                                continue;
                            }

                            if (queryResponseCode == "QUERY_ERROR")
                            {
                                // Query thất bại → KHÔNG expire, đợi cycle sau
                                _logger.LogWarning(
                                    "VNPay QueryDR failed for Booking {BookingId}. Skipping expire, will retry next cycle.",
                                    booking.Id);
                                continue;
                            }
                        }

                        // ── EXPIRE: Chắc chắn chưa thanh toán → hủy ──
                        booking.Status = BookingStatus.Expired;
                        await bookingRepo.UpdateAsync(booking);

                        // Release slot
                        if (booking.ChargingSlot != null && booking.ChargingSlot.Status == SlotStatus.Booked)
                        {
                            booking.ChargingSlot.Status = SlotStatus.Active;
                            slotRepo.Update(booking.ChargingSlot);
                            await slotRepo.SaveChangesAsync();
                        }

                        // Notify Driver
                        await notificationService.SendAsync(
                            booking.DriverUserId,
                            "Đặt chỗ đã hết hạn",
                            $"Yêu cầu đặt chỗ tại slot {booking.ChargingSlot?.SlotName} — trạm {booking.ChargingSlot?.ChargingStation?.Name} đã bị hủy do không thanh toán kịp thời hạn.",
                            NotificationType.Booking);

                        // Notify Owner
                        var ownerUserId = booking.ChargingSlot?.ChargingStation?.OwnerUserId;
                        if (ownerUserId.HasValue)
                        {
                            await notificationService.SendAsync(
                                ownerUserId.Value,
                                "Đặt chỗ bị hủy",
                                $"Slot {booking.ChargingSlot?.SlotName} — trạm {booking.ChargingSlot?.ChargingStation?.Name} ({booking.StartTime:HH:mm} - {booking.EndTime:HH:mm dd/MM}): Khách không thanh toán kịp, slot đã được mở lại.",
                                NotificationType.Booking);
                        }

                        _logger.LogInformation("Booking {BookingId} expired due to payment timeout.", booking.Id);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in PaymentExpiryJob");
                }

                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }

        private static async Task LockSlotIfNeeded(IChargingSlotRepository slotRepo, Models.Booking booking)
        {
            if (booking.ChargingSlot != null && booking.ChargingSlot.Status != SlotStatus.Booked)
            {
                booking.ChargingSlot.Status = SlotStatus.Booked;
                slotRepo.Update(booking.ChargingSlot);
                await slotRepo.SaveChangesAsync();
            }
        }
    }
}
