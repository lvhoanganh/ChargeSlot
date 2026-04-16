using ChargeSlot.Api.Helpers;
using ChargeSlot.Api.DTOs.Wallet;
using ChargeSlot.Api.Enums;
using ChargeSlot.Api.Models;
using ChargeSlot.Api.Models.Identity;
using ChargeSlot.Api.Repositories.Interfaces;
using ChargeSlot.Api.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;

namespace ChargeSlot.Api.Services.Implementation
{
    public class WalletService : IWalletService
    {
        private readonly IWalletRepository _walletRepo;
        private readonly IBookingRepository _bookingRepo;
        private readonly IPaymentRepository _paymentRepo;
        private readonly IChargingSlotRepository _slotRepo;
        private readonly INotificationService _notificationService;
        private readonly IFileStorageService _fileStorageService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IWithdrawRequestRepository _withdrawRepo;
        private readonly IOwnerRepository _ownerRepo;
        private readonly IExtraServiceRepository _extraServiceRepo;
        private readonly ILedgerTransactionRepository _ledgerRepo;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IConfiguration _configuration;
        private readonly ISystemConfigService _configService;

        public WalletService(
            IWalletRepository walletRepo,
            IBookingRepository bookingRepo,
            IPaymentRepository paymentRepo,
            IChargingSlotRepository slotRepo,
            INotificationService notificationService,
            IFileStorageService fileStorageService,
            IUnitOfWork unitOfWork,
            IWithdrawRequestRepository withdrawRepo,
            IOwnerRepository ownerRepo,
            IExtraServiceRepository extraServiceRepo,
            ILedgerTransactionRepository ledgerRepo,
            UserManager<ApplicationUser> userManager,
            IConfiguration configuration,
            ISystemConfigService configService)
        {
            _walletRepo = walletRepo;
            _bookingRepo = bookingRepo;
            _paymentRepo = paymentRepo;
            _slotRepo = slotRepo;
            _notificationService = notificationService;
            _fileStorageService = fileStorageService;
            _unitOfWork = unitOfWork;
            _withdrawRepo = withdrawRepo;
            _ownerRepo = ownerRepo;
            _extraServiceRepo = extraServiceRepo;
            _ledgerRepo = ledgerRepo;
            _userManager = userManager;
            _configuration = configuration;
            _configService = configService;
        }

        /// <summary>
        /// Lấy hoặc tạo ví cho user
        /// </summary>
        public async Task<WalletDto> GetOrCreateWalletAsync(int userId)
        {
            var wallet = await GetOrCreateWalletInternalAsync(userId);
            return MapToDto(wallet);
        }

        /// <summary>
        /// Trả về URL ảnh VietQR để nạp tiền vào ví
        /// </summary>
        public async Task<string> GetSePayTopUpQrUrlAsync(int userId, decimal amount)
        {
            // BUG-3 FIX: Validate amount
            if (amount < 10_000)
                throw new InvalidOperationException("Số tiền nạp tối thiểu là 10,000 VND.");
            if (amount > 50_000_000)
                throw new InvalidOperationException("Số tiền nạp tối đa là 50,000,000 VND.");

            var wallet = await GetOrCreateWalletInternalAsync(userId);
            
            var accountNumber = _configuration["SePay:AccountNumber"] ?? "YOUR_BANK_ACCOUNT";
            var bankCode = _configuration["SePay:BankCode"] ?? "YOUR_BANK_CODE"; 
            var intAmount = (int)amount;
            
            // Format nạp tiền ví: W{userId}
            var description = $"W{userId}";

            var qrUrl = $"https://img.vietqr.io/image/{bankCode}-{accountNumber}-compact.png?amount={intAmount}&addInfo={description}";

            return qrUrl;
        }

