# 🚀 TÀI LIỆU TÍCH HỢP FRONTEND 
**Phiên bản: Hoàn chỉnh & Chi tiết nhất (Cập nhật ngày 07/04/2026)**

Tài liệu này là đặc tả kiến trúc kỹ thuật nghiêm ngặt dành cho Frontend. Mọi tính năng cốt lõi hôm nay bao gồm: **Xếp lịch trạm sạc theo thời gian thực (Real-time Availability)**, **Bảo mật khung giờ (30-Minute Blocks)**, **Quy trình KYC**, và **Admin God Mode**.

Yêu cầu đội ngũ Frontend tuân thủ tuyệt đối các ràng buộc (Constraints) dưới đây để xây dựng UI (Giao diện) tránh bị Backend chối bỏ (400 Bad Request).

---

## 🔥 PHẦN 1: TÍNH NĂNG CHỌN TRẠM & ĐẶT LỊCH (TRỌNG TÂM MỚI CHUẨN XÁC)

Hệ thống đã chuyển đổi toàn bộ cấu trúc sang cơ chế **Slot Thời Gian Chẵn (Block Scheduling)** và kiểm tra trùng lịch kép. Các sai lầm về giờ giấc sẽ bị Backend block thẳng tay.

### 1.1 Tìm Kiếm Trạm Khả Dụng Theo Vị Trí & Khung Giờ (Map / List Screen)
Khi Driver mở bản đồ hoặc danh sách trạm, FE phải tải các trạm có **chỗ trống (Available Slots)** tương ứng với khoảng thời gian dự định sạc.

**API:** `GET /api/public/stations`
**Tham số Filter bắt buộc (Cho UI Pick Giờ):**
- `startTime` (ISO 8601): Thời gian dự định vào sạc (Ví dụ: `2026-04-07T10:00:00Z`).
- `endTime` (ISO 8601): Thời gian dự định sạc xong (Ví dụ: `2026-04-07T11:00:00Z`).
- `lat`, `lng`, `radiusKm`: Tọa độ hiện tại của tài xế.

**Response Mới Nhất Dành Cho FE:**
Mỗi đối tượng trạm trả về giờ đây có thêm thuộc tính cốt lõi:
- `distanceKm`: Khoảng cách thực tế.
- `availableSlotsCount`: Số lượng Slot **đang trống hoàn toàn** trong khung giờ `startTime` -> `endTime` mà FE đã gửi. (Backend đã tính cả khoảng đệm 15 phút giữa các ca).

*Hành động của FE:* Nếu `availableSlotsCount == 0`, mờ trạm đi (Disabled) hoặc gắn nhãn "Kín lịch".

### 1.2 Ràng Buộc Form Đặt Chỗ (Booking Form) - QUAN TRỌNG
Khi FE thiết kế giao diện chọn giờ sạc (Time Picker component), **phải khóa (disable)** các lựa chọn không hợp lệ theo quy chế Backend:

1. **Khóa Múi Phút Lẻ:** Giờ bắt đầu (`StartTime`) **BẮT BUỘC** phải nằm ở vạch `00` phút hoặc `30` phút (VD: 10:00, 10:30, 23:30). Không nhận 10:15 hay 10:45.
2. **Khóa Giây:** Thuộc tính `Second` và `Millisecond` phải truyền lên là `00`.
3. **Khóa Múi Thời Lượng (Duration):** Tham số `DurationHours` bắt buộc phải là bội số của `0.5` (Cụ thể: `0.5h`, `1h`, `1.5h`, `2h`...).
4. Nếu FE cố tình đẩy giờ sai, Backend lập tức văng `400 BadRequest: "Giờ bắt đầu sạc bắt buộc phải chẵn theo block 30 phút..."`.

### 1.3 Ngày Khóa Trạm (Unavailable Dates)
Owner có quyền đóng cửa trạm nguyên ngày (đi nghỉ lễ, cúp điện). FE phải chốt sổ chặn đặt lịch các ngày này.
- **Lấy ngày khóa:** `GET /api/charging-stations/{id}/unavailable-dates`
- *Hành động của FE:* Thiết lập hàm `disabledDate` trên thẻ Calendar/Datepicker để chặn Driver bấm vào các ngày này.

---

## 🛡️ PHẦN 2: TÍCH HỢP LUỒNG XÁC THỰC DANH TÍNH (OWNER KYC)

Owner bị khóa quyền tạo Trạm / Rút tiền cho đến khi hồ sơ kinh doanh được chốt duyệt.
1. FE Get `/api/auth/me` -> Đọc thuộc tính `kycStatus`.
   - `Unverified`: Mở Component giục nộp KYC.
   - `Pending`: Render luồng "Đang xử lý". Khóa form.
   - `Approved`: Trải thảm đỏ, mở mọi tính năng kinh doanh.
   - `Rejected`: Đọc thuộc tính `rejectionReason` (để chữ đỏ), cho phép Submit lại.

2. **Cổng Nộp Hồ Sơ:** `POST /api/owner/kyc/submit`
   - Payload: FormData. Up đủ thẻ CMND Trước/Sau (`identityDocumentFront`, `identityDocumentBack`) và Giấy Kinh Doanh (`businessLicense`).
3. **Cổng Admin Duyệt:** `PUT /api/admin/kyc/{ownerUserId}/review`
   - Phải có `{ "isApproved": true/false, "rejectReason": "..." }`.

---

## 👁️ PHẦN 3: TÍCH HỢP "ADMIN GOD MODE" DATAGRIDS

Đây là tính năng DataGrid Backend. Backend áp dụng `.AsNoTracking` cắt giảm 99% RAM. Backend chỉ trả về giới hạn trang hiện tại. FE tuyệt đố kỵ cắm List lớn rồi Filter bằng Local JS.

**Khung Response Bắt Buộc Của Grid:**
```json
{
  "items": [{...}, {...}],
  "totalCount": 105,
  "page": 1,
  "pageSize": 20
}
```

Trang bị thanh Filter động cho mỗi bảng, bắn lên URL Query như sau:

**1. Bảng 1: Liệt kê Bookings Toàn Hệ Thống**
- `GET /api/admin/operations/bookings`
- Thêm Query: `status` (Paid, Cancelled...), `driverUserId`, `ownerUserId`, `stationId`, `fromDate`, `toDate`.

**2. Bảng 2: Liệt kê Phiên Sạc Thực Tế (Bắt Lỗi Server/Trạm)**
- `GET /api/admin/operations/sessions`
- Thêm Query: `status` (Active, Completed, Faulted), `bookingId`.

**3. Bảng 3: Kiểm Kê Hóa Đơn Trạm**
- `GET /api/admin/operations/invoices`
- Thêm Query: `status` (Confirmed, PendingConfirm, UnderDispute), `isPaid`. (Cho kế toán dò nợ hệ thống).

**4. Bảng 4: Soi Tình Trạng Trữ Tiền (Các Ví)**
- `GET /api/admin/finance/wallets`
- Thêm Query: `walletType` (Owner, Driver, System), `systemCode` (ESCROW, CLEARING). (Kiểm soát nợ xấu, nợ ký quỹ).

**5. Bảng 5: Kính Lúp Sổ Cái Dòng Tiền (Ledger)**
- `GET /api/admin/finance/wallets/{walletId}/transactions`
- Thêm Query: `transactionType` (Credit, Debit). Vạch mặt dòng tiền thất thoát trong tích tắc.
