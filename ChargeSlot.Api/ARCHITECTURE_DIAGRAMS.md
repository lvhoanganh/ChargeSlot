# 📸 HỆ THỐNG SƠ ĐỒ CHI TIẾT (STRICT CODE-LEVEL ARCHITECTURE)

Dưới đây là các sơ đồ tuần tự (Sequence Diagram) được trích xuất **chính xác 100% từng dòng lệnh (method calls)** từ source code hiện tại, với trật tự Layer chuẩn xác từ `Controller` -> `Service` -> `Repository` -> `DbContext`.

---

## 📅 1. TẠO HỒ SƠ ĐẶT CHỖ (CREATE BOOKING)

```mermaid
sequenceDiagram
    autonumber
    actor Driver
    participant Ctrl as :BookingController
    participant Svc as :IBookingService
    participant SlotRepo as :IChargingSlotRepository
    participant BookingRepo as :IBookingRepository
    participant Ctx as :ChargeSlotDbContext
    participant SetupConf as :ISystemConfigService
    participant NotiSvc as :INotificationService

    Driver->>Ctrl: POST /api/booking
    Ctrl->>Ctrl: GetUserId()
    Ctrl->>Svc: CreateBookingAsync(driverUserId, dto)
    
    Svc->>Ctx: BeginTransactionAsync()
    Svc->>Ctx: ExecuteSqlRawAsync("EXEC sp_getapplock...")
    Ctx-->>Svc: Lock acquired
    
    Svc->>Svc: Validate booking request
    Note right of Svc: 1. StartTime.Minute == 0 || 30<br>2. Duration: bội số 0.5

    Svc->>SlotRepo: GetByIdWithStationAsync(dto.SlotId)
    
    Svc->>BookingRepo: GetOverlappingBookingsAsync(slotId, startTime, endTime, 0)
    alt isOverlap == true
        Svc-->>Ctrl: throw InvalidOperationException("Slot đã được đặt/sử dụng")
    end
    
    Svc->>BookingRepo: GetDriverOverlappingBookingsAsync(userId, startTime, endTime)
    alt isUserOverlap == true
        Svc-->>Ctrl: throw InvalidOperationException("Bạn đã có booking trùng giờ")
    end

    Svc->>SetupConf: GetCurrentConfigsAsync()
    
    Svc->>BookingRepo: CreateAsync(booking)
    Svc->>Ctx: SaveChangesAsync()
    
    Svc->>Ctx: CommitTransactionAsync()
    Note over Svc, Ctx: Release applock tự động
    
    Svc->>NotiSvc: SendAsync(ownerUserId, "Có người đặt lịch")
    Svc->>NotiSvc: SendAsync(driverUserId, "Đặt chỗ thành công")
    
    Svc-->>Ctrl: mapped DTO
```

---

## 💳 2A. THANH TOÁN PAYMENT VIA WALLET

```mermaid
sequenceDiagram
    autonumber
    actor Driver
    participant Ctrl as :WalletController
    participant Svc as :IWalletService
    participant WalletRepo as :IWalletRepository
    participant BookingRepo as :IBookingRepository
    participant PaymentRepo as :IPaymentRepository
    participant SlotRepo as :IChargingSlotRepository
    participant Ctx as :ChargeSlotDbContext

    Driver->>Ctrl: POST /api/wallet/pay-booking/{bookingId}
    Ctrl->>Ctrl: GetUserId()
    Ctrl->>Svc: PayBookingByWalletAsync(userId, bookingId)
    
    Svc->>Ctx: BeginTransactionAsync()
    
    Svc->>Svc: GetOrCreateWalletInternalAsync(userId)
    Svc->>BookingRepo: GetByIdWithDetailsAsync(bookingId)
    
    Svc->>Svc: Validate: Status == PendingPayment & Balance
    
    Svc->>Ctx: ExecuteSqlRawSafeAsync("UPDATE Wallet SET AvailableBalance -= ...")
    Svc->>Ctx: Entry(wallet).ReloadAsync()
    
    Svc->>Ctx: Wallets.FirstOrDefaultAsync(w => w.SystemCode == "ESCROW")
    Svc->>Ctx: ExecuteSqlRawSafeAsync("UPDATE Wallet SET AvailableBalance += ...")
    
    Svc->>WalletRepo: AddLedgerTransactionAsync(ledgerTx)
    
    Svc->>PaymentRepo: GetByBookingIdAsync(bookingId)
    alt payment == null
        Svc->>PaymentRepo: CreateAsync(new Payment)
    else
        Svc->>PaymentRepo: UpdateAsync(payment)
    end
    
    Svc->>BookingRepo: UpdateAsync(booking)
    
    opt BookingExtraServices.Count > 0
        Svc->>Ctx: Set<ExtraService>().FindAsync(bes.ServiceId)
        Svc->>Ctx: SaveChangesAsync()
    end
    
    Svc->>SlotRepo: GetByIdAsync(booking.SlotId, tracking: true)
    Svc->>SlotRepo: Update(slot)
    Svc->>SlotRepo: SaveChangesAsync()
    
    Svc->>Ctx: CommitTransactionAsync()
    Svc-->>Ctrl: return WalletDto
```