        /// <summary>
        /// Thanh toán booking bằng số dư ví
        /// </summary>
        public async Task<WalletDto> PayBookingByWalletAsync(int userId, int bookingId)
        {
            // Dùng transaction để đảm bảo tính nhất quán tài chính
            using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
            var wallet = await GetOrCreateWalletInternalAsync(userId);

            var booking = await _bookingRepo.GetByIdWithDetailsAsync(bookingId)
                ?? throw new InvalidOperationException("Booking không tồn tại.");

            if (booking.DriverUserId != userId)
                throw new UnauthorizedAccessException("Bạn không có quyền thanh toán booking này.");

            if (booking.Status != BookingStatus.PendingPayment)
                throw new InvalidOperationException("Booking không ở trạng thái chờ thanh toán.");

            if (booking.PaymentExpiresAt.HasValue && booking.PaymentExpiresAt.Value <= DateTimeHelper.VietnamNow())
                throw new InvalidOperationException("Đã hết thời gian thanh toán.");

            if (wallet.AvailableBalance < booking.TotalAmount)
                throw new InvalidOperationException(
                    $"Số dư ví không đủ. Cần {booking.TotalAmount:N0} VND, hiện có {wallet.AvailableBalance:N0} VND.");

            // BUG-1 FIX: Atomic SQL via Repository tránh race condition
            var rowsAffected = await _walletRepo.DeductIfSufficientAsync(wallet.Id, booking.TotalAmount);
            if (rowsAffected == 0)
                throw new InvalidOperationException("Số dư ví không đủ hoặc đã bị thay đổi bởi giao dịch khác (Kẹt ví).");

            // Cộng tiền vào ESCROW (Atomic via Repository)
            var escrowWallet = await _walletRepo.GetBySystemCodeAsync("ESCROW") 
                ?? throw new InvalidOperationException("Ví hệ thống ESCROW chưa được cấu hình. Vui lòng liên hệ Admin.");
            await _walletRepo.AdjustBalanceAtomicAsync(escrowWallet.Id, booking.TotalAmount, 0);

            // Ghi ledger: DEBIT từ ví Driver, CREDIT vào ESCROW
            var ledgerTx = new LedgerTransaction
            {
                ReferenceType = "BookingPayment",
                ReferenceId = bookingId,
                Memo = $"Thanh toán booking #{bookingId} bằng ví - {booking.TotalAmount:N0}đ → ESCROW",
                CreatedByUserId = userId,
                CreatedAt = DateTimeHelper.VietnamNow(),
                Entries = new List<LedgerEntry>
                {
                    new LedgerEntry
                    {
                        WalletId = wallet.Id,
                        Direction = LedgerDirection.Debit,
                        Amount = booking.TotalAmount,
                        CreatedAt = DateTimeHelper.VietnamNow()
                    },
                    new LedgerEntry
                    {
                        WalletId = escrowWallet?.Id ?? 1,
                        Direction = LedgerDirection.Credit,
                        Amount = booking.TotalAmount,
                        CreatedAt = DateTimeHelper.VietnamNow()
                    }
                }
            };
            _ledgerRepo.Add(ledgerTx);

            // Tạo Payment record
            var payment = await _paymentRepo.GetByBookingIdAsync(bookingId);
            if (payment == null)
            {
                payment = new Payment
                {
                    BookingId = bookingId,
                    Amount = booking.TotalAmount,
                    PaymentMethod = PaymentMethod.Wallet,
                    Status = PaymentStatus.Completed,
                    PaidAt = DateTimeHelper.VietnamNow(),
                    GatewayTxnRef = $"WALLET_{wallet.Id}_{DateTimeHelper.VietnamNow().Ticks}",
                    CreatedAt = DateTimeHelper.VietnamNow()
                };
                _paymentRepo.Add(payment);
            await _unitOfWork.CompleteAsync();
            }
            else
            {
                payment.Status = PaymentStatus.Completed;
                payment.PaymentMethod = PaymentMethod.Wallet;
                payment.PaidAt = DateTimeHelper.VietnamNow();
                _paymentRepo.Update(payment);
            await _unitOfWork.CompleteAsync();
            }

            // Set booking = Paid
            booking.Status = BookingStatus.Paid;
            _bookingRepo.Update(booking);
            await _unitOfWork.CompleteAsync();

            // Lưu ý: Tồn kho ExtraServices ĐÃ ĐƯỢC TRỪ tại thời điểm CreateBookingAsync (Reservation Pattern).
            // Do đó không thực hiện trừ stock ở đây nữa để tránh lỗi Double Deduction (Trừ đúp).

            // Lock slot
            var slot = await _slotRepo.GetByIdAsync(booking.SlotId, tracking: true);
            if (slot != null)
            {
                slot.Status = SlotStatus.Booked;
                _slotRepo.Update(slot);
                await _unitOfWork.CompleteAsync();
            }

            // Notify
            await _notificationService.SendAsync(
                userId,
                "Thanh toán thành công",
                $"Thanh toán {booking.TotalAmount:N0}đ bằng ví cho slot {booking.ChargingSlot?.SlotName} — trạm {booking.ChargingSlot?.ChargingStation?.Name} ({booking.StartTime:HH:mm} - {booking.EndTime:HH:mm dd/MM}) thành công. Slot đã được giữ cho bạn.",
                NotificationType.Payment);

            // Notify Owner: Driver đã thanh toán
            var ownerUserId = booking.ChargingSlot?.ChargingStation?.OwnerUserId;
            if (ownerUserId.HasValue)
            {
                await _notificationService.SendAsync(
                    ownerUserId.Value,
                    "Khách đã thanh toán",
                    $"Slot {booking.ChargingSlot?.SlotName} — trạm {booking.ChargingSlot?.ChargingStation?.Name} ({booking.StartTime:HH:mm} - {booking.EndTime:HH:mm dd/MM}) đã được thanh toán {booking.TotalAmount:N0}đ bằng ví. Chờ Driver check-in.",
                    NotificationType.Payment);
            }

            await transaction.CommitAsync();
            return MapToDto(wallet);
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        /// <summary>
        /// Rút tiền từ ví (tạo yêu cầu + freeze tiền)
        /// </summary>
        public async Task<WithdrawRequestDto> WithdrawAsync(int userId, WithdrawDto dto)
        {
            // Validate withdraw amount
            if (dto.Amount < 50_000)
                throw new InvalidOperationException("Số tiền rút tối thiểu là 50,000 VND.");

            // Hệ thống chỉ cho Owner rút tiền, và Owner phải KYC thành công (Chống Rửa Tiền - AML).
            var owner = await _ownerRepo.GetByUserIdAsync(userId);
            if (owner != null && owner.KycStatus != ChargeSlot.Api.Enums.KycStatus.Approved)
                throw new InvalidOperationException("Chức năng Rút tiền tạm khóa. Vui lòng hoàn tất xác minh danh tính (KYC) để tiếp tục.");

            using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                var wallet = await GetOrCreateWalletInternalAsync(userId);

            if (wallet.AvailableBalance < dto.Amount)
                throw new InvalidOperationException(
                    $"Số dư không đủ. Hiện có {wallet.AvailableBalance:N0} VND.");

            // Atomic SQL via Repository: chống race condition khi rút tiền 2 lần cùng lúc
            var rowsAffected = await _walletRepo.FreezeIfSufficientAsync(wallet.Id, dto.Amount);
            if (rowsAffected == 0)
                throw new InvalidOperationException("Số dư không đủ hoặc đã bị thay đổi bởi giao dịch khác.");

            // Tạo WithdrawRequest với snapshot thông tin ngân hàng
            var request = new WithdrawRequest
            {
                UserId = userId,
                WalletId = wallet.Id,
                Amount = dto.Amount,
                BankName = dto.BankName,
                BankAccountNumber = dto.BankAccountNumber,
                BankAccountHolder = dto.BankAccountHolder,
                UserNote = dto.UserNote,
                Status = WithdrawStatus.Pending,
                RequestedAt = DateTimeHelper.VietnamNow()
            };
            _withdrawRepo.Add(request);
            await _unitOfWork.CompleteAsync();

            // Ghi ledger double-entry: DEBIT từ ví user (available → frozen)
            var clearingWallet = await _walletRepo.GetBySystemCodeAsync("CLEARING") 
                ?? throw new InvalidOperationException("Ví hệ thống CLEARING chưa được cấu hình.");
            var ledgerTx = new LedgerTransaction
            {
                ReferenceType = "WithdrawRequest",
                ReferenceId = request.Id,
                Memo = $"Yêu cầu rút {dto.Amount:N0} VND → {dto.BankName} - {dto.BankAccountNumber}",
                CreatedByUserId = userId,
                CreatedAt = DateTimeHelper.VietnamNow(),
                Entries = new List<LedgerEntry>
                {
                    new LedgerEntry
                    {
                        WalletId = wallet.Id,
                        Direction = LedgerDirection.Debit,
                        Amount = dto.Amount,
                        CreatedAt = DateTimeHelper.VietnamNow()
                    },
                    new LedgerEntry
                    {
                        WalletId = clearingWallet.Id,
                        Direction = LedgerDirection.Credit,
                        Amount = dto.Amount,
                        CreatedAt = DateTimeHelper.VietnamNow()
                    }
                }
            };
            _ledgerRepo.Add(ledgerTx);
            await _unitOfWork.CompleteAsync();

            await transaction.CommitAsync();

            // Load user name
            var user = await _userManager.FindByIdAsync(userId.ToString());

            await _notificationService.SendAsync(
                userId,
                "Yêu cầu rút tiền",
                $"Yêu cầu rút {dto.Amount:N0} VND → {dto.BankName} ({dto.BankAccountNumber}) đã được gửi. Vui lòng chờ Admin xử lý.",
                NotificationType.Wallet);

            // Notify all Admins về yêu cầu rút tiền mới
            var adminUsers = await _userManager.GetUsersInRoleAsync(Constants.RoleConstants.Admin);
            foreach (var admin in adminUsers)
            {
                await _notificationService.SendAsync(
                    admin.Id,
                    "Yêu cầu rút tiền mới",
                    $"User {user?.FullName ?? userId.ToString()} yêu cầu rút {dto.Amount:N0} VND → {dto.BankName} ({dto.BankAccountNumber}). Vui lòng xử lý.",
                    NotificationType.Wallet);
            }

            // Load user name for DTO
            return MapToWithdrawDto(request, user?.FullName);
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        /// <summary>
        /// User xem danh sách yêu cầu rút tiền của mình
        /// </summary>
        public async Task<List<WithdrawRequestDto>> GetUserWithdrawRequestsAsync(int userId)
        {
            var requests = await _withdrawRepo.GetByUserIdAsync(userId);
            var user = await _userManager.FindByIdAsync(userId.ToString());
            return requests.Select(r => MapToWithdrawDto(r, user?.FullName)).ToList();
        }

        public async Task<ChargeSlot.Api.DTOs.PagedResultDto<WithdrawRequestDto>> GetUserWithdrawRequestsPagedAsync(int userId, int page, int pageSize)
        {
            var result = await _withdrawRepo.GetByUserIdPagedAsync(userId, page, pageSize);
            var user = await _userManager.FindByIdAsync(userId.ToString());
            return new ChargeSlot.Api.DTOs.PagedResultDto<WithdrawRequestDto>
            {
                Page = page,
                PageSize = pageSize,
                TotalItems = result.TotalCount,
                Items = result.Items.Select(r => MapToWithdrawDto(r, user?.FullName)).ToList()
            };
        }

        /// <summary>
        /// Admin xem tất cả yêu cầu rút tiền pending
        /// </summary>
        public async Task<List<WithdrawRequestDto>> GetAllPendingWithdrawsAsync()
        {
            var requests = await _withdrawRepo.GetPendingAsync();
            return requests.Select(r => MapToWithdrawDto(r, r.User?.FullName)).ToList();
        }

        public async Task<ChargeSlot.Api.DTOs.PagedResultDto<WithdrawRequestDto>> GetAllPendingWithdrawsPagedAsync(int page, int pageSize)
        {
            var result = await _withdrawRepo.GetPendingPagedAsync(page, pageSize);
            return new ChargeSlot.Api.DTOs.PagedResultDto<WithdrawRequestDto>
            {
                Page = page,
                PageSize = pageSize,
                TotalItems = result.TotalCount,
                Items = result.Items.Select(r => MapToWithdrawDto(r, r.User?.FullName)).ToList()
            };
        }

        /// <summary>
        /// Admin duyệt / từ chối yêu cầu rút tiền.
        /// Approve chỉ đổi trạng thái, CHƯA trừ tiền (tiền vẫn frozen).
        /// Tiền chỉ rời hệ thống khi Completed.
        /// </summary>
        public async Task<WithdrawRequestDto> ProcessWithdrawAsync(int adminUserId, int requestId, ProcessWithdrawDto dto)
        {
            var request = await _withdrawRepo.GetByIdWithUserAndWalletAsync(requestId)
                ?? throw new InvalidOperationException("Yêu cầu rút tiền không tồn tại.");

            if (request.Status != WithdrawStatus.Pending)
                throw new InvalidOperationException("Yêu cầu đã được xử lý trước đó.");

            var wallet = request.Wallet;

            if (dto.Approve)
            {
                // Approve: chỉ đổi trạng thái, tiền vẫn frozen.
                // Admin cần chuyển khoản thật rồi upload ảnh bill → TransferCompleted.
                request.Status = WithdrawStatus.Approved;

                await _notificationService.SendAsync(
                    request.UserId,
                    "Yêu cầu rút tiền đã được duyệt",
                    $"Yêu cầu rút {request.Amount:N0} VND đã được duyệt. Admin sẽ chuyển khoản trong thời gian sớm nhất." +
                    (string.IsNullOrEmpty(dto.AdminNote) ? "" : $" Ghi chú: {dto.AdminNote}"),
                    NotificationType.Wallet);
            }
            else
            {
                // Reject: trả lại tiền frozen → available (Atomic via Repository)
                await _walletRepo.UnfreezeAtomicAsync(wallet.Id, request.Amount);
                request.Status = WithdrawStatus.Rejected;

                await _notificationService.SendAsync(
                    request.UserId,
                    "Yêu cầu rút tiền bị từ chối",
                    $"Yêu cầu rút {request.Amount:N0} VND đã bị từ chối. Tiền đã được hoàn lại ví." +
                    (string.IsNullOrEmpty(dto.AdminNote) ? "" : $" Lý do: {dto.AdminNote}"),
                    NotificationType.System);
            }

            request.ProcessedAt = DateTimeHelper.VietnamNow();
            request.ProcessedByUserId = adminUserId;
            request.AdminNote = dto.AdminNote;

            _withdrawRepo.Update(request);
            await _unitOfWork.CompleteAsync();

            return MapToWithdrawDto(request, request.User?.FullName);
        }

        /// <summary>Admin xem tất cả yêu cầu rút tiền (mọi trạng thái).</summary>
        public async Task<List<WithdrawRequestDto>> GetAllWithdrawsAsync()
        {
            var requests = await _withdrawRepo.GetAllWithUserAsync();
            return requests.Select(r => MapToWithdrawDto(r, r.User?.FullName)).ToList();
        }

        public async Task<ChargeSlot.Api.DTOs.PagedResultDto<WithdrawRequestDto>> GetAllWithdrawsPagedAsync(int page, int pageSize)
        {
            var result = await _withdrawRepo.GetAllWithUserPagedAsync(page, pageSize);
            return new ChargeSlot.Api.DTOs.PagedResultDto<WithdrawRequestDto>
            {
                Page = page,
                PageSize = pageSize,
                TotalItems = result.TotalCount,
                Items = result.Items.Select(r => MapToWithdrawDto(r, r.User?.FullName)).ToList()
            };
        }

        /// <summary>
        /// Admin đã chuyển khoản thật → upload ảnh biên lai → TransferCompleted.
        /// </summary>
        public async Task<WithdrawRequestDto> ConfirmTransferAsync(int adminUserId, int requestId, IFormFile receiptImage)
        {
            var request = await _withdrawRepo.GetByIdWithUserAsync(requestId)
                ?? throw new InvalidOperationException("Yêu cầu rút tiền không tồn tại.");

            if (request.Status != WithdrawStatus.Approved)
                throw new InvalidOperationException($"Chỉ có thể xác nhận chuyển khoản khi trạng thái là Approved. Hiện tại: {request.Status}");

            // Upload ảnh biên lai lên Firebase
            var receiptUrl = await _fileStorageService.UploadAsync(receiptImage, $"withdraws/{requestId}");

            request.TransferReceiptUrl = receiptUrl;
            request.TransferredAt = DateTimeHelper.VietnamNow();
            request.Status = WithdrawStatus.TransferCompleted;

            _withdrawRepo.Update(request);
            await _unitOfWork.CompleteAsync();

            await _notificationService.SendAsync(
                request.UserId,
                "Tiền đã được chuyển khoản",
                $"Admin đã chuyển {request.Amount:N0} VND vào tài khoản {request.BankName} ({request.BankAccountNumber}). " +
                $"Vui lòng kiểm tra và xác nhận đã nhận tiền trong 24 giờ.",
                NotificationType.Wallet);

            return MapToWithdrawDto(request, request.User?.FullName);
        }

        /// <summary>
        /// User xác nhận đã nhận tiền → Completed (trừ frozen + ghi ledger).
        /// </summary>
        public async Task<WithdrawRequestDto> UserConfirmReceivedAsync(int userId, int requestId)
        {
            var request = await _withdrawRepo.GetByIdWithUserAndWalletAsync(requestId)
                ?? throw new InvalidOperationException("Yêu cầu rút tiền không tồn tại.");

            if (request.UserId != userId)
                throw new UnauthorizedAccessException("Yêu cầu này không thuộc về bạn.");

            if (request.Status != WithdrawStatus.TransferCompleted)
                throw new InvalidOperationException($"Chỉ xác nhận khi trạng thái là TransferCompleted. Hiện tại: {request.Status}");

            request.UserConfirmedAt = DateTimeHelper.VietnamNow();
            await FinalizeWithdrawCompletedAsync(request, userId);

            return MapToWithdrawDto(request, request.User?.FullName);
        }

        /// <summary>
        /// User báo chưa nhận được tiền → IssueReported.
        /// </summary>
        public async Task<WithdrawRequestDto> UserReportIssueAsync(int userId, int requestId, string issueNote)
        {
            var request = await _withdrawRepo.GetByIdWithUserAsync(requestId)
                ?? throw new InvalidOperationException("Yêu cầu rút tiền không tồn tại.");

            if (request.UserId != userId)
                throw new UnauthorizedAccessException("Yêu cầu này không thuộc về bạn.");

            if (request.Status != WithdrawStatus.TransferCompleted)
                throw new InvalidOperationException($"Chỉ báo lỗi khi trạng thái là TransferCompleted. Hiện tại: {request.Status}");

            request.Status = WithdrawStatus.IssueReported;
            request.IssueReportedAt = DateTimeHelper.VietnamNow();
            request.IssueNote = issueNote;

            _withdrawRepo.Update(request);
            await _unitOfWork.CompleteAsync();

            // Notify admins
            var adminUsers = await _userManager.GetUsersInRoleAsync(Constants.RoleConstants.Admin);
            foreach (var admin in adminUsers)
            {
                await _notificationService.SendAsync(
                    admin.Id,
                    "Vấn đề rút tiền",
                    $"User {request.User?.FullName ?? userId.ToString()} báo chưa nhận được {request.Amount:N0} VND. Lý do: {issueNote}",
                    NotificationType.Wallet);
            }

            return MapToWithdrawDto(request, request.User?.FullName);
        }

        /// <summary>
        /// Admin xử lý issue: refund=true → hoàn tiền (Rejected), refund=false → chuyển lại (TransferCompleted).
        /// </summary>
        public async Task<WithdrawRequestDto> AdminResolveIssueAsync(int adminUserId, int requestId, bool refund, string? note)
        {
            var request = await _withdrawRepo.GetByIdWithUserAndWalletAsync(requestId)
                ?? throw new InvalidOperationException("Yêu cầu rút tiền không tồn tại.");

            if (request.Status != WithdrawStatus.IssueReported)
                throw new InvalidOperationException($"Chỉ xử lý issue khi trạng thái là IssueReported. Hiện tại: {request.Status}");

            if (!string.IsNullOrEmpty(note))
                request.AdminNote = (request.AdminNote ?? "") + $"\n[Resolve] {note}";

            if (refund)
            {
                // Hoàn tiền: frozen → available, status = Rejected (Atomic via Repository)
                var wallet = request.Wallet;
                await _walletRepo.UnfreezeAtomicAsync(wallet.Id, request.Amount);
                request.Status = WithdrawStatus.Rejected;

                await _notificationService.SendAsync(
                    request.UserId,
                    "Hoàn tiền rút về ví",
                    $"Admin đã hoàn {request.Amount:N0} VND về ví của bạn do không chuyển khoản thành công.",
                    NotificationType.Wallet);
            }
            else
            {
                // Admin đã chuyển lại → cho user xác nhận lần nữa
                request.Status = WithdrawStatus.TransferCompleted;
                request.TransferredAt = DateTimeHelper.VietnamNow(); // reset timer 24h

                await _notificationService.SendAsync(
                    request.UserId,
                    "Admin đã chuyển khoản lại",
                    $"Admin đã xử lý vấn đề và chuyển lại {request.Amount:N0} VND. Vui lòng kiểm tra lại tài khoản.",
                    NotificationType.Wallet);
            }

            _withdrawRepo.Update(request);
            await _unitOfWork.CompleteAsync();

            return MapToWithdrawDto(request, request.User?.FullName);
        }

        /// <summary>
        /// Admin ép hoàn tất rút tiền khi User spam báo lỗi giả.
        /// Admin upload ảnh bằng chứng chuyển khoản thành công → force Completed.
        /// </summary>
        public async Task<WithdrawRequestDto> AdminForceCompleteAsync(int adminUserId, int requestId, IFormFile evidenceImage, string? note)
        {
            var request = await _withdrawRepo.GetByIdWithUserAndWalletAsync(requestId)
                ?? throw new InvalidOperationException("Yêu cầu rút tiền không tồn tại.");

            if (request.Status != WithdrawStatus.IssueReported)
                throw new InvalidOperationException($"Chỉ có thể ép hoàn tất khi trạng thái là IssueReported. Hiện tại: {request.Status}");

            // Upload ảnh bằng chứng
            var evidenceUrl = await _fileStorageService.UploadAsync(evidenceImage, $"withdraws/{requestId}/evidence");

            request.AdminNote = (request.AdminNote ?? "") + $"\n[ForceComplete] Admin đã xác minh chuyển khoản thành công. Bằng chứng: {evidenceUrl}" +
                (string.IsNullOrEmpty(note) ? "" : $" | Ghi chú: {note}");

            // Force complete: trừ frozen + ghi ledger
            await FinalizeWithdrawCompletedAsync(request, adminUserId);

            await _notificationService.SendAsync(
                request.UserId,
                "Rút tiền đã hoàn tất (Admin xác nhận)",
                $"Yêu cầu rút {request.Amount:N0} VND đã được Admin xác nhận hoàn tất sau khi điều tra. " +
                "Nếu có thắc mắc, vui lòng liên hệ hỗ trợ.",
                NotificationType.Wallet);

            return MapToWithdrawDto(request, request.User?.FullName);
        }

        /// <summary>
        /// Hoàn tất rút tiền: trừ frozen + ghi ledger (tiền rời hệ thống).
        /// Được gọi bởi UserConfirmReceivedAsync hoặc WithdrawAutoConfirmJob.
        /// </summary>
        public async Task FinalizeWithdrawCompletedAsync(WithdrawRequest request, int? confirmedByUserId = null)
        {
            var wallet = request.Wallet ?? await _walletRepo.GetByIdAsync(request.WalletId)
                ?? throw new InvalidOperationException("Không tìm thấy ví tương ứng để cập nhật.");

            // Trừ frozen balance (Atomic via Repository)
            await _walletRepo.AdjustBalanceAtomicAsync(wallet.Id, 0, -request.Amount);
            request.Status = WithdrawStatus.Completed;
            if (request.UserConfirmedAt == null)
                request.UserConfirmedAt = DateTimeHelper.VietnamNow(); // auto-confirm

            // Ghi ledger: DEBIT CLEARING → out (tiền rời hệ thống)
            var clearingWallet = await _walletRepo.GetBySystemCodeAsync("CLEARING")
                ?? throw new InvalidOperationException("Ví hệ thống CLEARING chưa được cấu hình.");
            await _walletRepo.AdjustBalanceAtomicAsync(clearingWallet.Id, -request.Amount, 0);

            var ledgerTx = new LedgerTransaction
            {
                ReferenceType = "WithdrawCompleted",
                ReferenceId = request.Id,
                Memo = $"Rút {request.Amount:N0} VND → {request.BankName} - {request.BankAccountNumber} (hoàn tất)",
                CreatedByUserId = confirmedByUserId,
                CreatedAt = DateTimeHelper.VietnamNow(),
                Entries = new List<LedgerEntry>
                {
                    new LedgerEntry
                    {
                        WalletId = clearingWallet.Id,
                        Direction = LedgerDirection.Debit,
                        Amount = request.Amount,
                        CreatedAt = DateTimeHelper.VietnamNow()
                    }
                }
            };
            _ledgerRepo.Add(ledgerTx);
            _withdrawRepo.Update(request);
            await _unitOfWork.CompleteAsync();
        }

        /// <summary>
        /// Lịch sử giao dịch ví
        /// </summary>
        public async Task<List<TransactionHistoryDto>> GetTransactionHistoryAsync(int userId)
        {
            var wallet = await GetOrCreateWalletInternalAsync(userId);
            var entries = await _walletRepo.GetTransactionHistoryAsync(wallet.Id);

            return entries.Select(e => new TransactionHistoryDto
            {
                Id = e.Id,
                Type = e.LedgerTransaction?.ReferenceType ?? "",
                Direction = e.Direction.ToString(),
                Amount = e.Amount,
                Memo = e.LedgerTransaction?.Memo,
                CreatedAt = e.CreatedAt
            }).ToList();
        }

        private async Task<Wallet> GetOrCreateWalletInternalAsync(int userId)
        {
            var wallet = await _walletRepo.GetByUserIdAsync(userId);
            if (wallet == null)
            {
                // Detect WalletType từ role thực tế của user
                var user = await _userManager.FindByIdAsync(userId.ToString());
                var walletType = WalletType.Driver;
                if (user != null)
                {
                    var roles = await _userManager.GetRolesAsync(user);
                    if (roles.Contains(Constants.RoleConstants.Owner))
                        walletType = WalletType.Owner;
                }

                wallet = new Wallet
                {
                    UserId = userId,
                    WalletType = walletType,
                    AvailableBalance = 0,
                    FrozenBalance = 0,
                    CreatedAt = DateTimeHelper.VietnamNow()
                };
                _walletRepo.Add(wallet);
            await _unitOfWork.CompleteAsync();
            }
            return wallet;
        }

        private static WalletDto MapToDto(Wallet w)
        {
            return new WalletDto
            {
                Id = w.Id,
                UserId = w.UserId,
                OwnerName = w.WalletType == Enums.WalletType.System
                    ? w.SystemCode switch
                    {
                        "ESCROW" => "Hệ thống (Escrow)",
                        "PLATFORM_REVENUE" => "Hệ thống (Platform Revenue)",
                        "TAX_HOLD" => "Hệ thống (Tax Hold)",
                        "CLEARING" => "Hệ thống (Clearing)",
                        _ => $"Hệ thống ({w.SystemCode})"
                    }
                    : (w.User?.FullName ?? "N/A"),
                SystemCode = w.SystemCode,
                AvailableBalance = w.AvailableBalance,
                FrozenBalance = w.FrozenBalance,
                WalletType = w.WalletType.ToString(),
                CreatedAt = w.CreatedAt
            };
        }

        private static WithdrawRequestDto MapToWithdrawDto(WithdrawRequest r, string? userFullName)
        {
            return new WithdrawRequestDto
            {
                Id = r.Id,
                UserId = r.UserId,
                UserFullName = userFullName,
                Amount = r.Amount,
                BankName = r.BankName,
                BankAccountNumber = r.BankAccountNumber,
                BankAccountHolder = r.BankAccountHolder,
                Status = r.Status.ToString(),
                RequestedAt = r.RequestedAt,
                ProcessedAt = r.ProcessedAt,
                AdminNote = r.AdminNote,
                UserNote = r.UserNote,
                TransferReceiptUrl = r.TransferReceiptUrl,
                TransferredAt = r.TransferredAt,
                UserConfirmedAt = r.UserConfirmedAt,
                IssueReportedAt = r.IssueReportedAt,
                IssueNote = r.IssueNote
            };
        }

        public async Task<ChargeSlot.Api.DTOs.PagedResultDto<WalletDto>> GetAdminAllWalletsAsync(ChargeSlot.Api.DTOs.Admin.Overview.WalletFilterDto filter)
        {
            var result = await _walletRepo.GetAdminAllWalletsAsync(filter);
            
            return new ChargeSlot.Api.DTOs.PagedResultDto<WalletDto>
            {
                Items = result.Items.Select(MapToDto).ToList(),
                TotalItems = result.TotalCount,
                Page = filter.Page,
                PageSize = filter.PageSize
            };
        }

        public async Task<ChargeSlot.Api.DTOs.PagedResultDto<TransactionHistoryDto>> GetAdminWalletTransactionsAsync(int walletId, ChargeSlot.Api.DTOs.Admin.Overview.TransactionFilterDto filter)
        {
            var result = await _ledgerRepo.GetAdminWalletTransactionsAsync(walletId, filter);

            var dtoList = result.Items.Select(e => new TransactionHistoryDto
            {
                Id = e.LedgerTransactionId,
                Type = e.LedgerTransaction?.ReferenceType ?? "Transfer",
                Direction = e.Direction.ToString(),
                Amount = e.Amount,
                Memo = e.LedgerTransaction?.Memo,
                CreatedAt = e.CreatedAt
            }).ToList();

            return new ChargeSlot.Api.DTOs.PagedResultDto<TransactionHistoryDto>
            {
                Items = dtoList,
                TotalItems = result.TotalCount,
                Page = filter.Page,
                PageSize = filter.PageSize
            };
        }

        public async Task<ChargeSlot.Api.DTOs.Admin.Overview.TransactionDetailDto?> GetAdminTransactionDetailAsync(long transactionId)
        {
            var tx = await _ledgerRepo.GetTransactionDetailAsync(transactionId);
            if (tx == null) return null;

            var dto = new ChargeSlot.Api.DTOs.Admin.Overview.TransactionDetailDto
            {
                Id = tx.Id,
                ReferenceType = tx.ReferenceType,
                ReferenceId = tx.ReferenceId,
                Memo = tx.Memo ?? string.Empty,
                CreatedAt = tx.CreatedAt,
                Entries = tx.Entries.Select(e => new ChargeSlot.Api.DTOs.Admin.Overview.LedgerEntryDetailDto
                {
                    WalletId = e.WalletId,
                    WalletType = e.Wallet?.WalletType.ToString() ?? "Unknown",
                    OwnerName = e.Wallet?.WalletType == Enums.WalletType.System 
                        ? e.Wallet.SystemCode switch
                        {
                            "ESCROW" => "Hệ thống (Escrow)",
                            "PLATFORM_REVENUE" => "Hệ thống (Platform Revenue)",
                            "TAX_HOLD" => "Hệ thống (Tax Hold)",
                            "CLEARING" => "Hệ thống (Clearing)",
                            _ => $"Hệ thống ({e.Wallet.SystemCode})"
                        }
                        : (e.Wallet?.User?.FullName ?? "N/A"),
                    Direction = e.Direction.ToString(),
                    Amount = e.Amount
                }).ToList()
            };

            return dto;
        }
    }
}



