using ChargeSlot.Api.Helpers;
using ChargeSlot.Api.DTOs.Booking;
using ChargeSlot.Api.DTOs.ChargingSession;
using ChargeSlot.Api.DTOs.Invoice;
using ChargeSlot.Api.Enums;
using ChargeSlot.Api.Models;
using ChargeSlot.Api.Repositories.Interfaces;
using ChargeSlot.Api.Services.Interfaces;

namespace ChargeSlot.Api.Services.Implementation
{
    public class ChargingSessionService : IChargingSessionService
    {
        private readonly IChargingSessionRepository _sessionRepo;
        private readonly IInvoiceRepository _invoiceRepo;
        private readonly IBookingRepository _bookingRepo;
        private readonly IChargingSlotRepository _slotRepo;
        private readonly IWalletRepository _walletRepo;
        private readonly INotificationService _notificationService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IDriverRepository _driverRepo;
        private readonly ILoyaltyTransactionRepository _loyaltyRepo;
        private readonly ILedgerTransactionRepository _ledgerRepo;
        private readonly ISystemConfigService _configService;

        public ChargingSessionService(
            IChargingSessionRepository sessionRepo,
            IInvoiceRepository invoiceRepo,
            IBookingRepository bookingRepo,
            IChargingSlotRepository slotRepo,
            IWalletRepository walletRepo,
            INotificationService notificationService,
            IUnitOfWork unitOfWork,
            IDriverRepository driverRepo,
            ILoyaltyTransactionRepository loyaltyRepo,
            ILedgerTransactionRepository ledgerRepo,
            ISystemConfigService configService)
        {
            _sessionRepo = sessionRepo;
            _invoiceRepo = invoiceRepo;
            _bookingRepo = bookingRepo;
            _slotRepo = slotRepo;
            _walletRepo = walletRepo;
            _notificationService = notificationService;
            _unitOfWork = unitOfWork;
            _driverRepo = driverRepo;
            _loyaltyRepo = loyaltyRepo;
            _ledgerRepo = ledgerRepo;
            _configService = configService;
        }

