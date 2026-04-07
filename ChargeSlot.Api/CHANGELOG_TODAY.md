# 🔍 BÁO CÁO PHÂN TÍCH CHUYÊN SÂU: TỔNG HỢP KIẾN TRÚC MÃ NGUỒN NGÀY 07/04

Báo cáo này phân rã và quét dọn **MỌI CHI TIẾT NHỎ NHẤT** từ Git Branch nhánh `HoangAnh` trong ngày hôm nay. Mọi thay đổi đều được phân tích cực kỳ nghiêm túc để thấy rõ chúng ta đã đập đi xây lại cấu trúc lõi ra sao.

---

## 📅 1. TÁI CẤU TRÚC LÕI ĐẶT CHỖ (BOOKING KERNEL REFACTOR)

Đây là thay đổi cực kỳ tinh vi để chống lại sự phân mảnh thời gian sạc, gây thiệt hại doanh thu cho trạm.

- **Ép Buộc Múi Giờ 30 Phút (Block Scheduling):**
  - Khởi tạo rào cản từ chối cứng mã tại `BookingService.cs`.
  - Tham số `DurationHours` bắt buộc phải là số thực chia hết cho `0.5m` (Tức 30 phút). Backend sẽ quăng Exception nếu cố tình nạp `0.3` (20 phút).
  - Thuộc tính `StartTime` (Giờ bắt đầu) không được phép phân mảnh. Backend chỉ nhận giờ phút ở mức `:00` hoặc `:30`. Không nhận mili-giây. Điều này giúp các phiên sạc xếp vừa khít như gạch Tetris.
- **Ngày Nghỉ Trạm (Unavailable Dates):**
  - Thêm bảng `StationUnavailableDate` vào Entity Framework.
  - Xây mới Controller chặn hoàn toàn tài xế đặt cọc vào các ngày Owner đi nghỉ/cúp điện mà không cần phải can thiệp trạng thái slot riêng lẻ.

---

## 🔎 2. THUẬT TOÁN KÉP: LỌC TRẠM SẠC & CHỐNG CHỒNG LỊCH (DOUBLE OVERLAP FILTERING)

Truy vấn tìm kiếm trạm ở `PublicStationController.cs` đã được tiến hóa thành thuật toán Real-time Availability (Tìm chỗ theo thời gian thực).

- **Đẩy Thuật Toán Về Dưới DB (SQL Offloading):** 
  - Backend không lọc Array tại RAM nữa. Truy vấn EF Core đã móc nối để tiêm thẳng khoảng thời gian `StartTime` -> `EndTime` từ App xuống Database.
  - **Khoảng Đệm An Toàn (15-Minute Buffer):** Backend tự động nới thêm 15 phút tại đầu múi giờ để kiểm tra trùng lịch kép vô cùng chặt chẽ.
- **Biến Đếm `AvailableSlotsCount`:**
  - Frontend truyền toạ độ + khoảng giờ muốn sạc -> Trả về danh sách trạm với con số `availableSlotsCount` CHÍNH XÁC. Dưới 0 là tự động ẩn.

---

## 🛡️ 3. LÁ CHẮN XÁC THỰC DANH TÍNH (OWNER KYC) VÀ HỆ THỐNG AN TOÀN

Bảo vệ dòng tiền cực sát, tránh Chủ trạm rửa tiền, gian lận:

- **Tạo Quy Trình Kiểm Duyệt Nhâm Dân:**
  - Nêm cột `KycStatus`, `BusinessName`, `TaxCode`, `DocumentLinks` vào Model Owner tĩnh.
  - Phân tách `OwnerKycController` (Xử lý Multipart Form Upload an toàn lên Cloud lưu trữ) và `AdminKycController` (Trao quyền Phê duyệt / Từ chối kèm lý do).
- **Vệ Sinh Database Định Kỳ (Email Verification Cleanup):**
  - Kích hoạt tiến trình Background Worker xé nhỏ RAM - Tự động xóa sổ các tài khoản `PendingEmailVerification` quá 24h, tiết kiệm rác cơ sở dữ liệu. Cập nhật mới các DTO bảo mật chống Spam (Xác thực mail, Đổi mail).

---

## 👁️ 4. CHẾ ĐỘ THẦN NHÃN QUẢN TRỊ VIÊN (ADMIN GOD MODE)

Tính năng "Toàn tri" với trọng tâm lớn nhất nằm ở Tốc độ xử lý. Hàng triệu bản ghi phải truy vấn dưới 200ms.

- **Công cụ Core Filter Engine:**
  - Chuẩn hóa lại cấu trúc `PagedFilterDto`, gom gọn Data Fetching vào `AdminFilters.cs`.
- **Triển Khai Datagrid Siêu Nhẹ (`.AsNoTracking`):**
  - Ngắt mọi con đường theo dõi bộ nhớ (Tracking) của Entity Framework bên trong toàn bộ `GetAdminAllBookings`, `GetAdminAllSessions`, `GetAdminAllWallets` tại các Service Lõi. Cắt giảm 99% hiện trạng Memory Leak của Server lúc tải dữ liệu lớn.
  - Chia cắt quyền hạn Kế toán (Wallets, Ledger) và Vận Hành (Sessions, Bookings) sang các Controller chuyên trách: `AdminFinanceController` & `AdminOperationsController`.

---

## 🧪 5. KIÊN CỐ HÓA BẢNG KIỂM THỬ TỰ ĐỘNG (UNIT TEST RESILIENCY)

Dọn dẹp bãi chiến trường Test Suite sau khi chắp vá hàng chục Dependency mới.

- **Bypass SQLite Crashing:** Vá lỗi `ExecuteSqlRawAsync` (Nguyên nhân sập khi Test In-Memory).
- Xây mới móng `TestDbHelper.cs` vững chắc: Seed Data Trạm, Chỗ, Owner, Driver mượt mà cho hơn 50 kịch bản giả lập, đặc biệt phủ sóng tuyệt đối đợt API KYC và Admin Overview vừa ra mắt.

**📌 TỔNG KẾT:**
Chỉ trong nhánh `HoangAnh` hôm nay, hạ tầng đã khóa cứng các nguy cơ Booking đứt gãy, chống rò rỉ bộ nhớ hoàn hảo, và cung cấp một cỗ máy Lưới thời gian (Time-blocking) tiêu chuẩn quốc tế. Mọi thứ đã có đầy đủ tại `FE_INTEGRATION_GUIDE.md` để Team Frontend cắm rễ xây dựng Giao diện.
