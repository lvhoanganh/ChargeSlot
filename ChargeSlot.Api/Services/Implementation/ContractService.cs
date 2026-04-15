using System.Web;
using ChargeSlot.Api.DTOs.Contract;
using ChargeSlot.Api.Enums;
using ChargeSlot.Api.Helpers;
using ChargeSlot.Api.Models;
using ChargeSlot.Api.Repositories.Interfaces;
using ChargeSlot.Api.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using ChargeSlot.Api.Models.Identity;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ChargeSlot.Api.Services.Implementation
{
    public class ContractService : IContractService
    {
        private readonly IContractRepository _contractRepo;
        private readonly IOwnerRepository _ownerRepo;
        private readonly IChargingStationRepository _stationRepo;
        private readonly IBookingRepository _bookingRepo;
        private readonly IBookingService _bookingService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IFileStorageService _fileStorageService;
        private readonly INotificationService _notificationService;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<ContractService> _logger;

        public ContractService(
            IContractRepository contractRepo,
            IOwnerRepository ownerRepo,
            IChargingStationRepository stationRepo,
            IBookingRepository bookingRepo,
            IBookingService bookingService,
            UserManager<ApplicationUser> userManager,
            IFileStorageService fileStorageService,
            INotificationService notificationService,
            IHttpClientFactory httpClientFactory,
            IUnitOfWork unitOfWork,
            ILogger<ContractService> logger)
        {
            _contractRepo = contractRepo;
            _ownerRepo = ownerRepo;
            _stationRepo = stationRepo;
            _bookingRepo = bookingRepo;
            _bookingService = bookingService;
            _userManager = userManager;
            _fileStorageService = fileStorageService;
            _notificationService = notificationService;
            _httpClientFactory = httpClientFactory;
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        // ═══════════════════════════════════════════════════
        // CREATE — Gọi tự động sau KYC Approved
        // ═══════════════════════════════════════════════════

        /// <summary>
        /// Tạo hoặc cập nhật hợp đồng sau khi KYC được Approved.
        /// LƯU Ý: Hàm này KHÔNG gọi CompleteAsync() — caller phải tự commit.
        /// </summary>
        public async Task CreateContractAsync(int ownerUserId)
        {
            var owner = await _ownerRepo.GetByUserIdAsync(ownerUserId)
                ?? throw new InvalidOperationException("Owner không tồn tại.");

            var user = await _userManager.FindByIdAsync(ownerUserId.ToString())
                ?? throw new InvalidOperationException("User không tồn tại.");

            // Kiểm tra đã có hợp đồng chưa
            var existing = await _contractRepo.GetByOwnerAsync(ownerUserId);

            if (existing != null && existing.Status == ContractStatus.Pending)
            {
                // Bug #3 fix: Hợp đồng Pending → cập nhật PII mới nhất thay vì bỏ qua
                existing.OwnerName = user.FullName ?? owner.BusinessName;
                existing.OwnerIdCard = owner.IdCardNumber ?? "Chưa cung cấp";
                existing.OwnerTaxCode = owner.TaxCode ?? "Chưa cung cấp";
                existing.OwnerAddress = owner.Address ?? "Chưa cung cấp";
                existing.OwnerBusinessLicense = owner.BusinessLicenseNumber ?? "Chưa cung cấp";
                existing.OwnerPhone = user.PhoneNumber ?? user.UserName ?? "";
                existing.OwnerEmail = user.Email ?? "Chưa cung cấp";
                _contractRepo.Update(existing);

                _logger.LogInformation("[Contract] Updated PII for existing Pending contract {ContractNumber} of Owner {UserId}",
                    existing.ContractNumber, ownerUserId);
                return;
            }

            if (existing != null && existing.Status == ContractStatus.Signed)
            {
                _logger.LogInformation("[Contract] Owner {UserId} already has signed contract {ContractNumber}",
                    ownerUserId, existing.ContractNumber);
                return;
            }

            // Bug #5 fix: Nếu contract gần nhất bị Admin terminate (vi phạm) → chặn tạo mới
            if (existing != null && existing.Status == ContractStatus.Terminated
                && existing.TerminationReason != null
                && !existing.TerminationReason.StartsWith("[Owner yêu cầu]"))
            {
                _logger.LogWarning(
                    "[Contract] Blocked auto-create for Owner {UserId}: previous contract {ContractNumber} was terminated by Admin.",
                    ownerUserId, existing.ContractNumber);
                return;
            }

            // Sinh số hợp đồng: CS-{năm}-{MaxId + 1} — tránh race condition với CountAsync
            var maxId = await _contractRepo.GetMaxIdAsync();
            var now = DateTimeHelper.VietnamNow();
            var contractNumber = $"CS-{now.Year}-{(maxId + 1):D4}";

            var contract = new Contract
            {
                OwnerUserId = ownerUserId,
                ContractNumber = contractNumber,
                Status = ContractStatus.Pending,

                // Snapshot PII
                OwnerName = user.FullName ?? owner.BusinessName,
                OwnerIdCard = owner.IdCardNumber ?? "Chưa cung cấp",
                OwnerTaxCode = owner.TaxCode ?? "Chưa cung cấp",
                OwnerAddress = owner.Address ?? "Chưa cung cấp",
                OwnerBusinessLicense = owner.BusinessLicenseNumber ?? "Chưa cung cấp",
                OwnerPhone = user.PhoneNumber ?? user.UserName ?? "",
                OwnerEmail = user.Email ?? "Chưa cung cấp",

                ContractDurationMonths = 12,
                CreatedAt = now
                // ExpiresAt sẽ được set khi Owner ký hợp đồng
            };

            await _contractRepo.AddAsync(contract);
            // Bug #4 fix: Không gọi CompleteAsync() — để caller (KycService) gộp vào 1 transaction

            _logger.LogInformation("[Contract] Created contract {ContractNumber} for Owner {UserId}", contractNumber, ownerUserId);
        }

        // ═══════════════════════════════════════════════════
        // PREVIEW — Owner xem nội dung hợp đồng
        // ═══════════════════════════════════════════════════

        public async Task<ContractPreviewDto> GetContractPreviewAsync(int ownerUserId)
        {
            var contract = await _contractRepo.GetByOwnerAsync(ownerUserId)
                ?? throw new InvalidOperationException("Chưa có hợp đồng. Vui lòng hoàn thành KYC trước.");

            return MapToPreviewDto(contract);
        }

        // ═══════════════════════════════════════════════════
        // SIGN — Owner ký hợp đồng bằng chữ ký tay
        // ═══════════════════════════════════════════════════

        public async Task<ContractPreviewDto> SignContractAsync(int ownerUserId, SignContractDto dto)
        {
            var contract = await _contractRepo.GetByOwnerAsync(ownerUserId)
                ?? throw new InvalidOperationException("Chưa có hợp đồng.");

            if (contract.Status == ContractStatus.Signed)
                throw new InvalidOperationException("Hợp đồng đã được ký trước đó.");

            if (contract.Status != ContractStatus.Pending)
                throw new InvalidOperationException($"Không thể ký hợp đồng ở trạng thái {contract.Status}.");

            // 1. Decode base64 signature
            var signatureBytes = DecodeBase64Image(dto.SignatureBase64);

            // 2. Upload signature image to Firebase
            var signatureUrl = await _fileStorageService.UploadBytesAsync(
                signatureBytes,
                $"contracts/{ownerUserId}",
                $"signature_{contract.Id}.png",
                "image/png");

            // 3. Generate PDF with signature embedded
            var pdfBytes = GenerateContractPdf(contract, signatureBytes);

            // 4. Upload PDF to Firebase
            var pdfUrl = await _fileStorageService.UploadBytesAsync(
                pdfBytes,
                $"contracts/{ownerUserId}",
                $"contract_{contract.ContractNumber}.pdf",
                "application/pdf");

            // 5. Update contract
            var now = DateTimeHelper.VietnamNow();
            contract.SignatureImageUrl = signatureUrl;
            contract.SignedPdfUrl = pdfUrl;
            contract.SignedAt = now;
            contract.ExpiresAt = now.AddMonths(contract.ContractDurationMonths);
            contract.Status = ContractStatus.Signed;
            _contractRepo.Update(contract);
            await _unitOfWork.CompleteAsync();

            _logger.LogInformation("[Contract] Owner {UserId} signed contract {ContractNumber}", ownerUserId, contract.ContractNumber);

            return MapToPreviewDto(contract);
        }

        // ═══════════════════════════════════════════════════
        // DOWNLOAD — Trả PDF đã ký
        // ═══════════════════════════════════════════════════

        public async Task<byte[]> DownloadContractPdfAsync(int ownerUserId)
        {
            var contract = await _contractRepo.GetByOwnerAsync(ownerUserId)
                ?? throw new InvalidOperationException("Chưa có hợp đồng.");

            // Nếu đã ký → lấy chữ ký embed vào PDF, chưa ký → trả preview PDF (không chữ ký)
            byte[]? signatureBytes = null;
            if (contract.Status == ContractStatus.Signed && !string.IsNullOrEmpty(contract.SignatureImageUrl))
                signatureBytes = await DownloadImageFromUrl(contract.SignatureImageUrl);
            return GenerateContractPdf(contract, signatureBytes);
        }

        // ═══════════════════════════════════════════════════
        // ADMIN
        // ═══════════════════════════════════════════════════

        public async Task<ContractPreviewDto?> GetContractByOwnerForAdminAsync(int ownerUserId)
        {
            var contract = await _contractRepo.GetByOwnerAsync(ownerUserId);
            return contract == null ? null : MapToPreviewDto(contract);
        }

        public async Task<List<ContractPreviewDto>> GetAllContractsAsync(string? status = null)
        {
            ContractStatus? parsed = null;
            if (!string.IsNullOrEmpty(status) && Enum.TryParse<ContractStatus>(status, true, out var s))
                parsed = s;

            var contracts = await _contractRepo.GetAllAsync(parsed);
            return contracts.Select(c => new ContractPreviewDto
            {
                ContractId = c.Id,
                ContractNumber = c.ContractNumber,
                OwnerName = c.OwnerName,
                OwnerUserId = c.OwnerUserId,
                Status = c.Status.ToString(),
                ContractHtml = "", // Không trả HTML cho list
                CreatedAt = c.CreatedAt,
                SignedAt = c.SignedAt,
                ExpiresAt = c.ExpiresAt,
                ContractDurationMonths = c.ContractDurationMonths,
                SignedPdfUrl = c.SignedPdfUrl
            }).ToList();
        }

        public async Task<ChargeSlot.Api.DTOs.PagedResultDto<ContractPreviewDto>> GetAllContractsPagedAsync(string? status, int page, int pageSize)
        {
            ContractStatus? parsed = null;
            if (!string.IsNullOrEmpty(status) && Enum.TryParse<ContractStatus>(status, true, out var s))
                parsed = s;

            var result = await _contractRepo.GetAllPagedAsync(parsed, page, pageSize);

            var items = result.Items.Select(c => new ContractPreviewDto
            {
                ContractId = c.Id,
                ContractNumber = c.ContractNumber,
                OwnerName = c.OwnerName,
                OwnerUserId = c.OwnerUserId,
                Status = c.Status.ToString(),
                ContractHtml = "",
                CreatedAt = c.CreatedAt,
                SignedAt = c.SignedAt,
                ExpiresAt = c.ExpiresAt,
                ContractDurationMonths = c.ContractDurationMonths,
                SignedPdfUrl = c.SignedPdfUrl
            }).ToList();

            return new ChargeSlot.Api.DTOs.PagedResultDto<ContractPreviewDto>
            {
                Page = page,
                PageSize = pageSize,
                TotalItems = result.TotalCount,
                Items = items
            };
        }

        public async Task<ChargeSlot.Api.DTOs.PagedResultDto<ContractPreviewDto>> GetAllContractsFilteredAsync(ContractFilterDto filter)
        {
            var result = await _contractRepo.GetAllFilteredPagedAsync(filter);

            var items = result.Items.Select(c => new ContractPreviewDto
            {
                ContractId = c.Id,
                ContractNumber = c.ContractNumber,
                OwnerName = c.OwnerName,
                OwnerUserId = c.OwnerUserId,
                Status = c.Status.ToString(),
                ContractHtml = "",
                CreatedAt = c.CreatedAt,
                SignedAt = c.SignedAt,
                ExpiresAt = c.ExpiresAt,
                ContractDurationMonths = c.ContractDurationMonths,
                SignedPdfUrl = c.SignedPdfUrl
            }).ToList();

            return new ChargeSlot.Api.DTOs.PagedResultDto<ContractPreviewDto>
            {
                Page = filter.Page <= 0 ? 1 : filter.Page,
                PageSize = filter.PageSize <= 0 ? 20 : filter.PageSize,
                TotalItems = result.TotalCount,
                Items = items
            };
        }

        public async Task<byte[]?> DownloadContractPdfForAdminAsync(int ownerUserId)
        {
            var contract = await _contractRepo.GetByOwnerAsync(ownerUserId);
            if (contract == null) return null;

            byte[]? signatureBytes = null;
            if (contract.Status == ContractStatus.Signed && !string.IsNullOrEmpty(contract.SignatureImageUrl))
                signatureBytes = await DownloadImageFromUrl(contract.SignatureImageUrl);
            return GenerateContractPdf(contract, signatureBytes);
        }

        // ═══════════════════════════════════════════════════
        // PDF GENERATION — QuestPDF
        // ═══════════════════════════════════════════════════

        private byte[] GenerateContractPdf(Contract contract, byte[]? signatureBytes)
        {
            var doc = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.MarginTop(1.5f, Unit.Centimetre);
                    page.MarginBottom(1.5f, Unit.Centimetre);
                    page.MarginHorizontal(2, Unit.Centimetre);
                    page.DefaultTextStyle(x => x.FontSize(11));

                    page.Content().Column(col =>
                    {
                        // ── QUỐC HIỆU ──
                        col.Item().AlignCenter().Text("CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM").Bold().FontSize(13);
                        col.Item().AlignCenter().Text("Độc lập — Tự do — Hạnh phúc").FontSize(12);
                        col.Item().AlignCenter().Text("─────────── ✦ ───────────").FontSize(10);
                        col.Item().Height(10);

                        // ── TIÊU ĐỀ ──
                        col.Item().AlignCenter().Text("HỢP ĐỒNG HỢP TÁC KINH DOANH").Bold().FontSize(15);
                        col.Item().AlignCenter().Text("DỊCH VỤ VẬN HÀNH TRẠM SẠC XE ĐIỆN").Bold().FontSize(13);
                        col.Item().Height(6);
                        col.Item().AlignCenter().Text($"Số: {contract.ContractNumber}").FontSize(11);
                        col.Item().AlignCenter().Text($"Ngày: {contract.CreatedAt:dd/MM/yyyy}").FontSize(11);
                        col.Item().Height(10);

                        // ── CĂN CỨ ──
                        col.Item().Text("Căn cứ Bộ luật Dân sự 2015;").Italic().FontSize(10);
                        col.Item().Text("Căn cứ Luật Thương mại 2005;").Italic().FontSize(10);
                        col.Item().Text("Căn cứ Luật Giao dịch điện tử 2023;").Italic().FontSize(10);
                        col.Item().Text("Căn cứ nhu cầu và năng lực thực tế của hai bên;").Italic().FontSize(10);
                        col.Item().Height(6);
                        col.Item().Text($"Hôm nay, ngày {contract.CreatedAt:dd} tháng {contract.CreatedAt:MM} năm {contract.CreatedAt:yyyy}, tại Thành phố Hà Nội, chúng tôi gồm:");
                        col.Item().Height(10);

                        // ── BÊN A ──
                        col.Item().Text("BÊN A: NỀN TẢNG CHARGESLOT").Bold().FontSize(12);
                        col.Item().Text("- Tên đơn vị: Công ty TNHH ChargeSlot Việt Nam").FontSize(11);
                        col.Item().Text("- Địa chỉ: TP. Hà Nội").FontSize(11);
                        col.Item().Text("- Đại diện: Ông Lại Vũ Hoàng Anh — Chức vụ: Giám đốc").FontSize(11);
                        col.Item().Text("- Mã số thuế: 037203002631").FontSize(11);
                        col.Item().Text("- Điện thoại: 0899 839 102").FontSize(11);
                        col.Item().Text("- Email: laivuhoanganh.fj@gmail.com").FontSize(11);
                        col.Item().Height(8);

                        // ── BÊN B ──
                        col.Item().Text("BÊN B: ĐỐI TÁC VẬN HÀNH TRẠM SẠC").Bold().FontSize(12);
                        col.Item().Text($"- Số CCCD / CMND: {contract.OwnerIdCard}").FontSize(11);
                        col.Item().Text($"- Mã số thuế: {contract.OwnerTaxCode}").FontSize(11);
                        col.Item().Text($"- Số GPKD: {contract.OwnerBusinessLicense}").FontSize(11);
                        col.Item().Text($"- Địa chỉ trụ sở: {contract.OwnerAddress}").FontSize(11);
                        col.Item().Text($"- Đại diện: {contract.OwnerName}").FontSize(11);
                        col.Item().Text($"- Điện thoại: {contract.OwnerPhone}").FontSize(11);
                        col.Item().Text($"- Email: {contract.OwnerEmail}").FontSize(11);
                        col.Item().Height(8);

                        col.Item().Text("Hai bên cùng thỏa thuận ký kết Hợp đồng hợp tác kinh doanh với các điều khoản sau:").FontSize(11);
                        col.Item().Height(8);

                        // ── ĐIỀU 1 ──
                        col.Item().Text("ĐIỀU 1: NỘI DUNG HỢP TÁC").Bold().FontSize(12);
                        col.Item().Text("1.1. Bên A cung cấp nền tảng phần mềm ChargeSlot (bao gồm ứng dụng di động và hệ thống quản lý) để Bên B đăng ký, quản lý và vận hành trạm sạc xe điện.");
                        col.Item().Text("1.2. Bên B sử dụng nền tảng ChargeSlot để tiếp nhận đặt chỗ sạc, quản lý slot sạc, xử lý thanh toán và phục vụ tài xế xe điện.");
                        col.Item().Text("1.3. Bên B cam kết cung cấp thông tin trạm sạc chính xác, đầy đủ và cập nhật kịp thời mọi thay đổi về giá cả, lịch hoạt động, số lượng slot.");
                        col.Item().Height(6);

                        // ── ĐIỀU 2 ──
                        col.Item().Text("ĐIỀU 2: QUYỀN VÀ NGHĨA VỤ CỦA BÊN A").Bold().FontSize(12);
                        col.Item().Text("2.1. Cung cấp nền tảng công nghệ ổn định, bảo đảm hệ thống hoạt động liên tục (trừ thời gian bảo trì có thông báo trước).");
                        col.Item().Text("2.2. Hỗ trợ kỹ thuật cho Bên B trong quá trình sử dụng nền tảng.");
                        col.Item().Text("2.3. Xử lý thu hộ thanh toán từ tài xế thông qua tài khoản ký quỹ (Escrow) và chuyển doanh thu cho Bên B theo quy định tại Điều 3.");
                        col.Item().Text("2.4. Bảo mật thông tin cá nhân và thông tin kinh doanh của Bên B theo quy định pháp luật về bảo vệ dữ liệu cá nhân.");
                        col.Item().Text("2.5. Cung cấp hệ thống báo cáo doanh thu, thống kê hoạt động trạm sạc cho Bên B.");
                        col.Item().Height(6);

                        // ── ĐIỀU 3 ──
                        col.Item().Text("ĐIỀU 3: CƠ CHẾ TÀI CHÍNH VÀ PHÂN CHIA DOANH THU").Bold().FontSize(12);
                        col.Item().Text("3.1. Cơ chế thanh toán: Toàn bộ thanh toán từ tài xế sẽ được chuyển vào tài khoản ký quỹ (Escrow) của hệ thống. Sau khi phiên sạc hoàn thành và được xác nhận, hệ thống tự động phân bổ doanh thu.");
                        col.Item().Text("3.2. Phí nền tảng: Bên A thu 5% (năm phần trăm) trên tổng giá trị mỗi giao dịch đặt chỗ thành công làm phí vận hành nền tảng.");
                        col.Item().Text("3.3. Thuế giá trị gia tăng (VAT): 8% (tám phần trăm) trên tổng giá trị giao dịch được khấu trừ theo quy định pháp luật hiện hành và giữ tại tài khoản thuế của hệ thống.");
                        col.Item().Text("3.4. Doanh thu Bên B nhận: Bằng tổng giá trị giao dịch trừ đi phí nền tảng (5%) và thuế GTGT (8%). Cụ thể, Bên B nhận 87% (tám mươi bảy phần trăm) giá trị mỗi giao dịch.");
                        col.Item().Text("3.5. Doanh thu được ghi nhận vào ví điện tử của Bên B trên nền tảng. Bên B có thể yêu cầu rút tiền về tài khoản ngân hàng theo quy trình rút tiền trên nền tảng.");
                        col.Item().Text("3.6. Các tỷ lệ nêu tại khoản 3.2 và 3.3 có thể được điều chỉnh bởi Bên A theo quy định pháp luật, với thông báo trước ít nhất 30 ngày cho Bên B.");
                        col.Item().Height(6);

                        // ── ĐIỀU 4 ──
                        col.Item().Text("ĐIỀU 4: QUYỀN VÀ NGHĨA VỤ CỦA BÊN B").Bold().FontSize(12);
                        col.Item().Text("4.1. Đảm bảo trạm sạc hoạt động đúng lịch đăng ký trên nền tảng; thông báo trước ít nhất 24 giờ nếu trạm cần đóng cửa đột xuất.");
                        col.Item().Text("4.2. Đảm bảo an toàn kỹ thuật, an toàn điện cho thiết bị sạc tại trạm. Bên B chịu hoàn toàn trách nhiệm nếu xảy ra sự cố kỹ thuật ảnh hưởng đến tài xế.");
                        col.Item().Text("4.3. Tiếp nhận và xử lý khiếu nại từ tài xế liên quan đến chất lượng dịch vụ sạc tại trạm.");
                        col.Item().Text("4.4. Không sử dụng nền tảng ChargeSlot cho mục đích bất hợp pháp hoặc vi phạm điều khoản sử dụng.");
                        col.Item().Text("4.5. Tuân thủ quy định về \"Hủy khẩn cấp\" của nền tảng: giới hạn 1 lần/tháng. Vi phạm sẽ bị đình chỉ trạm 30 ngày.");
                        col.Item().Height(6);

                        // ── ĐIỀU 5 ──
                        col.Item().Text("ĐIỀU 5: THỜI HẠN HỢP ĐỒNG").Bold().FontSize(12);
                        col.Item().Text("5.1. Hợp đồng có hiệu lực kể từ ngày Bên B ký điện tử và có thời hạn 12 tháng (mười hai tháng).");
                        col.Item().Text("5.2. Hợp đồng tự động gia hạn thêm 12 tháng nếu không bên nào có văn bản thông báo chấm dứt trước 30 ngày trước ngày hết hạn.");
                        col.Item().Height(6);

                        // ── ĐIỀU 6 ──
                        col.Item().Text("ĐIỀU 6: CHẤM DỨT HỢP ĐỒNG").Bold().FontSize(12);
                        col.Item().Text("6.1. Hai bên có thể thỏa thuận chấm dứt hợp đồng bằng văn bản hoặc qua hệ thống điện tử.");
                        col.Item().Text("6.2. Bên A có quyền đơn phương chấm dứt hợp đồng nếu Bên B vi phạm nghiêm trọng, bao gồm: cung cấp thông tin sai lệch, gian lận tài chính, vi phạm an toàn kỹ thuật, hoặc bị cơ quan chức năng xử phạt liên quan đến hoạt động trạm sạc.");
                        col.Item().Text("6.3. Bên B có quyền yêu cầu chấm dứt hợp đồng trước thời hạn với điều kiện: (a) không có đặt chỗ nào đang hoạt động tại các trạm sạc của Bên B; (b) gửi yêu cầu qua hệ thống và nêu rõ lý do.");
                        col.Item().Text("6.4. Khi hợp đồng chấm dứt, toàn bộ trạm sạc của Bên B sẽ bị đình chỉ hoạt động trên nền tảng và không tiếp nhận đặt chỗ mới.");
                        col.Item().Text("6.5. Bên A sẽ thanh toán toàn bộ doanh thu còn lại trong ví cho Bên B trong vòng 15 ngày làm việc kể từ ngày chấm dứt. Bên B vẫn có quyền rút số dư ví trong thời gian này.");
                        col.Item().Text("6.6. Trong trường hợp Bên A đơn phương chấm dứt do vi phạm, các đặt chỗ đang hoạt động sẽ được hệ thống tự động hủy và hoàn tiền cho tài xế.");
                        col.Item().Text("6.7. Trường hợp Bên A đơn phương chấm dứt hợp đồng theo khoản 6.2 (do vi phạm), Bên B sẽ không được ký kết hợp đồng hợp tác mới với Bên A trừ khi có quyết định khác từ Bên A.");
                        col.Item().Text("6.8. Trường hợp Bên B tự nguyện chấm dứt hợp đồng theo khoản 6.3, Bên B có quyền đăng ký ký kết hợp đồng hợp tác mới với Bên A khi đáp ứng đầy đủ điều kiện tại thời điểm đăng ký.");
                        col.Item().Height(6);

                        // ── ĐIỀU 7 ──
                        col.Item().Text("ĐIỀU 7: ĐIỀU KHOẢN CHUNG").Bold().FontSize(12);
                        col.Item().Text("7.1. Hợp đồng này được ký bằng phương thức chữ ký điện tử và có giá trị pháp lý tương đương văn bản giấy theo Luật Giao dịch điện tử 2023 (Luật số 20/2023/QH15).");
                        col.Item().Text("7.2. Mọi tranh chấp phát sinh sẽ được giải quyết thông qua thương lượng. Nếu không giải quyết được, sẽ đưa ra Tòa án nhân dân có thẩm quyền tại TP. Hà Nội.");
                        col.Item().Text("7.3. Hợp đồng này được lập thành bản điện tử, mỗi bên giữ một bản có giá trị pháp lý như nhau.");
                        col.Item().Height(20);

                        // ── KÝ TÊN ──
                        col.Item().Row(row =>
                        {
                            // Bên A
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().AlignCenter().Text("ĐẠI DIỆN BÊN A").Bold().FontSize(12);
                                c.Item().Height(8);
                                c.Item().AlignCenter().Text("(Đã ký điện tử)").Italic().FontSize(10);
                                c.Item().Height(30);
                                c.Item().AlignCenter().Text("Lại Vũ Hoàng Anh").Bold();
                                c.Item().AlignCenter().Text("Giám đốc").FontSize(10);
                                c.Item().AlignCenter().Text($"Ngày: {contract.CreatedAt:dd/MM/yyyy}").FontSize(10);
                            });

                            row.ConstantItem(30); // spacing

                            // Bên B — CHỮ KÝ TAY DÁN VÀO ĐÂY
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().AlignCenter().Text("ĐẠI DIỆN BÊN B").Bold().FontSize(12);
                                c.Item().Height(8);

                                if (signatureBytes != null && signatureBytes.Length > 0)
                                {
                                    c.Item().AlignCenter().Width(150).Height(60).Image(signatureBytes);
                                }
                                else
                                {
                                    c.Item().AlignCenter().Text("(Chưa ký)").Italic().FontSize(10);
                                    c.Item().Height(30);
                                }

                                c.Item().AlignCenter().Text(contract.OwnerName).Bold();
                                c.Item().AlignCenter().Text("Đại diện pháp luật").FontSize(10);
                                c.Item().AlignCenter().Text($"Ngày: {(contract.SignedAt.HasValue ? contract.SignedAt.Value.ToString("dd/MM/yyyy") : "___/___/______")}").FontSize(10);
                            });
                        });
                    });
                });
            });

            using var stream = new MemoryStream();
            doc.GeneratePdf(stream);
            return stream.ToArray();
        }

        // ═══════════════════════════════════════════════════
        // HTML PREVIEW — Cho frontend hiển thị
        // ═══════════════════════════════════════════════════

        private string GenerateContractHtml(Contract contract)
        {
            var signedAtStr = contract.SignedAt.HasValue ? contract.SignedAt.Value.ToString("dd/MM/yyyy") : "___/___/______";
            var signatureHtml = contract.Status == ContractStatus.Signed && !string.IsNullOrEmpty(contract.SignatureImageUrl)
                ? $"<img src=\"{contract.SignatureImageUrl}\" alt=\"Chữ ký\" style=\"max-width:150px;max-height:60px;\" />"
                : "<em>(Chưa ký)</em>";

            return $@"
<div style='font-family: ""Times New Roman"", serif; max-width: 800px; margin: 0 auto; padding: 20px; line-height: 1.6;'>
    <div>
        <p style='text-align: center; font-size: 14px; font-weight: bold; margin: 0;'>CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM</p>
        <p style='text-align: center; font-size: 13px; margin: 0;'>Độc lập — Tự do — Hạnh phúc</p>
        <p style='text-align: center; font-size: 11px; margin: 4px 0 16px;'>─────────── ✦ ───────────</p>
        <p style='text-align: center; font-size: 16px; font-weight: bold; margin: 4px 0;'>HỢP ĐỒNG HỢP TÁC KINH DOANH</p>
        <p style='text-align: center; font-size: 14px; font-weight: bold; margin: 0 0 4px;'>DỊCH VỤ VẬN HÀNH TRẠM SẠC XE ĐIỆN</p>
        <p style='text-align: center;'>Số: <strong>{contract.ContractNumber}</strong></p>
        <p style='text-align: center;'>Ngày: <strong>{contract.CreatedAt:dd/MM/yyyy}</strong></p>
    </div>
    <hr/>
    <p><em>Căn cứ Bộ luật Dân sự 2015;<br/>Căn cứ Luật Thương mại 2005;<br/>Căn cứ Luật Giao dịch điện tử 2023;<br/>Căn cứ nhu cầu và năng lực thực tế của hai bên;</em></p>
    <p>Hôm nay, ngày <strong>{contract.CreatedAt:dd}</strong> tháng <strong>{contract.CreatedAt:MM}</strong> năm <strong>{contract.CreatedAt:yyyy}</strong>, tại Thành phố Hà Nội, chúng tôi gồm:</p>
    <hr/>
    <p><strong>BÊN A: NỀN TẢNG CHARGESLOT</strong></p>
    <ul>
        <li>Tên đơn vị: <strong>Công ty TNHH ChargeSlot Việt Nam</strong></li>
        <li>Địa chỉ: TP. Hà Nội</li>
        <li>Đại diện: <strong>Ông Lại Vũ Hoàng Anh</strong> — Chức vụ: Giám đốc</li>
        <li>Mã số thuế: <strong>037203002631</strong></li>
        <li>Điện thoại: <strong>0899 839 102</strong></li>
        <li>Email: <strong>laivuhoanganh.fj@gmail.com</strong></li>
    </ul>
    <p><strong>BÊN B: ĐỐI TÁC VẬN HÀNH TRẠM SẠC</strong></p>
    <ul>
        <li>Tên doanh nghiệp: <strong>{HttpUtility.HtmlEncode(contract.OwnerName)}</strong></li>
        <li>Số CCCD / CMND: <strong>{HttpUtility.HtmlEncode(contract.OwnerIdCard)}</strong></li>
        <li>Mã số thuế: <strong>{HttpUtility.HtmlEncode(contract.OwnerTaxCode)}</strong></li>
        <li>Số GPKD: <strong>{HttpUtility.HtmlEncode(contract.OwnerBusinessLicense)}</strong></li>
        <li>Địa chỉ: <strong>{HttpUtility.HtmlEncode(contract.OwnerAddress)}</strong></li>
        <li>Điện thoại: <strong>{HttpUtility.HtmlEncode(contract.OwnerPhone)}</strong></li>
        <li>Email: <strong>{HttpUtility.HtmlEncode(contract.OwnerEmail)}</strong></li>
    </ul>
    <p>Hai bên cùng thỏa thuận ký kết Hợp đồng hợp tác kinh doanh với các điều khoản sau:</p>
    <hr/>

    <h3>ĐIỀU 1: NỘI DUNG HỢP TÁC</h3>
    <p>1.1. Bên A cung cấp nền tảng phần mềm <strong>ChargeSlot</strong> (bao gồm ứng dụng di động và hệ thống quản lý) để Bên B đăng ký, quản lý và vận hành trạm sạc xe điện.</p>
    <p>1.2. Bên B sử dụng nền tảng ChargeSlot để tiếp nhận đặt chỗ sạc, quản lý slot sạc, xử lý thanh toán và phục vụ tài xế xe điện.</p>
    <p>1.3. Bên B cam kết cung cấp thông tin trạm sạc chính xác, đầy đủ và cập nhật kịp thời mọi thay đổi về giá cả, lịch hoạt động, số lượng slot.</p>

    <h3>ĐIỀU 2: QUYỀN VÀ NGHĨA VỤ CỦA BÊN A</h3>
    <p>2.1. Cung cấp nền tảng công nghệ ổn định, bảo đảm hệ thống hoạt động liên tục (trừ thời gian bảo trì có thông báo trước).</p>
    <p>2.2. Hỗ trợ kỹ thuật cho Bên B trong quá trình sử dụng nền tảng.</p>
    <p>2.3. Xử lý thu hộ thanh toán từ tài xế thông qua tài khoản ký quỹ (Escrow) và chuyển doanh thu cho Bên B theo quy định tại Điều 3.</p>
    <p>2.4. Bảo mật thông tin cá nhân và thông tin kinh doanh của Bên B theo quy định pháp luật về bảo vệ dữ liệu cá nhân.</p>
    <p>2.5. Cung cấp hệ thống báo cáo doanh thu, thống kê hoạt động trạm sạc cho Bên B.</p>

    <h3>ĐIỀU 3: CƠ CHẾ TÀI CHÍNH VÀ PHÂN CHIA DOANH THU</h3>
    <p>3.1. <strong>Cơ chế thanh toán</strong>: Toàn bộ thanh toán từ tài xế sẽ được chuyển vào tài khoản ký quỹ (Escrow) của hệ thống. Sau khi phiên sạc hoàn thành và được xác nhận, hệ thống tự động phân bổ doanh thu.</p>
    <p>3.2. <strong>Phí nền tảng</strong>: Bên A thu <strong>5%</strong> (năm phần trăm) trên tổng giá trị mỗi giao dịch đặt chỗ thành công làm phí vận hành nền tảng.</p>
    <p>3.3. <strong>Thuế giá trị gia tăng (VAT)</strong>: <strong>8%</strong> (tám phần trăm) trên tổng giá trị giao dịch được khấu trừ theo quy định pháp luật hiện hành.</p>
    <p>3.4. <strong>Doanh thu Bên B nhận</strong>: Bằng tổng giá trị giao dịch trừ đi phí nền tảng (5%) và thuế GTGT (8%). Cụ thể, Bên B nhận <strong>87%</strong> giá trị mỗi giao dịch.</p>
    <p>3.5. Doanh thu được ghi nhận vào ví điện tử của Bên B trên nền tảng. Bên B có thể yêu cầu rút tiền về tài khoản ngân hàng theo quy trình rút tiền trên nền tảng.</p>
    <p>3.6. Các tỷ lệ nêu tại khoản 3.2 và 3.3 có thể được điều chỉnh bởi Bên A theo quy định pháp luật, với thông báo trước ít nhất <strong>30 ngày</strong> cho Bên B.</p>

    <h3>ĐIỀU 4: QUYỀN VÀ NGHĨA VỤ CỦA BÊN B</h3>
    <p>4.1. Đảm bảo trạm sạc hoạt động đúng lịch đăng ký trên nền tảng; thông báo trước ít nhất 24 giờ nếu trạm cần đóng cửa đột xuất.</p>
    <p>4.2. Đảm bảo an toàn kỹ thuật, an toàn điện cho thiết bị sạc tại trạm. Bên B chịu hoàn toàn trách nhiệm nếu xảy ra sự cố kỹ thuật ảnh hưởng đến tài xế.</p>
    <p>4.3. Tiếp nhận và xử lý khiếu nại từ tài xế liên quan đến chất lượng dịch vụ sạc tại trạm.</p>
    <p>4.4. Không sử dụng nền tảng ChargeSlot cho mục đích bất hợp pháp hoặc vi phạm điều khoản sử dụng.</p>
    <p>4.5. Tuân thủ quy định về &quot;Hủy khẩn cấp&quot; của nền tảng: giới hạn 1 lần/tháng. Vi phạm sẽ bị đình chỉ trạm 30 ngày.</p>

    <h3>ĐIỀU 5: THỜI HẠN HỢP ĐỒNG</h3>
    <p>5.1. Hợp đồng có hiệu lực kể từ ngày Bên B ký điện tử và có thời hạn <strong>12 tháng</strong> (mười hai tháng).</p>
    <p>5.2. Hợp đồng tự động gia hạn thêm 12 tháng nếu không bên nào có văn bản thông báo chấm dứt trước 30 ngày trước ngày hết hạn.</p>

    <h3>ĐIỀU 6: CHẤM DỨT HỢP ĐỒNG</h3>
    <p>6.1. Hai bên có thể thỏa thuận chấm dứt hợp đồng bằng văn bản hoặc qua hệ thống điện tử.</p>
    <p>6.2. <strong>Bên A</strong> có quyền đơn phương chấm dứt hợp đồng nếu Bên B vi phạm nghiêm trọng: cung cấp thông tin sai lệch, gian lận tài chính, vi phạm an toàn kỹ thuật, hoặc bị cơ quan chức năng xử phạt.</p>
    <p>6.3. <strong>Bên B</strong> có quyền yêu cầu chấm dứt hợp đồng trước thời hạn với điều kiện: (a) không có đặt chỗ nào đang hoạt động; (b) gửi yêu cầu qua hệ thống và nêu rõ lý do.</p>
    <p>6.4. Khi hợp đồng chấm dứt, toàn bộ trạm sạc của Bên B sẽ bị đình chỉ hoạt động và không tiếp nhận đặt chỗ mới.</p>
    <p>6.5. Bên A sẽ thanh toán toàn bộ doanh thu còn lại trong ví cho Bên B trong vòng <strong>15 ngày làm việc</strong>. Bên B vẫn có quyền rút số dư ví trong thời gian này.</p>
    <p>6.6. Trong trường hợp Bên A đơn phương chấm dứt do vi phạm, các đặt chỗ đang hoạt động sẽ được hệ thống tự động hủy và hoàn tiền cho tài xế.</p>
    <p>6.7. Trường hợp Bên A đơn phương chấm dứt hợp đồng theo khoản 6.2 (do vi phạm), Bên B sẽ <strong>không được ký kết hợp đồng hợp tác mới</strong> với Bên A trừ khi có quyết định khác từ Bên A.</p>
    <p>6.8. Trường hợp Bên B tự nguyện chấm dứt hợp đồng theo khoản 6.3, Bên B có quyền đăng ký ký kết hợp đồng hợp tác mới với Bên A khi đáp ứng đầy đủ điều kiện tại thời điểm đăng ký.</p>

    <h3>ĐIỀU 7: ĐIỀU KHOẢN CHUNG</h3>
    <p>7.1. Hợp đồng này được ký bằng chữ ký điện tử và có giá trị pháp lý theo Luật Giao dịch điện tử 2023 (Luật số 20/2023/QH15).</p>
    <p>7.2. Mọi tranh chấp sẽ được giải quyết thông qua thương lượng. Nếu không được, đưa ra Tòa án nhân dân có thẩm quyền tại TP. Hà Nội.</p>
    <p>7.3. Hợp đồng được lập thành bản điện tử, mỗi bên giữ một bản có giá trị pháp lý như nhau.</p>
    <hr/>

    <table style='width:100%; margin-top:20px; border:none;'>
        <tr>
            <td style='width:50%; text-align:center; vertical-align:top; border:none;'>
                <p style='text-align: center;'><strong>ĐẠI DIỆN BÊN A</strong></p>
                <p style='text-align: center;'><em>(Đã ký điện tử)</em></p>
                <br/><br/>
                <p style='text-align: center;'><strong>Lại Vũ Hoàng Anh</strong></p>
                <p style='text-align: center;'>Giám đốc</p>
                <p style='text-align: center;'>Ngày: {contract.CreatedAt:dd/MM/yyyy}</p>
            </td>
            <td style='width:50%; text-align:center; vertical-align:top; border:none;'>
                <p style='text-align: center;'><strong>ĐẠI DIỆN BÊN B</strong></p>
                <div style='text-align: center;'>
                    {signatureHtml}
                </div>
                <br/>
                <p style='text-align: center;'><strong>{HttpUtility.HtmlEncode(contract.OwnerName)}</strong></p>
                <p style='text-align: center;'>Đại diện pháp luật</p>
                <p style='text-align: center;'>Ngày: {signedAtStr}</p>
            </td>
        </tr>
    </table>
</div>";
        }

        // ═══════════════════════════════════════════════════
        // HELPERS
        // ═══════════════════════════════════════════════════

        private ContractPreviewDto MapToPreviewDto(Contract contract)
        {
            return new ContractPreviewDto
            {
                ContractId = contract.Id,
                ContractNumber = contract.ContractNumber,
                OwnerName = contract.OwnerName,
                OwnerUserId = contract.OwnerUserId,
                Status = contract.Status.ToString(),
                ContractHtml = GenerateContractHtml(contract),
                CreatedAt = contract.CreatedAt,
                SignedAt = contract.SignedAt,
                ExpiresAt = contract.ExpiresAt,
                ContractDurationMonths = contract.ContractDurationMonths,
                SignedPdfUrl = contract.SignedPdfUrl
            };
        }

        private static byte[] DecodeBase64Image(string base64String)
        {
            // Remove data URI header if present: "data:image/png;base64,..."
            var base64Data = base64String;
            if (base64Data.Contains(","))
            {
                base64Data = base64Data[(base64Data.IndexOf(",") + 1)..];
            }
            return Convert.FromBase64String(base64Data);
        }

        private async Task<byte[]?> DownloadImageFromUrl(string url)
        {
            try
            {
                using var httpClient = _httpClientFactory.CreateClient();
                return await httpClient.GetByteArrayAsync(url);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[Contract] Failed to download signature image from {Url}", url);
                return null;
            }
        }

        // ═══════════════════════════════════════════════════
        // ADMIN — Chấm dứt hợp đồng
        // ═══════════════════════════════════════════════════

        public async Task TerminateContractAsync(int ownerUserId, string reason)
        {
            var contract = await _contractRepo.GetByOwnerAsync(ownerUserId)
                ?? throw new InvalidOperationException("Owner chưa có hợp đồng.");

            if (contract.Status == ContractStatus.Terminated)
                throw new InvalidOperationException("Hợp đồng đã bị chấm dứt trước đó.");

            if (contract.Status == ContractStatus.Pending)
                throw new InvalidOperationException("Hợp đồng chưa được ký, không thể chấm dứt.");

            contract.Status = ContractStatus.Terminated;
            contract.TerminatedAt = DateTimeHelper.VietnamNow();
            contract.TerminationReason = reason;
            _contractRepo.Update(contract);

            // Điều 6.4: Đình chỉ toàn bộ trạm sạc của Owner
            await SuspendOwnerStationsAsync(ownerUserId);

            // Điều 6.6: Tự động hủy các đặt chỗ đang hoạt động và hoàn tiền cho tài xế
            await CancelActiveBookingsForOwnerAsync(ownerUserId, $"Hợp đồng {contract.ContractNumber} bị chấm dứt bởi quản trị viên. Lý do: {reason}");

            await _unitOfWork.CompleteAsync();

            // Notify Owner
            await _notificationService.SendAsync(
                ownerUserId,
                "Hợp đồng hợp tác đã bị chấm dứt",
                $"Hợp đồng {contract.ContractNumber} đã bị chấm dứt bởi quản trị viên. Lý do: {reason}. Toàn bộ trạm sạc đã bị đình chỉ và các đặt chỗ đang hoạt động đã được hủy.",
                NotificationType.System);

            _logger.LogWarning("[Contract] Admin terminated contract {ContractNumber} for Owner {UserId}. Reason: {Reason}",
                contract.ContractNumber, ownerUserId, reason);
        }

        // ═══════════════════════════════════════════════════
        // OWNER — Yêu cầu chấm dứt hợp đồng sớm (Điều 6.3)
        // ═══════════════════════════════════════════════════

        public async Task RequestTerminationAsync(int ownerUserId, string reason)
        {
            var contract = await _contractRepo.GetByOwnerAsync(ownerUserId)
                ?? throw new InvalidOperationException("Chưa có hợp đồng.");

            if (contract.Status != ContractStatus.Signed)
                throw new InvalidOperationException($"Chỉ có thể yêu cầu chấm dứt hợp đồng đang có hiệu lực. Trạng thái hiện tại: {contract.Status}.");

            // Kiểm tra điều kiện Điều 6.3(a): không có booking đang hoạt động
            var stations = await _stationRepo.GetAllByOwnerAsync(ownerUserId);
            var stationIds = stations.Select(s => s.Id).ToList();

            if (stationIds.Count > 0)
            {
                var activeStatuses = new[]
                {
                    BookingStatus.WaitingOwner,
                    BookingStatus.PendingPayment,
                    BookingStatus.Paid,
                    BookingStatus.CheckedIn,
                    BookingStatus.CompletedPendingInvoice
                };

                var activeBookings = await _bookingRepo.GetActiveBookingsByStationIdsAsync(stationIds, activeStatuses);

                if (activeBookings.Count > 0)
                {
                    throw new InvalidOperationException(
                        $"Không thể chấm dứt hợp đồng khi còn {activeBookings.Count} đặt chỗ đang hoạt động (Điều 6.3). " +
                        "Vui lòng chờ tất cả đặt chỗ hoàn thành hoặc hủy trước khi yêu cầu chấm dứt.");
                }
            }

            // Chấm dứt hợp đồng
            contract.Status = ContractStatus.Terminated;
            contract.TerminatedAt = DateTimeHelper.VietnamNow();
            contract.TerminationReason = $"[Owner yêu cầu] {reason}";
            _contractRepo.Update(contract);

            // Đình chỉ toàn bộ trạm sạc (Điều 6.4)
            await SuspendOwnerStationsAsync(ownerUserId);

            await _unitOfWork.CompleteAsync();

            // Notify Admin
            var adminUsers = await _userManager.GetUsersInRoleAsync(Constants.RoleConstants.Admin);
            foreach(var admin in adminUsers)
            {
                await _notificationService.SendAsync(
                    admin.Id,
                    "Owner yêu cầu chấm dứt hợp đồng",
                    $"Owner (UserId: {ownerUserId}) đã yêu cầu chấm dứt hợp đồng {contract.ContractNumber}. Lý do: {reason}.",
                    NotificationType.System);
            }

            _logger.LogWarning(
                "[Contract] Owner {UserId} requested termination of contract {ContractNumber}. Reason: {Reason}",
                ownerUserId, contract.ContractNumber, reason);
        }

        // ═══════════════════════════════════════════════════
        // HELPERS — Đình chỉ trạm sạc khi chấm dứt hợp đồng
        // ═══════════════════════════════════════════════════

        private async Task SuspendOwnerStationsAsync(int ownerUserId)
        {
            var stations = await _stationRepo.GetAllByOwnerTrackingAsync(ownerUserId);
            var count = 0;
            foreach (var station in stations)
            {
                if (station.OperationalStatus == OperationalStatus.Active)
                {
                    station.OperationalStatus = OperationalStatus.Inactive;
                    count++;
                }

                // Bug #10 fix: Từ chối cả trạm đang PendingApproval
                if (station.ApprovalStatus == ApprovalStatus.PendingApproval)
                {
                    station.ApprovalStatus = ApprovalStatus.Rejected;
                    station.AdminNote = "Tự động từ chối: Hợp đồng hợp tác đã bị chấm dứt.";
                    count++;
                }
            }

            if (count > 0)
            {
                _logger.LogInformation(
                    "[Contract] Suspended/rejected {Count} station(s) for Owner {UserId} due to contract termination",
                    count, ownerUserId);
            }
        }

        /// <summary>
        /// Điều 6.6: Tự động hủy tất cả đặt chỗ đang hoạt động của Owner và hoàn tiền cho tài xế.
        /// </summary>
        private async Task CancelActiveBookingsForOwnerAsync(int ownerUserId, string reason)
        {
            var stations = await _stationRepo.GetAllByOwnerAsync(ownerUserId);
            var stationIds = stations.Select(s => s.Id).ToList();
            if (stationIds.Count == 0) return;

            var activeStatuses = new[]
            {
                BookingStatus.WaitingOwner,
                BookingStatus.PendingPayment,
                BookingStatus.Paid,
                BookingStatus.CheckedIn,
                BookingStatus.CompletedPendingInvoice
            };

            var activeBookings = await _bookingRepo.GetActiveBookingsByStationIdsAsync(stationIds, activeStatuses);

            foreach (var booking in activeBookings)
            {
                try
                {
                    await _bookingService.CancelSystemBookingAsync(booking.Id, reason);
                    _logger.LogInformation(
                        "[Contract] Auto-cancelled booking {BookingId} due to contract termination for Owner {UserId}",
                        booking.Id, ownerUserId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "[Contract] Failed to cancel booking {BookingId} during contract termination for Owner {UserId}",
                        booking.Id, ownerUserId);
                }
            }

            if (activeBookings.Count > 0)
            {
                _logger.LogInformation(
                    "[Contract] Auto-cancelled {Count} active booking(s) for Owner {UserId} (Điều 6.6)",
                    activeBookings.Count, ownerUserId);
            }
        }
    }
}