        /// <summary>
        /// Driver scans QR code on slot → system finds matching Paid booking → check in.
        /// Validates: slot exists, booking is Paid, time window ±15 min.
        /// </summary>
        public async Task<ChargingSessionDto> CheckInAsync(int driverUserId, string qrCodeToken)
        {
            // 1. Find slot by QR token
            var slot = await _slotRepo.GetByQrCodeTokenAsync(qrCodeToken)
                ?? throw new InvalidOperationException("QR code không hợp lệ.");

            // 1.5 Validate if station/slot is operational
            if (slot.Status == SlotStatus.Inactive || slot.Status == SlotStatus.Maintenance)
                throw new InvalidOperationException("Slot hiện đang ngừng hoạt động hoặc bảo trì, không thể check-in.");
            
            if (slot.ChargingStation.OperationalStatus == OperationalStatus.Inactive)
                throw new InvalidOperationException("Trạm sạc hiện đang ngừng hoạt động, không thể check-in.");

            // 2. Find Paid booking for this driver on this slot
            var now = DateTimeHelper.VietnamNow();
            var booking = await _bookingRepo.GetPaidBookingForDriverAndSlotAsync(driverUserId, slot.Id)
                ?? throw new InvalidOperationException("Không tìm thấy booking đã thanh toán trên slot này.");

            // 3. Validate time window: dùng snapshot CheckinDeadlineAt (đã set lúc payment)
            var configs = await _configService.GetCurrentConfigsAsync();
            var checkInWindowMinutes = configs.CheckIn_Window_Minutes;

            var earliestCheckin = booking.StartTime.AddMinutes(-checkInWindowMinutes);
            var latestCheckin = booking.CheckinDeadlineAt ?? booking.StartTime.AddMinutes(checkInWindowMinutes);
            if (now < earliestCheckin)
                throw new InvalidOperationException($"Chưa đến giờ check-in. Vui lòng quay lại lúc {earliestCheckin:HH:mm dd/MM/yyyy}.");
            if (now > latestCheckin)
                throw new InvalidOperationException("Đã quá thời gian check-in cho booking này.");

            // 4. Chống double check-in (cùng 1 booking)
            var existingSession = await _sessionRepo.HasSessionByBookingAsync(booking.Id);
            if (existingSession)
                throw new InvalidOperationException("Booking này đã được check-in trước đó.");

            // 4.5. Chống 2 phiên sạc đè nhau trên cùng 1 slot (thực tế vật lý)
            var hasOngoingSession = await _sessionRepo.HasOngoingSessionBySlotAsync(slot.Id);
            if (hasOngoingSession)
                throw new InvalidOperationException("Trụ sạc hiện vẫn đang có xe cắm sạc. Vui lòng yêu cầu xe trước kết thúc trước khi check-in.");

            // 5. Update booking status
            booking.Status = BookingStatus.CheckedIn;
            booking.CheckedInAt = now;
            booking.UpdatedAt = now;
            _bookingRepo.Update(booking);
            await _unitOfWork.CompleteAsync();

            // 6. Create charging session
            var session = new ChargingSession
            {
                BookingId = booking.Id,
                CheckinTime = now,
                ActualStartTime = now,
                CreatedAt = now
            };
            _sessionRepo.Add(session);
            await _unitOfWork.CompleteAsync();

            // 6. Update slot status to Booked
            var slotEntity = await _slotRepo.GetByIdAsync(slot.Id, tracking: true);
            if (slotEntity != null)
            {
                slotEntity.Status = SlotStatus.Booked;
                slotEntity.UpdatedAt = now;
                await _unitOfWork.CompleteAsync();
            }

            // 7. Notify Owner
            await _notificationService.SendAsync(
                slot.ChargingStation.OwnerUserId,
                "Driver đã check-in",
                $"Driver {booking.Driver?.User?.FullName ?? ""} đã check-in tại slot {slot.SlotName}.",
                NotificationType.Booking);

            return MapToDto(session, booking);
        }

        /// <summary>
        /// Owner stops charging session → create invoice from booking TotalAmount → notify driver.
        /// </summary>
        public async Task<ChargingSessionDto> StopChargingAsync(int ownerUserId, int sessionId)
        {
            using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                var session = await _sessionRepo.GetByIdWithDetailsAsync(sessionId)
                    ?? throw new InvalidOperationException("Session không tồn tại.");

                var booking = session.Booking;

                // Validate owner
                if (booking.ChargingSlot.ChargingStation.OwnerUserId != ownerUserId)
                    throw new UnauthorizedAccessException("Bạn không có quyền thao tác trên session này.");

                // Validate status
                if (booking.Status != BookingStatus.CheckedIn)
                    throw new InvalidOperationException("Booking không ở trạng thái CheckedIn.");

                var now = DateTimeHelper.VietnamNow();

                // Owner chỉ được kết thúc khi:
                // 1. Hết thời gian sạc (now >= EndTime), HOẶC
                // 2. Driver đã request kết thúc sớm
                var isTimeUp = now >= booking.EndTime;
                var isDriverRequestedEarlyEnd = booking.EarlyEndRequestedAt.HasValue;

                if (!isTimeUp && !isDriverRequestedEarlyEnd)
                    throw new InvalidOperationException(
                        $"Chưa thể kết thúc phiên sạc. Thời gian sạc kết thúc lúc {booking.EndTime:HH:mm dd/MM/yyyy}. " +
                        "Driver cần yêu cầu kết thúc sớm trước khi Owner có thể dừng.");

                // Update session
                session.ActualEndTime = now;
                session.ActualDurationHours = (decimal)(now - (session.ActualStartTime ?? now)).TotalHours;
                _sessionRepo.Update(session);
            await _unitOfWork.CompleteAsync();

                // Update booking → CompletedPendingInvoice (= WaitingDriverConfirm)
                booking.Status = BookingStatus.CompletedPendingInvoice;
                _bookingRepo.Update(booking);
            await _unitOfWork.CompleteAsync();

                // Create invoice - VAT & PlatformFee are DEDUCTED from booking amount
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
                    Status = InvoiceStatus.PendingConfirm,
                    CreatedAt = now
                };
                _invoiceRepo.Add(invoice);
            await _unitOfWork.CompleteAsync();

