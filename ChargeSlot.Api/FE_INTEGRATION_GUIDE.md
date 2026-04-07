# 🚀 TÀI LIỆU TÍCH HỢP FRONTEND (KYC & ADMIN GOD MODE)

Tài liệu này mô tả chi tiết quy trình luồng dữ liệu (Standard Flow) và danh sách các API mới nhất để team Frontend (FE) tích hợp, đảm bảo hệ thống chặn mọi lỗ hổng hoạt động (Operational Vulnerabilities) và cho phép Admin quan sát toàn cục.

---

## PHẦN 1: TÍCH HỢP LUỒNG XÁC THỰC DANH TÍNH CHỦ TRẠM (OWNER KYC)

### 1 MÔ TẢ LUỒNG CHUẨN (STANDARD KYC FLOW)

Tại sao phải có KYC? Để ngăn chặn rửa tiền và trạm sạc "ma", Account Role `Owner` bị khóa phần lớn các thao tác nhạy cảm (Tạo trạm, Rút tiền ví) cho đến khi được Admin duyệt hồ sơ kinh doanh.

**BƯỚC 1: KIỂM TRA TRẠNG THÁI KYC CỦA OWNER**
1. FE gọi API lấy thông tin Profile sau khi Login `GET /api/auth/me`.
2. Kiểm tra thuộc tính `kycStatus` (Kiểu `string`).
3. Các trạng thái:
   - `Unverified`: Mặc định, FE hiển thị Banner "Vui lòng xác minh danh tính để tạo trạm".
   - `Pending`: Đã nộp, FE hiển thị "Hồ sơ đang chờ duyệt, KHÔNG cho nộp lại".
   - `Approved`: Tuyệt vời, FE ẩn cờ chặn, mở nút "Tạo Trạm", "Rút tiền".
   - `Rejected`: Bị Admin từ chối. FE hiển thị lý do đỏ chót `rejectionReason`, hiện nút "Nộp lại hồ sơ".

**BƯỚC 2: MÀN HÌNH NỘP HỒ SƠ (OWNER PORTAL)**
1. Màn hình yêu cầu Owner cung cấp:
   - CMND/CCCD/Passport (Mặt trước + Mặt sau). Tối đa 5MB.
   - Giấy phép kinh doanh (Kinh doanh hộ gia đình/Doanh nghiệp). PDF hoặc Ảnh.
   - Business Name (Tên đăng ký kinh doanh).
   - Tax Code (Mã số thuế).
2. FE bọc các file này vào chuẩn `multipart/form-data` và gọi API Submit.

**BƯỚC 3: MÀN HÌNH PHÊ DUYỆT HỒ SƠ (ADMIN PORTAL)**
1. Admin vào "Quản lý KYC Chủ Trạm".
2. FE gọi API Backend kéo danh sách hồ sơ `Pending`.
3. Admin xem ảnh -> Duyệt: Gọi API `Approve` hoặc `Reject` (bắt buộc nhập lý do).

### 2. DANH SÁCH API (KYC)

#### Dành cho Owner (App/Web Owner)
- **Kiểm tra trạng thái cá nhân**: 
  - `GET /api/owner-kyc/status`
  - *Response*: `{ "status": "Pending", "submittedAt": "...", "rejectionReason": null }`
- **Gửi hồ sơ KYC**: 
  - `POST /api/owner-kyc/submit` (Cần Header: `Content-Type: multipart/form-data`)
  - *Payload*: `identityDocumentFront` (File), `identityDocumentBack` (File), `businessLicense` (File), `businessName` (Text), `taxCode` (Text).

#### Dành cho Admin (Web Admin)
- **Lấy danh sách các hồ sơ đang treo (Pending)**: 
  - `GET /api/admin-kyc/pending`
- **Thực thi duyệt hồ sơ (Approve / Reject)**: 
  - `PUT /api/admin-kyc/review`
  - *Payload (JSON)*: 
    ```json
    {
      "ownerUserId": 12,
      "kycStatus": "Approved", // Hoặc "Rejected"
      "rejectionReason": "" // Bắt buộc nếu là Rejected
    }
    ```

---

