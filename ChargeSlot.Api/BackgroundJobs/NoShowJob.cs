using ChargeSlot.Api.Enums;
using ChargeSlot.Api.Repositories.Interfaces;
using ChargeSlot.Api.Services.Interfaces;
using ChargeSlot.Api.Helpers;
using Microsoft.EntityFrameworkCore;

namespace ChargeSlot.Api.BackgroundJobs
{
    /// <summary>
    /// Xử lý 3 trường hợp overdue:
    /// 1. WaitingOwner > 30 phút → auto-expire (Owner không phản hồi)
    /// 2. Paid quá EndTime + 30 phút → auto-complete + settle (No-Show)
    /// 3. CheckedIn quá EndTime + 30 phút → auto-stop + invoice + settle (Owner quên dừng)
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
                    await ProcessWaitingOwnerTimeoutAsync();
                    await ProcessPaidNoShowAsync();
                    await ProcessCheckedInOvertimeAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in NoShowJob");
                }

                await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
            }
        }

        // ═══════════════════════════════════════════════════════
        // 1. WaitingOwner > 30 phút → Auto-expire
        // ═══════════════════════════════════════════════════════
        private async Task ProcessWaitingOwnerTimeoutAsync()
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<Data.ChargeSlotDbContext>();
            var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
            var configService = scope.ServiceProvider.GetRequiredService<ISystemConfigService>();

            var configs = await configService.GetCurrentConfigsAsync();
            var graceTime = configs.NoShow_Grace_Minutes;

            var now = DateTimeHelper.VietnamNow();
            var cutoff = now.AddMinutes(-graceTime);

            var staleBookings = await db.Bookings
                .Where(b => b.Status == BookingStatus.WaitingOwner && b.CreatedAt <= cutoff)
                .Include(b => b.ChargingSlot).ThenInclude(s => s.ChargingStation)
                .ToListAsync();

            foreach (var booking in staleBookings)
            {
                booking.Status = BookingStatus.Expired;
                booking.CancelReason = "Không có phản hồi từ chủ trạm trong 30 phút.";
                booking.UpdatedAt = now;

                // Release slot nếu đang bị giữ
                if (booking.ChargingSlot != null && booking.ChargingSlot.Status == SlotStatus.Booked)
                {
                    booking.ChargingSlot.Status = SlotStatus.Active;
                    booking.ChargingSlot.UpdatedAt = now;
                }

                await db.SaveChangesAsync();

                await notificationService.SendAsync(
                    booking.DriverUserId,
                    "Yêu cầu đặt chỗ đã hết hạn",
                    $"Yêu cầu đặt slot {booking.ChargingSlot?.SlotName} — trạm {booking.ChargingSlot?.ChargingStation?.Name} đã hết hạn do chủ trạm không phản hồi trong 30 phút.",
                    NotificationType.Booking);

                var ownerUserId = booking.ChargingSlot?.ChargingStation?.OwnerUserId;
                if (ownerUserId.HasValue)
                {
                    await notificationService.SendAsync(
                        ownerUserId.Value,
                        "Yêu cầu đặt chỗ đã hết hạn",
                        $"Yêu cầu đặt slot {booking.ChargingSlot?.SlotName} ({booking.StartTime:HH:mm} - {booking.EndTime:HH:mm dd/MM}) đã tự động hủy do bạn không phản hồi trong 30 phút.",
                        NotificationType.Booking);
                }

                _logger.LogInformation("Booking {BookingId} auto-expired: WaitingOwner timeout 30 min.", booking.Id);
            }
        }

        // ═══════════════════════════════════════════════════════
        // 2. Paid quá EndTime + 30 phút → Auto-complete + settle
        // ═══════════════════════════════════════════════════════
        private async Task ProcessPaidNoShowAsync()
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<Data.ChargeSlotDbContext>();
            var bookingRepo = scope.ServiceProvider.GetRequiredService<IBookingRepository>();
            var slotRepo = scope.ServiceProvider.GetRequiredService<IChargingSlotRepository>();
            var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
            var configService = scope.ServiceProvider.GetRequiredService<ISystemConfigService>();

            var configs = await configService.GetCurrentConfigsAsync();
            var graceTime = configs.NoShow_Grace_Minutes;

            var now = DateTimeHelper.VietnamNow();
            var cutoff = now.AddMinutes(-graceTime);

            var overdueBookings = await db.Bookings
                .Where(b => b.Status == BookingStatus.Paid && b.EndTime < cutoff && b.ManualCheckinRequestedAt == null)
                .Include(b => b.ChargingSlot).ThenInclude(s => s.ChargingStation)
                .Include(b => b.Driver).ThenInclude(d => d.User)
                .ToListAsync();

            foreach (var booking in overdueBookings)
            {
                using var transaction = await db.Database.BeginTransactionAsync();
                try
                {
                    booking.Status = BookingStatus.Completed;
                    booking.UpdatedAt = now;
                    await bookingRepo.UpdateAsync(booking);

                    if (booking.ChargingSlot != null && booking.ChargingSlot.Status == SlotStatus.Booked)
                    {
                        booking.ChargingSlot.Status = SlotStatus.Active;
                        slotRepo.Update(booking.ChargingSlot);
                        await slotRepo.SaveChangesAsync();
                    }

                    var ownerUserId = booking.ChargingSlot?.ChargingStation?.OwnerUserId;
                    if (ownerUserId.HasValue)
                    {
                        await SettleToOwnerAsync(db, booking, ownerUserId.Value, "AutoComplete", now);
                    }

                    await transaction.CommitAsync();

                    if (ownerUserId.HasValue)
                    {
                        await notificationService.SendAsync(
                            ownerUserId.Value,
                            "Booking đã hoàn thành tự động",
                            $"Booking tại slot {booking.ChargingSlot?.SlotName} — trạm {booking.ChargingSlot?.ChargingStation?.Name} đã hoàn thành.",
                            NotificationType.Payment);
                    }

                    await notificationService.SendAsync(
                        booking.DriverUserId,
                        "Booking đã hoàn thành",
                        $"Booking tại slot {booking.ChargingSlot?.SlotName} — trạm {booking.ChargingSlot?.ChargingStation?.Name} ({booking.StartTime:HH:mm} - {booking.EndTime:HH:mm dd/MM}) đã tự động hoàn thành.",
                        NotificationType.Booking);

                    _logger.LogInformation("Booking {BookingId} auto-completed (Paid, no check-in).", booking.Id);
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Error auto-completing Paid booking {BookingId}", booking.Id);
                }
            }
        }

        // ═══════════════════════════════════════════════════════
        // 3. CheckedIn quá EndTime + 30 phút → Auto-stop + invoice + settle
        // ═══════════════════════════════════════════════════════
        private async Task ProcessCheckedInOvertimeAsync()
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<Data.ChargeSlotDbContext>();
            var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
            var configService = scope.ServiceProvider.GetRequiredService<ISystemConfigService>();

            var configs = await configService.GetCurrentConfigsAsync();
            var graceTime = configs.NoShow_Grace_Minutes;

            var now = DateTimeHelper.VietnamNow();
            var cutoff = now.AddMinutes(-graceTime);

            var overtimeBookings = await db.Bookings
                .Where(b => b.Status == BookingStatus.CheckedIn && b.EndTime < cutoff)
                .Include(b => b.ChargingSlot).ThenInclude(s => s.ChargingStation)
                .Include(b => b.ChargingSession)
                .ToListAsync();

            foreach (var booking in overtimeBookings)
            {
                using var transaction = await db.Database.BeginTransactionAsync();
                try
                {
                    // 1. Stop session
                    var session = booking.ChargingSession;
                    if (session != null)
                    {
                        session.ActualEndTime = booking.EndTime; // Dùng EndTime gốc, không phải now
                        session.ActualDurationHours = (decimal)(booking.EndTime - (session.ActualStartTime ?? booking.StartTime)).TotalHours;
                        db.ChargingSessions.Update(session);
                    }

                    // 2. Create invoice
                    var grossAmount = booking.TotalAmount;
                    var vatRate = booking.VatRateSnapshot == 0 ? 0.08m : booking.VatRateSnapshot;
                    var platformFeeRate = booking.PlatformFeeRateSnapshot == 0 ? 0.05m : booking.PlatformFeeRateSnapshot;

                    var vatAmount = Math.Round(grossAmount * vatRate, 0);
                    var platformFee = Math.Round(grossAmount * platformFeeRate, 0);
                    var ownerNetAmount = grossAmount - vatAmount - platformFee;

                    var invoice = new Models.Invoice
                    {
                        BookingId = booking.Id,
                        ChargingAmount = ownerNetAmount,
                        ServiceAmount = 0,
                        VatAmount = vatAmount,
                        PlatformFee = platformFee,
                        TotalAmount = grossAmount,
                        Status = InvoiceStatus.Confirmed, // Auto-confirmed (Owner quên dừng)
                        CreatedAt = now,
                        UpdatedAt = now
                    };
                    db.Invoices.Add(invoice);

                    // 3. Complete booking
                    booking.Status = BookingStatus.Completed;
                    booking.UpdatedAt = now;

                    // 4. Release slot
                    if (booking.ChargingSlot != null && booking.ChargingSlot.Status == SlotStatus.Booked)
                    {
                        booking.ChargingSlot.Status = SlotStatus.Active;
                        booking.ChargingSlot.UpdatedAt = now;
                    }

                    await db.SaveChangesAsync();

                    // 5. Settle payment
                    var ownerUserId = booking.ChargingSlot?.ChargingStation?.OwnerUserId;
                    if (ownerUserId.HasValue)
                    {
                        await SettleToOwnerAsync(db, booking, ownerUserId.Value, "AutoStopComplete", now);
                    }

                    await transaction.CommitAsync();

                    // Notifications (ngoài transaction)
                    await notificationService.SendAsync(
                        booking.DriverUserId,
                        "Phiên sạc đã kết thúc tự động",
                        $"Phiên sạc tại slot {booking.ChargingSlot?.SlotName} — trạm {booking.ChargingSlot?.ChargingStation?.Name} đã tự động kết thúc vì đã quá thời gian booking.",
                        NotificationType.Booking);

                    if (ownerUserId.HasValue)
                    {
                        await notificationService.SendAsync(
                            ownerUserId.Value,
                            "Phiên sạc đã kết thúc tự động",
                            $"Phiên sạc tại slot {booking.ChargingSlot?.SlotName} đã tự động kết thúc (quá thời gian). Tiền đã settle vào ví của bạn.",
                            NotificationType.Payment);
                    }

                    _logger.LogInformation("Booking {BookingId} auto-stopped (CheckedIn overtime).", booking.Id);
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Error auto-stopping CheckedIn booking {BookingId}", booking.Id);
                }
            }
        }

        // ═══════════════════════════════════════════════════════
        // SHARED: Settle ESCROW → Owner + Platform
        // ═══════════════════════════════════════════════════════
        private static async Task SettleToOwnerAsync(Data.ChargeSlotDbContext db, Models.Booking booking, int ownerUserId, string referenceType, DateTime now)
        {
            var grossAmount = booking.TotalAmount;
            var vatRate = booking.VatRateSnapshot == 0 ? 0.08m : booking.VatRateSnapshot;
            var platformFeeRate = booking.PlatformFeeRateSnapshot == 0 ? 0.05m : booking.PlatformFeeRateSnapshot;

            var vatAmount = Math.Round(grossAmount * vatRate, 0);
            var platformFee = Math.Round(grossAmount * platformFeeRate, 0);
            var ownerNet = grossAmount - vatAmount - platformFee;

            var escrowWallet = await db.Wallets.FirstAsync(w => w.SystemCode == "ESCROW");
            var platformWallet = await db.Wallets.FirstAsync(w => w.SystemCode == "PLATFORM_REVENUE");
            var ownerWallet = await db.Wallets.FirstOrDefaultAsync(w => w.UserId == ownerUserId);

            if (ownerWallet == null)
            {
                ownerWallet = new Models.Wallet
                {
                    UserId = ownerUserId,
                    WalletType = WalletType.Owner,
                    AvailableBalance = 0,
                    FrozenBalance = 0,
                    CreatedAt = now
                };
                db.Wallets.Add(ownerWallet);
                await db.SaveChangesAsync();
            }

            // ESCROW → Owner (net) + Platform (fee) atomically
            await db.Database.ExecuteSqlRawAsync(
                "UPDATE Wallet SET AvailableBalance = AvailableBalance - {0} WHERE Id = {1}",
                ownerNet + platformFee, escrowWallet.Id);
            
            await db.Database.ExecuteSqlRawAsync(
                "UPDATE Wallet SET AvailableBalance = AvailableBalance + {0} WHERE Id = {1}",
                ownerNet, ownerWallet.Id);
                
            await db.Database.ExecuteSqlRawAsync(
                "UPDATE Wallet SET AvailableBalance = AvailableBalance + {0} WHERE Id = {1}",
                platformFee, platformWallet.Id);

            db.Set<Models.LedgerTransaction>().Add(new Models.LedgerTransaction
            {
                ReferenceType = referenceType,
                ReferenceId = booking.Id,
                Memo = $"{referenceType} booking #{booking.Id} — Owner nhận {ownerNet:N0}đ, phí nền tảng {platformFee:N0}đ",
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
    }
}