                // Release slot → Active
                var slot = await _slotRepo.GetByIdAsync(booking.SlotId, tracking: true);
                if (slot != null)
                {
                    slot.Status = SlotStatus.Active;
                    slot.UpdatedAt = now;
                    await _unitOfWork.CompleteAsync();
                }

                await transaction.CommitAsync();

                // Notify Driver (ngoài transaction)
                await _notificationService.SendAsync(
                    booking.DriverUserId,
                    "Phiên sạc đã kết thúc",
                    $"Phiên sạc tại slot {booking.ChargingSlot?.SlotName} — trạm {booking.ChargingSlot?.ChargingStation?.Name} đã kết thúc. Vui lòng xác nhận hóa đơn {grossAmount:N0}đ.",
                    NotificationType.Booking);

                return MapToDto(session, booking);
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        /// <summary>
        /// Driver yêu cầu kết thúc phiên sạc sớm → Owner mới được dừng.
        /// </summary>
        public async Task<ChargingSessionDto> RequestEarlyEndAsync(int driverUserId, int sessionId)
        {
            var session = await _sessionRepo.GetByIdWithDetailsAsync(sessionId)
                ?? throw new InvalidOperationException("Session không tồn tại.");

            var booking = session.Booking;

            if (booking.DriverUserId != driverUserId)
                throw new InvalidOperationException("Booking này không thuộc về bạn.");

            if (booking.Status != BookingStatus.CheckedIn)
                throw new InvalidOperationException("Booking không ở trạng thái đang sạc.");

            if (booking.EarlyEndRequestedAt.HasValue)
                throw new InvalidOperationException("Bạn đã yêu cầu kết thúc sớm rồi.");

            booking.EarlyEndRequestedAt = DateTimeHelper.VietnamNow();
            booking.UpdatedAt = DateTimeHelper.VietnamNow();
            _bookingRepo.Update(booking);
            await _unitOfWork.CompleteAsync();

            // Notify Owner
            var ownerUserId = booking.ChargingSlot.ChargingStation.OwnerUserId;
            await _notificationService.SendAsync(
                ownerUserId,
                "Driver yêu cầu kết thúc sạc sớm",
                $"{booking.Driver?.User?.FullName ?? "Driver"} yêu cầu kết thúc sớm phiên sạc tại slot {booking.ChargingSlot?.SlotName} — trạm {booking.ChargingSlot?.ChargingStation?.Name}. Vui lòng dừng phiên sạc.",
                NotificationType.Booking);

            return MapToDto(session, booking);
        }