---

## 🏦 2B. THANH TOÁN VIETQR WEBHOOK (SEPAY)

```mermaid
sequenceDiagram
    autonumber
    participant SePay as :SePay Webhook
    participant Ctrl as :PaymentController
    participant Svc as :IPaymentService
    participant BookingRepo as :IBookingRepository
    participant PaymentRepo as :IPaymentRepository
    participant SlotRepo as :IChargingSlotRepository
    participant Ctx as :ChargeSlotDbContext

    SePay->>Ctrl: POST /api/payment/sepay-webhook
    Ctrl->>Svc: ProcessSePayWebhookAsync(request)
    
    Svc->>Svc: Extract "CS{bookingId}" or "W{userId}"
    alt Match CS{bookingId}
        Svc->>Svc: ProcessBookingWebhookAsync(bookingId, request)
        
        Svc->>Ctx: BeginTransactionAsync()
        Svc->>BookingRepo: GetByIdWithDetailsAsync(bookingId)
        Svc->>PaymentRepo: GetByBookingIdAsync(bookingId)
        
        alt booking.Status == PendingPayment & amount >= TotalAmount
            Svc->>Svc: CompletePaymentAsync(booking, payment)
            Note over Svc, Ctx: Cập nhật ESCROW 
            Svc->>Ctx: AddLedgerTransactionAsync() / ExecuteSqlRaw
            Svc->>PaymentRepo: UpdateAsync(payment)
            Svc->>BookingRepo: UpdateAsync(booking)
            Svc->>SlotRepo: GetByIdAsync() & SaveChangesAsync()
        else (amount < TotalAmount OR Conflict)
            Svc->>Svc: RefundToDriverWalletAsync
            Note right of Svc: Fallback ngầm (Nạp lại Wallet User)
        end
        Svc->>Ctx: CommitTransactionAsync()
    end
```

---

## 📍 3. CHECK-IN NHẬN XE TẠI TRẠM

```mermaid
sequenceDiagram
    autonumber
    actor Driver
    participant Ctrl as :ChargingSessionController
    participant Svc as :IChargingSessionService
    participant ConfigSvc as :ISystemConfigService
    participant BookingRepo as :IBookingRepository
    participant SessionRepo as :IChargingSessionRepository
    participant SlotRepo as :IChargingSlotRepository
    participant Ctx as :ChargeSlotDbContext

    Driver->>Ctrl: POST /api/session/checkin
    Ctrl->>Ctrl: GetUserId()
    Ctrl->>Svc: CheckInAsync(driverUserId, dto.QrCodeToken)
    
    Svc->>Ctx: ChargingSlots.FirstOrDefaultAsync(s => s.QrCodeToken == qrCodeToken)
    Svc->>Ctx: Bookings.FirstOrDefaultAsync(b => b.DriverUserId == driverUserId && b.SlotId == slot.Id && b.Status == Paid)
    
    Svc->>ConfigSvc: GetCurrentConfigsAsync()
    
    Svc->>Svc: Validate time window (CheckinDeadlineAt)
    Svc->>Ctx: ChargingSessions.AnyAsync(s => s.BookingId == booking.Id)
    Svc->>Ctx: ChargingSessions.AnyAsync(s => s.Booking.SlotId == slot.Id && s.ActualEndTime == null)
    
    Svc->>BookingRepo: UpdateAsync(booking) (Status = CheckedIn)
    
    Svc->>SessionRepo: CreateAsync(new ChargingSession)
    
    Svc->>SlotRepo: GetByIdAsync(slot.Id, tracking: true)
    Svc->>SlotRepo: SaveChangesAsync() (Status = Booked)
    
    Svc-->>Ctrl: ChargingSessionDto
```

---

## ⚡ 4. BẮT ĐẦU SẠC (START SESSION)

