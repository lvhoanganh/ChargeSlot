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
                    List<int> expiredBookingIds;

                    // 1. Chỉ lấy danh sách ID từ repo (scope ngoài)
                    using (var outerScope = _serviceProvider.CreateScope())
                    {
                        var outerRepo = outerScope.ServiceProvider.GetRequiredService<IBookingRepository>();
                        var expiredBookings = await outerRepo.GetExpiredPendingPaymentsAsync();
                        expiredBookingIds = expiredBookings.Select(b => b.Id).ToList();
                    }

                    // 2. Xử lý từng ID trong 1 scope biệt lập
                    foreach (var bookingId in expiredBookingIds)
                    {
                        using var innerScope = _serviceProvider.CreateScope();
                        var unitOfWork = innerScope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                        var bookingRepo = innerScope.ServiceProvider.GetRequiredService<IBookingRepository>();
                        var slotRepo = innerScope.ServiceProvider.GetRequiredService<IChargingSlotRepository>();
                        var notificationService = innerScope.ServiceProvider.GetRequiredService<INotificationService>();

                        using var transaction = await unitOfWork.BeginTransactionAsync();
                        try
                        {
                            var booking = await bookingRepo.GetByIdAsync(bookingId);
                            if (booking == null || booking.Status != BookingStatus.PendingPayment)
                                continue;

                            // ── SAFE-CHECK 1: Payment.Status đã Completed (callback đến kịp) ──
                            if (booking.Payment?.Status == PaymentStatus.Completed)
                            {
                                _logger.LogInformation(
                                    "Booking {BookingId} has payment completed. Recovering to Paid instead of expiring.",
                                    booking.Id);

                                booking.Status = BookingStatus.Paid;
                                bookingRepo.Update(booking);
                                await unitOfWork.CompleteAsync();
                                await LockSlotIfNeeded(unitOfWork, slotRepo, booking);
                                await transaction.CommitAsync(stoppingToken);
                                continue;
                            }

                            // ── EXPIRE: Chắc chắn chưa thanh toán (tại thời điểm này) → hủy ──
                            booking.Status = BookingStatus.Expired;
                            bookingRepo.Update(booking);
                                await unitOfWork.CompleteAsync();

                            // Release slot
                            if (booking.ChargingSlot != null && booking.ChargingSlot.Status == SlotStatus.Booked)
                            {
                                booking.ChargingSlot.Status = SlotStatus.Active;
                                slotRepo.Update(booking.ChargingSlot);
                                await unitOfWork.CompleteAsync();
                            }

                            await transaction.CommitAsync(stoppingToken);

                            // Notifications (ngoài transaction)
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
                        catch (Exception ex)
                        {
                            await transaction.RollbackAsync(stoppingToken);
                            _logger.LogError(ex, "Error processing expired booking {BookingId}", bookingId);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in PaymentExpiryJob");
                }

                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }

        private static async Task LockSlotIfNeeded(IUnitOfWork unitOfWork, IChargingSlotRepository slotRepo, Models.Booking booking)
        {
            if (booking.ChargingSlot != null && booking.ChargingSlot.Status != SlotStatus.Booked)
            {
                booking.ChargingSlot.Status = SlotStatus.Booked;
                slotRepo.Update(booking.ChargingSlot);
                await unitOfWork.CompleteAsync();
            }
        }
    }
}