        /// <summary>
        /// Driver confirms invoice → booking = Completed.
        /// </summary>
        public async Task<BookingDto> ConfirmCompletionAsync(int driverUserId, int sessionId)
        {
            // FIX: Transaction bảo vệ settlement flow (ESCROW → Owner + Platform)
            using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                var session = await _sessionRepo.GetByIdWithDetailsAsync(sessionId)
                    ?? throw new InvalidOperationException("Session không tồn tại.");

                var booking = session.Booking;

                if (booking.DriverUserId != driverUserId)
                    throw new InvalidOperationException("Booking này không thuộc về bạn.");

                if (booking.Status != BookingStatus.CompletedPendingInvoice)
                    throw new InvalidOperationException("Booking không ở trạng thái chờ xác nhận.");

                // Confirm invoice
                var invoice = await _invoiceRepo.GetByBookingIdAsync(booking.Id);
                if (invoice != null)
                {
                    invoice.Status = InvoiceStatus.Confirmed;
                    _invoiceRepo.Update(invoice);
            await _unitOfWork.CompleteAsync();
                }

                // Complete booking
                booking.Status = BookingStatus.Completed;
                _bookingRepo.Update(booking);
            await _unitOfWork.CompleteAsync();

                // ── LOYALTY POINTS: chỉ tích điểm khi Driver đã check-in (no-show không được điểm) ──
                if (booking.CheckedInAt != null)
                {
                var earnRate = booking.LoyaltyEarnRateSnapshot == 0 ? 0.05m : booking.LoyaltyEarnRateSnapshot;
                var pointsEarned = Math.Floor(booking.TotalAmount * earnRate);

                if (pointsEarned > 0)
                {
                    var driver = await _driverRepo.GetByUserIdAsync(booking.DriverUserId);
                    if (driver != null)
                    {
                        driver.LoyaltyPoints += pointsEarned;
                        booking.PointsEarned = pointsEarned;
                        _bookingRepo.Update(booking);
            await _unitOfWork.CompleteAsync();

                        _loyaltyRepo.Add(new LoyaltyTransaction
                        {
                            DriverUserId = booking.DriverUserId,
                            BookingId = booking.Id,
                            Type = "Earn",
                            Points = pointsEarned,
                            Description = $"Tích {pointsEarned:N0} điểm từ booking #{booking.Id} ({booking.TotalAmount:N0}đ × {earnRate * 100:N0}%)",
                            CreatedAt = DateTimeHelper.VietnamNow()
                        });
                        await _unitOfWork.CompleteAsync();
                    }
                }
                } // end CheckedInAt != null

                // ── WALLET SETTLEMENT ──
                if (invoice != null)
                {
                    await SettlePaymentToOwnerAsync(booking, invoice);
                }

                await transaction.CommitAsync();

                // Notify Owner (ngoài transaction)
                var station = booking.ChargingSlot?.ChargingStation;
                if (station != null)
                {
                    await _notificationService.SendAsync(
                        station.OwnerUserId,
                        "Driver đã xác nhận hoàn thành",
                        $"Phiên sạc tại slot {booking.ChargingSlot?.SlotName} — trạm {station.Name} đã hoàn thành. {invoice?.ChargingAmount:N0}đ đã chuyển vào ví của bạn.",
                        NotificationType.Payment);
                }

                return MapToBookingDto(booking);
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        /// <summary>
        /// Chuyển tiền từ ESCROW → Owner wallet (net) + PLATFORM_REVENUE (fee).
        /// Ghi ledger double-entry cho mỗi giao dịch.
        /// </summary>
        private async Task SettlePaymentToOwnerAsync(Booking booking, Invoice invoice)
        {
            var ownerUserId = booking.ChargingSlot!.ChargingStation!.OwnerUserId;
            var now = DateTimeHelper.VietnamNow();

            // Get wallets
            var escrowWallet = await _walletRepo.GetBySystemCodeAsync("ESCROW")
                ?? throw new InvalidOperationException("Ví hệ thống ESCROW chưa được cấu hình.");
            var platformWallet = await _walletRepo.GetBySystemCodeAsync("PLATFORM_REVENUE")
                ?? throw new InvalidOperationException("Ví hệ thống PLATFORM_REVENUE chưa được cấu hình.");
            var taxWallet = await _walletRepo.GetBySystemCodeAsync("TAX_HOLD")
                ?? throw new InvalidOperationException("Ví hệ thống TAX_HOLD chưa được cấu hình.");

            // Get or create Owner wallet
            var ownerWallet = await _walletRepo.GetByUserIdAsync(ownerUserId);
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
                _walletRepo.Add(ownerWallet);
            await _unitOfWork.CompleteAsync();
            }

            var ownerNet = invoice.ChargingAmount;  // Net amount after VAT + platform fee deducted
            var platformFee = invoice.PlatformFee;
            var vatAmount = invoice.VatAmount;

            // 1. ESCROW → Owner: net amount (Atomic via Repository)
            await _walletRepo.TransferAtomicAsync(escrowWallet.Id, ownerWallet.Id, ownerNet);

            var ownerLedger = new LedgerTransaction
            {
                ReferenceType = "Settlement",
                ReferenceId = booking.Id,
                Memo = $"Thanh toán booking #{booking.Id} - Owner nhận {ownerNet:N0}đ (sau trừ phí 5% + thuế 8%)",
                CreatedByUserId = null, // System
                CreatedAt = now,
                Entries = new List<LedgerEntry>
                {
                    new LedgerEntry { WalletId = escrowWallet.Id, Direction = LedgerDirection.Debit, Amount = ownerNet, CreatedAt = now },
                    new LedgerEntry { WalletId = ownerWallet.Id, Direction = LedgerDirection.Credit, Amount = ownerNet, CreatedAt = now }
                }
            };
            _walletRepo.AddLedgerTransaction(ownerLedger);
                    await _unitOfWork.CompleteAsync();

            // 2. ESCROW → PLATFORM_REVENUE: platform fee (Atomic via Repository)
            await _walletRepo.TransferAtomicAsync(escrowWallet.Id, platformWallet.Id, platformFee);

            var feeLedger = new LedgerTransaction
            {
                ReferenceType = "PlatformFee",
                ReferenceId = booking.Id,
                Memo = $"Phí nền tảng booking #{booking.Id} - {platformFee:N0}đ (Station: {booking.ChargingSlot.ChargingStation.Name})",
                CreatedByUserId = null,
                CreatedAt = now,
                Entries = new List<LedgerEntry>
                {
                    new LedgerEntry { WalletId = escrowWallet.Id, Direction = LedgerDirection.Debit, Amount = platformFee, CreatedAt = now },
                    new LedgerEntry { WalletId = platformWallet.Id, Direction = LedgerDirection.Credit, Amount = platformFee, CreatedAt = now }
                }
            };
            _walletRepo.AddLedgerTransaction(feeLedger);
                    await _unitOfWork.CompleteAsync();

            // 3. ESCROW → TAX_HOLD: VAT tax atomically
            if (vatAmount > 0)
            {
                await _walletRepo.TransferAtomicAsync(escrowWallet.Id, taxWallet.Id, vatAmount);

                var taxLedger = new LedgerTransaction
                {
                    ReferenceType = "TaxHold",
                    ReferenceId = booking.Id,
                    Memo = $"Thuế GTGT booking #{booking.Id} - {vatAmount:N0}đ",
                    CreatedByUserId = null,
                    CreatedAt = now,
                    Entries = new List<LedgerEntry>
                    {
                        new LedgerEntry { WalletId = escrowWallet.Id, Direction = LedgerDirection.Debit, Amount = vatAmount, CreatedAt = now },
                        new LedgerEntry { WalletId = taxWallet.Id, Direction = LedgerDirection.Credit, Amount = vatAmount, CreatedAt = now }
                    }
                };
                _walletRepo.AddLedgerTransaction(taxLedger);
                await _unitOfWork.CompleteAsync();
            }
        }