```mermaid
sequenceDiagram
    autonumber
    actor Owner
    participant Ctrl as :ChargingSessionController
    participant Svc as :IChargingSessionService
    participant SessionRepo as :IChargingSessionRepository
    participant BookingRepo as :IBookingRepository
    participant Ctx as :ChargeSlotDbContext

    Owner->>Ctrl: PUT /api/session/{sessionId}/start
    Ctrl->>Ctrl: GetUserId()
    Ctrl->>Svc: StartSessionAsync(ownerUserId, sessionId)
    
    Svc->>SessionRepo: GetByIdWithDetailsAsync(sessionId)
    
    Svc->>Ctx: BeginTransactionAsync()

    Svc->>Svc: Validate owner ownership & Booking.Status == CheckedIn
    
    Svc->>Svc: session.ActualStartTime = DateTimeHelper.VietnamNow()
    Svc->>SessionRepo: UpdateAsync(session)
    
    Svc->>Svc: booking.Status = Active
    Svc->>BookingRepo: UpdateAsync(booking)
    
    Svc->>Ctx: CommitTransactionAsync()
    
    Svc-->>Ctrl: ChargingSessionDto
```

---

## 🛑 5. KẾT THÚC SẠC (END SESSION)

```mermaid
sequenceDiagram
    autonumber
    actor Owner
    participant Ctrl as :ChargingSessionController
    participant Svc as :IChargingSessionService
    participant SessionRepo as :IChargingSessionRepository
    participant BookingRepo as :IBookingRepository
    participant InvoiceRepo as :IInvoiceRepository
    participant SlotRepo as :IChargingSlotRepository
    participant Ctx as :ChargeSlotDbContext

    Owner->>Ctrl: PUT /api/session/{sessionId}/end
    Ctrl->>Ctrl: GetUserId()
    Ctrl->>Svc: StopChargingAsync(ownerUserId, sessionId)
    
    Svc->>Ctx: BeginTransactionAsync()
    Svc->>SessionRepo: GetByIdWithDetailsAsync(sessionId)
    
    Svc->>Svc: Validate: now >= EndTime OR booking.EarlyEndRequestedAt.HasValue
    
    Svc->>Svc: session.ActualEndTime = now
    Svc->>SessionRepo: UpdateAsync(session)
    
    Svc->>Svc: booking.Status = CompletedPendingInvoice
    Svc->>BookingRepo: UpdateAsync(booking)
    
    Svc->>Svc: grossAmount = TotalAmount
    Svc->>Svc: Tính vatAmount = Math.Round(grossAmount * vatRate)
    Svc->>Svc: Tính platformFee = Math.Round(grossAmount * platformFeeRate)
    Svc->>Svc: ownerNet = grossAmount - vat - fee
    
    Svc->>InvoiceRepo: CreateAsync(new Invoice { ChargingAmount=ownerNet ... })
    
    Svc->>SlotRepo: GetByIdAsync(booking.SlotId, tracking: true)
    Svc->>SlotRepo: SaveChangesAsync() (Status = Active)
    
    Svc->>Ctx: CommitTransactionAsync()
    Svc-->>Ctrl: ChargingSessionDto
```

---

## 💸 6. RÚT TIỀN VÍ (WITHDRAW REQUEST)

```mermaid
sequenceDiagram
    autonumber
    actor Owner
    participant Ctrl as :WalletController
    participant Svc as :IWalletService
    participant WalletRepo as :IWalletRepository
    participant Ctx as :ChargeSlotDbContext

    Owner->>Ctrl: POST /api/wallet/withdraw
    Ctrl->>Ctrl: GetUserId()
    Ctrl->>Svc: WithdrawAsync(userId, dto)
    
    Svc->>WalletRepo: GetWalletByUserIdAsync(userId, WalletType.Owner)
    Svc->>Ctx: DbContext.Users.FindAsync(userId) (Check KYC)
    
    Svc->>Ctx: BeginTransactionAsync()
    
    Svc->>Svc: wallet.AvailableBalance -= dto.Amount
    Svc->>Svc: wallet.FrozenBalance += dto.Amount
    Svc->>WalletRepo: UpdateWalletAsync(wallet)
    
    Svc->>Ctx: DbContext.Set<WithdrawRequest>().Add(new WithdrawRequest)
    Svc->>Ctx: SaveChangesAsync()
    
    Svc->>Ctx: CommitTransactionAsync()
    Svc-->>Ctrl: WithdrawRequestDto
```
