using ChargeSlot.Api.Enums;
using ChargeSlot.Api.Repositories.Interfaces;
using ChargeSlot.Api.Services.Interfaces;
using ChargeSlot.Api.Helpers;
using Microsoft.EntityFrameworkCore;

namespace ChargeSlot.Api.BackgroundJobs
{
    /// <summary>
    /// BUG-5 FIX: Tự động hoàn thành booking khi hết giờ sạc.
    /// Driver đã trả tiền → slot thuộc về họ trong khung giờ đó.
    /// Sau EndTime + 30 phút mà chưa check-in hoặc chưa kết thúc → auto-complete.
    /// Giải phóng slot, settle tiền bình thường cho Owner.
    /// Chạy mỗi 60 giây.
    /// </summary>
    public class NoShowJob : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<NoShowJob> _logger;

        public NoShowJob(IServiceProvider serviceProvider, ILogger<NoShowJob> logger)
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
                    var db = scope.ServiceProvider.GetRequiredService<Data.ChargeSlotDbContext>();
                    var bookingRepo = scope.ServiceProvider.GetRequiredService<IBookingRepository>();
                    var slotRepo = scope.ServiceProvider.GetRequiredService<IChargingSlotRepository>();
                    var walletRepo = scope.ServiceProvider.GetRequiredService<IWalletRepository>();
                    var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

                    var now = DateTimeHelper.VietnamNow();
                    var cutoff = now.AddMinutes(-30); // Grace period 30 phút sau EndTime

                    // Tìm booking Paid mà đã quá EndTime + 30 phút nhưng chưa check-in
                    // Driver đã trả tiền nên KHÔNG phạt — auto-complete bình thường
                    var overdueBookings = await db.Bookings
                        .Where(b => b.Status == BookingStatus.Paid && b.EndTime < cutoff)
                        .Include(b => b.ChargingSlot).ThenInclude(s => s.ChargingStation)
                        .Include(b => b.Driver).ThenInclude(d => d.User)
                        .ToListAsync();

                    foreach (var booking in overdueBookings)
                    {
                        // Auto-complete — driver đã trả tiền, slot đã được giữ trong khung giờ
                        booking.Status = BookingStatus.Completed;
                        booking.UpdatedAt = now;
                        await bookingRepo.UpdateAsync(booking);

                        // Giải phóng slot (đã hết giờ booking)
                        if (booking.ChargingSlot != null && booking.ChargingSlot.Status == SlotStatus.Booked)
                        {
                            booking.ChargingSlot.Status = SlotStatus.Active;
                            slotRepo.Update(booking.ChargingSlot);
                            await slotRepo.SaveChangesAsync();
                        }

                        // Settle tiền cho Owner bình thường (giống flow hoàn thành)
                        var ownerUserId = booking.ChargingSlot?.ChargingStation?.OwnerUserId;
                        if (ownerUserId.HasValue)
                        {
                            // Tính toán: Owner nhận = TotalAmount - VAT(8%) - PlatformFee(5%)
                            var grossAmount = booking.TotalAmount;
                            var vatAmount = Math.Round(grossAmount * 0.08m, 0);
                            var platformFee = Math.Round(grossAmount * 0.05m, 0);
                            var ownerNet = grossAmount - vatAmount - platformFee;

                            var escrowWallet = await db.Wallets.FirstAsync(w => w.SystemCode == "ESCROW");
                            var platformWallet = await db.Wallets.FirstAsync(w => w.SystemCode == "PLATFORM_REVENUE");
                            var ownerWallet = await db.Wallets.FirstOrDefaultAsync(w => w.UserId == ownerUserId.Value);

                            if (ownerWallet != null)
                            {
                                // ESCROW → Owner (net)
                                escrowWallet.AvailableBalance -= ownerNet;
                                ownerWallet.AvailableBalance += ownerNet;

                                // ESCROW → Platform (fee)
                                escrowWallet.AvailableBalance -= platformFee;
                                platformWallet.AvailableBalance += platformFee;

                                db.Set<Models.LedgerTransaction>().Add(new Models.LedgerTransaction
                                {
                                    ReferenceType = "AutoComplete",
                                    ReferenceId = booking.Id,
                                    Memo = $"Auto-complete booking #{booking.Id} — Owner nhận {ownerNet:N0}đ, phí nền tảng {platformFee:N0}đ",
                                    CreatedAt = now,
                                    Entries = new List<Models.LedgerEntry>
                                    {
                                        new() { WalletId = escrowWallet.Id, Direction = LedgerDirection.Debit, Amount = ownerNet + platformFee, CreatedAt = now },
                                        new() { WalletId = ownerWallet.Id, Direction = LedgerDirection.Credit, Amount = ownerNet, CreatedAt = now },
                                        new() { WalletId = platformWallet.Id, Direction = LedgerDirection.Credit, Amount = platformFee, CreatedAt = now }
                                    }
                                });
                                await db.SaveChangesAsync();
                            }

                            await notificationService.SendAsync(
                                ownerUserId.Value,
                                "Booking đã hoàn thành tự động",
                                $"Booking tại slot {booking.ChargingSlot?.SlotName} — trạm {booking.ChargingSlot?.ChargingStation?.Name} đã hoàn thành. {ownerNet:N0}đ đã chuyển vào ví của bạn.",
                                NotificationType.Payment);
                        }

                        // Notify Driver
                        await notificationService.SendAsync(
                            booking.DriverUserId,
                            "Booking đã hoàn thành",
                            $"Booking tại slot {booking.ChargingSlot?.SlotName} — trạm {booking.ChargingSlot?.ChargingStation?.Name} ({booking.StartTime:HH:mm} - {booking.EndTime:HH:mm dd/MM}) đã tự động hoàn thành. Slot đã được giải phóng.",
                            NotificationType.Booking);

                        _logger.LogInformation("Booking {BookingId} auto-completed after end time.", booking.Id);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in NoShowJob");
                }

                await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
            }
        }
    }
}