        public async Task<ChargingSessionDto?> GetByBookingIdAsync(int bookingId)
        {
            var session = await _sessionRepo.GetByBookingIdAsync(bookingId);
            if (session == null) return null;
            return MapToDto(session, session.Booking);
        }

        public async Task<List<ChargingSessionDto>> GetActiveByOwnerAsync(int ownerUserId)
        {
            var sessions = await _sessionRepo.GetActiveByOwnerAsync(ownerUserId);
            return sessions.Select(s => MapToDto(s, s.Booking)).ToList();
        }

        public async Task<InvoiceDto?> GetInvoiceByBookingIdAsync(int bookingId)
        {
            var invoice = await _invoiceRepo.GetByBookingIdAsync(bookingId);
            if (invoice == null) return null;
            return MapToInvoiceDto(invoice);
        }

        // ─────────────── MAPPING ───────────────

        private static ChargingSessionDto MapToDto(ChargingSession session, Booking booking)
        {
            return new ChargingSessionDto
            {
                Id = session.Id,
                BookingId = session.BookingId,
                SlotId = booking.SlotId,
                SlotName = booking.ChargingSlot?.SlotName ?? "",
                StationId = booking.ChargingSlot?.StationId ?? 0,
                StationName = booking.ChargingSlot?.ChargingStation?.Name ?? "",
                DriverName = booking.Driver?.User?.FullName ?? "",
                CheckinTime = session.CheckinTime,
                ActualStartTime = session.ActualStartTime,
                ActualEndTime = session.ActualEndTime,
                ActualDurationHours = session.ActualDurationHours,
                BookingStartTime = booking.StartTime,
                BookingEndTime = booking.EndTime,
                TotalAmount = booking.TotalAmount,
                BookingStatus = booking.Status.ToString(),
                CreatedAt = session.CreatedAt
            };
        }