## PHẦN 2: TÍCH HỢP TÍNH NĂNG "ADMIN GOD MODE" (KIỂM SOÁT TOÀN TRI)

### 1 MÔ TẢ LUỒNG CHUẨN (STANDARD ADMIN OVERVIEW FLOW)

Backend đã cung cấp vũ khí Lọc từ Server Server-Side Pagination siêu nhẹ. Nghĩa là DB có 5 triệu dòng, Admin bấm cũng không lag. 
FE tuyệt đối **KHÔNG ĐƯỢC** lấy toàn bộ dữ liệu về rồi Lọc ở mảng Local Memory (Array.filter), mà phải **chuyền Parameter** qua URL cho Backend.

**GIAO DIỆN KHUYẾN NGHỊ CHO FE:**
1. Sử dụng Component bảng kiểu dữ liệu **DataGrid (Ant Design, MUI Table)** có cơ chế `Serevr-Side Pagination`.
2. Tạo một thanh Header bên trên bảng chứa các **Filter Bar** (Ví dụ: Dropdown Status, Component Date Range Picker (Từ ngày - Đến ngày)).
3. Mỗi khi User gõ hoặc chọn Filter -> Setup ngắt nhịp debounce (300ms) -> Reload lại Grid bằng API mới.

### 2. DANH SÁCH API VÀ CÁCH GỌI (ADMIN)

TẤT CẢ các API dưới đây đều dùng Method `GET` và nhận tham số qua Query URL (`?page=1&pageSize=20...`).  
Tham số gốc luôn luôn có:
- `page` (Mặc định: 1)
- `pageSize` (Mặc định: 20)
- `fromDate` (Chuẩn ISO: `2026-04-07T00:00:00Z`)
- `toDate`

**Response Khung Chuẩn Tự Động Trả Về Mọi Bảng:**
```json
{
  "items": [{...}, {...}],
  "totalCount": 105,
  "page": 1,
  "pageSize": 20,
  "totalPages": 6
}
```

#### A. Nhóm Quản Lý Vận Hành (Operations)

**1. Liệt kê Đặt chỗ (Bookings)**
- **API:** `GET /api/admin/operations/bookings`
- **Params Lọc thêm:** `status` (Draft, Cancelled, Completed, Paid...), `driverUserId`, `ownerUserId`, `stationId`
- **Ví dụ FE Gọi:** `/api/admin/operations/bookings?page=1&status=Paid&stationId=5`

**2. Liệt kê Phiên Sạc Thực Tế (Charging Sessions)**
- **API:** `GET /api/admin/operations/sessions`
- **Params Lọc thêm:** `status` (Active, Completed, Faulted), `bookingId`

**3. Liệt kê Hóa Đơn Trạm (Invoices)**
- **API:** `GET /api/admin/operations/invoices`
- **Params Lọc thêm:** `status` (Confirmed, PendingConfirm, UnderDispute), `isPaid` (true/false)

#### B. Nhóm Giám Sát Tài Chính (Finance)

**4. Dò Tìm Tất Cả Các Ví (Wallets)**
- **API:** `GET /api/admin/finance/wallets`
- **Params Lọc thêm:** `walletType` (Owner, Driver, System), `userId`, `systemCode` (ESCROW, CLEARING).
- **Mục đích:** Admin xem tổng nợ tiền của hệ thống so với số dư khả dụng thực tế.

**5. Kiểm Toán Riêng Sổ Kế Toán 1 Ví (Ledger Transactions)**
- **API:** `GET /api/admin/finance/wallets/{walletId}/transactions`
- **Params Lọc thêm:** `transactionType` (Credit - Nhận tiền, Debit - Bị Trừ tiền).
- **Mục đích:** Nếu Driver kêu mất tiền, truyền WalletID của nó bảo đây bấm lọc Debit là ra chi tiết biến động.

---
**TIPS CHO TEAM FE:** Hãy dán ngay cái Tài liệu API này lên Swagger hoặc lưu ở thư mục doc. Các bạn không cần đau đầu xử lý Filter khó nữa, hãy cứ map đúng URL và tham số thôi! Chúc code giao diện thật ngầu!
