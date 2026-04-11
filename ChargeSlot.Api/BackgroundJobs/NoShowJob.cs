using ChargeSlot.Api.Enums;
using ChargeSlot.Api.Models;
using ChargeSlot.Api.Repositories.Interfaces;
using ChargeSlot.Api.Services.Interfaces;
using ChargeSlot.Api.Helpers;

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
            var bookingRepo = scope.ServiceProvider.GetRequiredService<IBookingRepository>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
            var configService = scope.ServiceProvider.GetRequiredService<ISystemConfigService>();

            var configs = await configService.GetCurrentConfigsAsync();
            var graceTime = configs.NoShow_Grace_Minutes;

            var now = DateTimeHelper.VietnamNow();
            var cutoff = now.AddMinutes(-graceTime);

            var staleBookings = await bookingRepo.GetStaleWaitingOwnerAsync(cutoff);

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

                await unitOfWork.CompleteAsync();

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
            var bookingRepo = scope.ServiceProvider.GetRequiredService<IBookingRepository>();
            var slotRepo = scope.ServiceProvider.GetRequiredService<IChargingSlotRepository>();
            var invoiceRepo = scope.ServiceProvider.GetRequiredService<IInvoiceRepository>();
            var walletRepo = scope.ServiceProvider.GetRequiredService<IWalletRepository>();
            var ledgerRepo = scope.ServiceProvider.GetRequiredService<ILedgerTransactionRepository>();
            var loyaltyTxRepo = scope.ServiceProvider.GetRequiredService<ILoyaltyTransactionRepository>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
            var configService = scope.ServiceProvider.GetRequiredService<ISystemConfigService>();

            var configs = await configService.GetCurrentConfigsAsync();
            var graceTime = configs.NoShow_Grace_Minutes;

            var now = DateTimeHelper.VietnamNow();
            var cutoff = now.AddMinutes(-graceTime);

            var overdueBookings = await bookingRepo.GetPaidNoShowAsync(cutoff);

            foreach (var booking in overdueBookings)
            {
                using var transaction = await unitOfWork.BeginTransactionAsync();
                try
                {
                    booking.Status = BookingStatus.Completed;
                    booking.UpdatedAt = now;
                    bookingRepo.Update(booking);
                    await unitOfWork.CompleteAsync();

                    if (booking.ChargingSlot != null && booking.ChargingSlot.Status == SlotStatus.Booked)
                    {
                        booking.ChargingSlot.Status = SlotStatus.Active;
                        slotRepo.Update(booking.ChargingSlot);
                        await unitOfWork.CompleteAsync();
                    }

                    // H1 FIX: Tạo Invoice (trước đây bị thiếu)
                    var grossAmount = booking.TotalAmount;
                    var vatRate = booking.VatRateSnapshot == 0 ? 0.08m : booking.VatRateSnapshot;
                    var platformFeeRate = booking.PlatformFeeRateSnapshot == 0 ? 0.05m : booking.PlatformFeeRateSnapshot;
                    var vatAmount = Math.Round(grossAmount * vatRate, 0);
                    var platformFee = Math.Round(grossAmount * platformFeeRate, 0);
                    var ownerNetAmount = grossAmount - vatAmount - platformFee;

                    var invoice = new Invoice
                    {
                        BookingId = booking.Id,
                        ChargingAmount = ownerNetAmount,
                        ServiceAmount = 0,
                        VatAmount = vatAmount,
                        PlatformFee = platformFee,
                        TotalAmount = grossAmount,
                        Status = InvoiceStatus.Confirmed, // Auto-confirmed (No-Show)
                        CreatedAt = now,
                        UpdatedAt = now
                    };
                    invoiceRepo.Add(invoice);
                    await unitOfWork.CompleteAsync();

                    // H2 FIX: Loyalty Points (dùng snapshot từ lúc tạo booking)
                    var earnRate = booking.LoyaltyEarnRateSnapshot == 0 ? 0.05m : booking.LoyaltyEarnRateSnapshot;
                    var pointsEarned = Math.Floor(booking.TotalAmount * earnRate);
                    if (pointsEarned > 0 && booking.Driver != null)
                    {
                        booking.Driver.LoyaltyPoints += pointsEarned;
                        booking.PointsEarned = pointsEarned;
                        loyaltyTxRepo.Add(new LoyaltyTransaction
                        {
                            DriverUserId = booking.DriverUserId,
                            BookingId = booking.Id,
                            Type = "Earn",
                            Points = pointsEarned,
                            Description = $"Tích {pointsEarned:N0} điểm từ booking #{booking.Id} (auto-complete, no-show)",
                            CreatedAt = now
                        });
                        await unitOfWork.CompleteAsync();
                    }

                    var ownerUserId = booking.ChargingSlot?.ChargingStation?.OwnerUserId;
                    if (ownerUserId.HasValue)
                    {
                        await SettleToOwnerAsync(walletRepo, ledgerRepo, unitOfWork, booking, ownerUserId.Value, "AutoComplete", now);
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
            var bookingRepo = scope.ServiceProvider.GetRequiredService<IBookingRepository>();
            var invoiceRepo = scope.ServiceProvider.GetRequiredService<IInvoiceRepository>();
            var walletRepo = scope.ServiceProvider.GetRequiredService<IWalletRepository>();
            var ledgerRepo = scope.ServiceProvider.GetRequiredService<ILedgerTransactionRepository>();
            var loyaltyTxRepo = scope.ServiceProvider.GetRequiredService<ILoyaltyTransactionRepository>();
            var chargingSessionRepo = scope.ServiceProvider.GetRequiredService<IChargingSessionRepository>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
            var configService = scope.ServiceProvider.GetRequiredService<ISystemConfigService>();

            var configs = await configService.GetCurrentConfigsAsync();
            var graceTime = configs.NoShow_Grace_Minutes;

            var now = DateTimeHelper.VietnamNow();
            var cutoff = now.AddMinutes(-graceTime);

            var overtimeBookings = await bookingRepo.GetCheckedInOvertimeAsync(cutoff);

            foreach (var booking in overtimeBookings)
            {
                using var transaction = await unitOfWork.BeginTransactionAsync();
                try
                {
                    // 1. Stop session
                    var session = booking.ChargingSession;
                    if (session != null)
                    {
                        session.ActualEndTime = booking.EndTime; // Dùng EndTime gốc, không phải now
                        session.ActualDurationHours = (decimal)(booking.EndTime - (session.ActualStartTime ?? booking.StartTime)).TotalHours;
                        chargingSessionRepo.Update(session);
                    }

                    // 2. Create invoice
                    var grossAmount = booking.TotalAmount;
                    var vatRate = booking.VatRateSnapshot == 0 ? 0.08m : booking.VatRateSnapshot;
                    var platformFeeRate = booking.PlatformFeeRateSnapshot == 0 ? 0.05m : booking.PlatformFeeRateSnapshot;

                    var vatAmount = Math.Round(grossAmount * vatRate, 0);
                    var platformFee = Math.Round(grossAmount * platformFeeRate, 0);
                    var ownerNetAmount = grossAmount - vatAmount - platformFee;

                    var invoice = new Invoice
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
                    invoiceRepo.Add(invoice);

                    // 3. Complete booking
                    booking.Status = BookingStatus.Completed;
                    booking.UpdatedAt = now;

                    // 4. Release slot
                    if (booking.ChargingSlot != null && booking.ChargingSlot.Status == SlotStatus.Booked)
                    {
                        booking.ChargingSlot.Status = SlotStatus.Active;
                        booking.ChargingSlot.UpdatedAt = now;
                    }

                    await unitOfWork.CompleteAsync();

                    // H2 FIX: Loyalty Points (dùng snapshot từ lúc tạo booking)
                    var earnRate = booking.LoyaltyEarnRateSnapshot == 0 ? 0.05m : booking.LoyaltyEarnRateSnapshot;
                    var pointsEarned2 = Math.Floor(booking.TotalAmount * earnRate);
                    if (pointsEarned2 > 0 && booking.Driver != null)
                    {
                        booking.Driver.LoyaltyPoints += pointsEarned2;
                        booking.PointsEarned = pointsEarned2;
                        loyaltyTxRepo.Add(new LoyaltyTransaction
                        {
                            DriverUserId = booking.DriverUserId,
                            BookingId = booking.Id,
                            Type = "Earn",
                            Points = pointsEarned2,
                            Description = $"Tích {pointsEarned2:N0} điểm từ booking #{booking.Id} (auto-stop overtime)",
                            CreatedAt = now
                        });
                        await unitOfWork.CompleteAsync();
                    }

                    // 5. Settle payment
                    var ownerUserId = booking.ChargingSlot?.ChargingStation?.OwnerUserId;
                    if (ownerUserId.HasValue)
                    {
                        await SettleToOwnerAsync(walletRepo, ledgerRepo, unitOfWork, booking, ownerUserId.Value, "AutoStopComplete", now);
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
        // SHARED: Settle ESCROW → Owner + Platform + TAX_HOLD
        // ═══════════════════════════════════════════════════════
        private static async Task SettleToOwnerAsync(IWalletRepository walletRepo, ILedgerTransactionRepository ledgerRepo, IUnitOfWork unitOfWork, Booking booking, int ownerUserId, string referenceType, DateTime now)
        {
            var grossAmount = booking.TotalAmount;
            var vatRate = booking.VatRateSnapshot == 0 ? 0.08m : booking.VatRateSnapshot;
            var platformFeeRate = booking.PlatformFeeRateSnapshot == 0 ? 0.05m : booking.PlatformFeeRateSnapshot;

            var vatAmount = Math.Round(grossAmount * vatRate, 0);
            var platformFee = Math.Round(grossAmount * platformFeeRate, 0);
            var ownerNet = grossAmount - vatAmount - platformFee;

            var escrowWallet = await walletRepo.GetBySystemCodeAsync("ESCROW")
                ?? throw new InvalidOperationException("ESCROW wallet not found");
            var platformWallet = await walletRepo.GetBySystemCodeAsync("PLATFORM_REVENUE")
                ?? throw new InvalidOperationException("PLATFORM_REVENUE wallet not found");
            var taxWallet = await walletRepo.GetBySystemCodeAsync("TAX_HOLD")
                ?? throw new InvalidOperationException("TAX_HOLD wallet not found");
            var ownerWallet = await walletRepo.GetByUserIdAsync(ownerUserId);

            if (ownerWallet == null)
            {
                ownerWallet = new Wallet
                {
                    UserId = ownerUserId,
                    WalletType = WalletType.Owner,
                    AvailableBalance = 0,
                    FrozenBalance = 0,
                    CreatedAt = now
                };
                walletRepo.Add(ownerWallet);
                await unitOfWork.CompleteAsync();
            }

            // ESCROW → Owner (net) + Platform (fee) + TAX_HOLD (VAT)
            escrowWallet.AvailableBalance -= (ownerNet + platformFee + vatAmount);
            ownerWallet.AvailableBalance += ownerNet;
            platformWallet.AvailableBalance += platformFee;
            taxWallet.AvailableBalance += vatAmount;

            // Ledger: Settlement (Owner + Platform)
            ledgerRepo.Add(new LedgerTransaction
            {
                ReferenceType = referenceType,
                ReferenceId = booking.Id,
                Memo = $"{referenceType} booking #{booking.Id} — Owner nhận {ownerNet:N0}đ, phí nền tảng {platformFee:N0}đ",
                CreatedAt = now,
                Entries = new List<LedgerEntry>
                {
                    new() { WalletId = escrowWallet.Id, Direction = LedgerDirection.Debit, Amount = ownerNet + platformFee, CreatedAt = now },
                    new() { WalletId = ownerWallet.Id, Direction = LedgerDirection.Credit, Amount = ownerNet, CreatedAt = now },
                    new() { WalletId = platformWallet.Id, Direction = LedgerDirection.Credit, Amount = platformFee, CreatedAt = now }
                }
            });

            // Ledger: VAT → TAX_HOLD
            if (vatAmount > 0)
            {
                ledgerRepo.Add(new LedgerTransaction
                {
                    ReferenceType = "TaxHold",
                    ReferenceId = booking.Id,
                    Memo = $"Thuế GTGT {referenceType} booking #{booking.Id} - {vatAmount:N0}đ",
                    CreatedAt = now,
                    Entries = new List<LedgerEntry>
                    {
                        new() { WalletId = escrowWallet.Id, Direction = LedgerDirection.Debit, Amount = vatAmount, CreatedAt = now },
                        new() { WalletId = taxWallet.Id, Direction = LedgerDirection.Credit, Amount = vatAmount, CreatedAt = now }
                    }
                });
            }

            await unitOfWork.CompleteAsync();
        }
    }
}