        private static BookingDto MapToBookingDto(Booking b)
        {
            var serviceAmount = b.BookingExtraServices?.Sum(e => e.TotalPrice) ?? 0;

            return new BookingDto
            {
                Id = b.Id,
                DriverUserId = b.DriverUserId,
                DriverName = b.Driver?.User?.FullName ?? "",
                SlotId = b.SlotId,
                SlotName = b.ChargingSlot?.SlotName ?? "",
                StationId = b.ChargingSlot?.StationId ?? 0,
                StationName = b.ChargingSlot?.ChargingStation?.Name ?? "",
                StartTime = b.StartTime,
                EndTime = b.EndTime,
                DurationHours = b.DurationHours,
                TotalAmount = b.TotalAmount,
                ServiceAmount = serviceAmount,
                PointsUsed = b.PointsUsed,
                PointsDiscountAmount = b.PointsDiscountAmount,
                PointsEarned = b.PointsEarned,
                Note = b.Note,
                Status = b.Status.ToString(),
                RejectionReason = b.RejectionReason,
                CancelReason = b.CancelReason,
                PaymentExpiresAt = b.PaymentExpiresAt,
                CreatedAt = b.CreatedAt,
                ExtraServices = b.BookingExtraServices?.Select(e => new BookingExtraServiceDto
                {
                    ServiceId = e.ServiceId,
                    ServiceName = e.ExtraService?.ServiceName ?? "",
                    Quantity = e.Quantity,
                    UnitPrice = e.UnitPrice,
                    TotalPrice = e.TotalPrice
                }).ToList()
            };
        }

        private static InvoiceDto MapToInvoiceDto(Invoice invoice)
        {
            return new InvoiceDto
            {
                Id = invoice.Id,
                BookingId = invoice.BookingId,
                DriverName = invoice.Booking?.Driver?.User?.FullName,
                StationName = invoice.Booking?.ChargingSlot?.ChargingStation?.Name,
                ChargingAmount = invoice.ChargingAmount,
                ServiceAmount = invoice.ServiceAmount,
                VatAmount = invoice.VatAmount,
                PlatformFee = invoice.PlatformFee,
                TotalAmount = invoice.TotalAmount,
                Status = invoice.Status.ToString(),
                CreatedAt = invoice.CreatedAt,
                UpdatedAt = invoice.UpdatedAt
            };
        }

        // ═══════════════════════════════════════════════════════
        // MANUAL CHECK-IN (khi app/mạng lỗi, driver không quét QR được)
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// Driver gửi yêu cầu xác nhận thủ công khi không check-in được do lỗi mạng/app.
        /// Booking phải ở trạng thái Paid. Owner sẽ nhìn thấy request và xác nhận.
        /// </summary>
        public async Task<BookingDto> RequestManualCheckinAsync(int driverUserId, int bookingId)
        {
            var booking = await _bookingRepo.GetByIdWithDetailsAsync(bookingId)
                ?? throw new InvalidOperationException("Booking không tồn tại.");

            if (booking.DriverUserId != driverUserId)
                throw new UnauthorizedAccessException("Booking này không thuộc về bạn.");

            if (booking.Status != BookingStatus.Paid)
                throw new InvalidOperationException("Chỉ có thể yêu cầu xác nhận thủ công khi booking đã thanh toán.");

            if (booking.ManualCheckinRequestedAt.HasValue)
                throw new InvalidOperationException("Bạn đã gửi yêu cầu xác nhận thủ công rồi. Vui lòng chờ Owner xác nhận.");

            booking.ManualCheckinRequestedAt = DateTimeHelper.VietnamNow();
            booking.UpdatedAt = DateTimeHelper.VietnamNow();
            _bookingRepo.Update(booking);
            await _unitOfWork.CompleteAsync();

            // Notify Owner
            var ownerUserId = booking.ChargingSlot.ChargingStation.OwnerUserId;
            await _notificationService.SendAsync(
                ownerUserId,
                "Yêu cầu xác nhận thủ công",
                $"Driver {booking.Driver?.User?.FullName ?? ""} yêu cầu xác nhận thủ công tại slot {booking.ChargingSlot?.SlotName} — trạm {booking.ChargingSlot?.ChargingStation?.Name} ({booking.StartTime:HH:mm} - {booking.EndTime:HH:mm dd/MM}) do không thể check-in qua app. Vui lòng xác nhận nếu Driver đã sạc thực tế.",
                NotificationType.Booking);

            return MapToBookingDto(booking);
        }

        /// <summary>
        /// Owner xác nhận manual check-in → tạo session + invoice + settle payment + hoàn thành booking.
        /// Flow nén: CheckIn + StopCharging + ConfirmCompletion gộp thành 1 bước.
        /// </summary>
        public async Task<BookingDto> ConfirmManualCheckinAsync(int ownerUserId, int bookingId)
        {
            using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                var booking = await _bookingRepo.GetByIdWithDetailsAsync(bookingId)
                    ?? throw new InvalidOperationException("Booking không tồn tại.");

                // Validate owner
                if (booking.ChargingSlot.ChargingStation.OwnerUserId != ownerUserId)
                    throw new UnauthorizedAccessException("Bạn không có quyền thao tác trên booking này.");

                // Validate: booking phải đã Paid và có manual request
                if (booking.Status != BookingStatus.Paid)
                    throw new InvalidOperationException("Booking không ở trạng thái đã thanh toán.");

                if (!booking.ManualCheckinRequestedAt.HasValue)
                    throw new InvalidOperationException("Driver chưa gửi yêu cầu xác nhận thủ công.");

                var now = DateTimeHelper.VietnamNow();

                // 1. Tạo session (dùng thời gian booking làm thời gian sạc)
                var session = new ChargingSession
                {
                    BookingId = booking.Id,
                    CheckinTime = booking.ManualCheckinRequestedAt.Value,
                    ActualStartTime = booking.StartTime,
                    ActualEndTime = now > booking.EndTime ? booking.EndTime : now,
                    ActualDurationHours = (decimal)(booking.EndTime - booking.StartTime).TotalHours,
                    CreatedAt = now
                };
                _sessionRepo.Add(session);
            await _unitOfWork.CompleteAsync();

                // 2. Tạo invoice
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
                    Status = InvoiceStatus.Confirmed, // Đã xác nhận trực tiếp (không cần chờ 24h)
                    CreatedAt = now,
                    UpdatedAt = now
                };
                _invoiceRepo.Add(invoice);
            await _unitOfWork.CompleteAsync();

                // 3. Complete booking
                booking.Status = BookingStatus.Completed;
                booking.CheckedInAt = booking.ManualCheckinRequestedAt.Value;
                booking.UpdatedAt = now;
                _bookingRepo.Update(booking);
            await _unitOfWork.CompleteAsync();

                // 4. Loyalty points
                // Dùng snapshot LoyaltyEarnRateSnapshot (đồng nhất toàn hệ thống)
                var earnRate = booking.LoyaltyEarnRateSnapshot == 0 ? 0.05m : booking.LoyaltyEarnRateSnapshot;
                var pointsEarned = Math.Floor(booking.TotalAmount * earnRate);

                if (pointsEarned > 0)
                {
                    var driver = await _driverRepo.GetByUserIdAsync(booking.DriverUserId);
                    if (driver != null)
                    {
                        driver.LoyaltyPoints += pointsEarned;
                        booking.PointsEarned = pointsEarned;

                        _loyaltyRepo.Add(new LoyaltyTransaction
                        {
                            DriverUserId = booking.DriverUserId,
                            BookingId = booking.Id,
                            Type = "Earn",
                            Points = pointsEarned,
                            Description = $"Tích {pointsEarned:N0} điểm từ booking #{booking.Id} (xác nhận thủ công)",
                            CreatedAt = now
                        });
                        await _unitOfWork.CompleteAsync();
                    }
                }

                // 5. Settle payment (ESCROW → Owner + Platform)
                await SettlePaymentToOwnerAsync(booking, invoice);

                // 6. Release slot
                var slot = await _slotRepo.GetByIdAsync(booking.SlotId, tracking: true);
                if (slot != null && slot.Status == SlotStatus.Booked)
                {
                    slot.Status = SlotStatus.Active;
                    slot.UpdatedAt = now;
                    await _unitOfWork.CompleteAsync();
                }

                await transaction.CommitAsync();

                // Notifications (ngoài transaction)
                await _notificationService.SendAsync(
                    booking.DriverUserId,
                    "Xác nhận thủ công thành công",
                    $"Owner đã xác nhận phiên sạc thủ công tại slot {booking.ChargingSlot?.SlotName} — trạm {booking.ChargingSlot?.ChargingStation?.Name}. Booking #{booking.Id} đã hoàn thành. {(pointsEarned > 0 ? $"Bạn nhận {pointsEarned:N0} điểm thưởng." : "")}",
                    NotificationType.Booking);

                await _notificationService.SendAsync(
                    ownerUserId,
                    "Đã xác nhận phiên sạc thủ công",
                    $"Booking #{booking.Id} tại slot {booking.ChargingSlot?.SlotName} đã hoàn thành. {ownerNetAmount:N0}đ đã chuyển vào ví của bạn.",
                    NotificationType.Payment);

                return MapToBookingDto(booking);
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<ChargeSlot.Api.DTOs.Admin.Overview.PagedResultDto<ChargingSessionDto>> GetAdminAllSessionsAsync(ChargeSlot.Api.DTOs.Admin.Overview.SessionFilterDto filter)
        {
            var result = await _sessionRepo.GetAdminAllSessionsAsync(filter);
            
            return new ChargeSlot.Api.DTOs.Admin.Overview.PagedResultDto<ChargingSessionDto>
            {
                Items = result.Items.Select(s => MapToDto(s, s.Booking)).ToList(),
                TotalCount = result.TotalCount,
                Page = filter.Page,
                PageSize = filter.PageSize
            };
        }

        public async Task<ChargeSlot.Api.DTOs.Admin.Overview.PagedResultDto<InvoiceDto>> GetAdminAllInvoicesAsync(ChargeSlot.Api.DTOs.Admin.Overview.InvoiceFilterDto filter)
        {
            var result = await _invoiceRepo.GetAdminAllInvoicesAsync(filter);
            
            return new ChargeSlot.Api.DTOs.Admin.Overview.PagedResultDto<InvoiceDto>
            {
                Items = result.Items.Select(MapToInvoiceDto).ToList(),
                TotalCount = result.TotalCount,
                Page = filter.Page,
                PageSize = filter.PageSize
            };
        }
    }
}




