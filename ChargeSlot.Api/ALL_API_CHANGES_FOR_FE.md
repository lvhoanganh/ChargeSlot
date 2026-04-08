# 🚀 CẨM NANG TÍCH HỢP FRONTEND - TỔNG HỢP MỌI THAY ĐỔI LÕI (Cập nhật 07/04)

Tài liệu này tổng hợp **TẤT CẢ** những thay đổi lớn về API, Logic và Business rules mà team Backend đã đập đi xây lại trong hôm nay. Frontend (FE) vui lòng đọc **thật kỹ** và cập nhật đúng chuẩn để tránh lỗi `400 Bad Request` hoặc sập App.

---

## 🛑 1. THAY ĐỔI CẤU TRÚC PHÂN TRANG (PAGINATION) - [QUAN TRỌNG NHẤT KHẮP NƠI]

**TOÀN BỘ** các API trả về dạng danh sách (List) trong hệ thống đều đã được thiết kế lại để phân trang chuẩn phía Server (Server-side Pagination). Frontend **tuyệt đối không** gọi full list rồi tự cắt bằng JS nữa để tiết kiệm RAM điện thoại/web.

### Mẫu Data Trả Về Mới Bắt Buộc:
Thay vì trả về thẳng một mảng Data `[...]`, API giờ đây sẽ bọc trong một Object (kiểu `PagedResultDto<T>`):
```json
{
  "totalCount": 105,     // Tổng số record khớp điều kiện Filter trong DB
  "page": 1,             // Trang hiện tại
  "pageSize": 20,        // Số phần tử trên 1 trang limit
  "items": [             // Mảng dữ liệu thực tế
    { ... }, 
    { ... }
  ]
}
```

### Các API Bị Ảnh Hưởng (FE cần update code map data ngay lập tức):
1. **Notifications:** `GET /api/notifications`
2. **Bookings của Driver:** `GET /api/bookings/driver/history`
3. **Bookings của Chủ Trạm:** `GET /api/bookings/owner/ongoing`, `GET /api/bookings/owner/history` (Thêm query filter: `?status=Paid`)
4. **Lịch sử Giao dịch Ví (Ledger):** `GET /api/wallet/transactions`
5. **Lịch sử Rút tiền:** `GET /api/wallet/withdrawals`
6. **Khiếu nại (Disputes):** `GET /api/dispute/my-disputes` (Và các endpoint Admin duyệt Dispute)
7. **Chat & Tin nhắn:** `GET /api/chat/rooms`, `GET /api/chat/room/{roomId}/messages`
8. **Toàn bộ API danh sách của Admin (Datagrids - Xem phần 5 dưới đây)**

### Tham số Gửi Đi Bắt Buộc:
Kẹp thêm `?page=1&pageSize=20` vào URL.
- Nếu gửi `?page=0` hoặc âm, Server sẽ tự ép về `1`.
- Nếu gửi `pageSize` > 100, Server sẽ tự ép về max `100` để chống DDOS.

---

## ⏰ 2. LOGIC ĐẶT CHỖ (BLOCK SCHEDULING & DURATION) - [RẤT QUAN TRỌNG]

Logic đặt giờ đã được thay đổi nghiêm ngặt để phân lô thời gian sạc giống hệt đặt vé máy bay. 

### Ràng buộc về Form đặt lịch:
- **KHÔNG ĐƯỢC GỬI ENDTIME.** Payload `POST /api/bookings` giờ **CHỈ NHẬN**:
  - `startTime`: `2026-04-07T10:30:00Z`
  - `durationHours`: `2.5` (Lưu ý, kiểu số nguyên/thực như 0.5, 1, 1.5, 2. Mặc định FE show select box chọn giờ để sạc).
- **Giới hạn múi phút (30-Minute Block):** `startTime` truyền lên **BẮT BUỘC** phải là phút `00` hoặc `30`. Nếu FE đẩy giờ lẻ (VD: `10:15` hay `16:45`), Backend chém ngay HTTP `400 BadRequest`.
- **Tuyệt đối khóa Giây / Milli-giây:** Cần set giây về `.000Z` (Backend đẩy văng nếu mang giây lẻ).
- **Khoảng đệm (Buffer 15 phút):** Hệ thống tự nới biên độ nội bộ. Khi list, FE không lo, Backend lo thuật toán bù trừ giờ để không ai tranh slot của ai.

### Lọc danh sách Trạm Khả dụng (Real-time Availability)
- API Gọi: `GET /api/public/stations?startTime=...&endTime=...` 
- Res trả về từng Object của Trạm sẽ có thêm 1 field CỰC MA THUẬT: `"availableSlotsCount": 3`.
- Giao diện: Nếu `count == 0` -> Frontend **phải làm mờ Trạm / Đổi chữ thành Kín lịch**. Đừng cho họ click sâu thêm vào trong vì kiểu gì cũng không còn slot.

---

## 🔔 3. HỆ THỐNG CẢNH BÁO DEADLINE (PROACTIVE NOTIFICATIONS & AUTO SEND MAIL)

Hệ thống bổ sung một CronJob (Bot nhắc nhở) siêu đỉnh. FE tự tin update UI, Backend lo automation. Người dùng sẽ nhận chung cả **In-app Notification** (chuông) lẫn **Email CC tới hòm thư thật** trước đúng 1 tiếng tính từ mốc deadline tự động kích hoạt:

1. **Lịch Sạc:** Booking sắp tới hạn check-in (nhắc Driver chuấn bị tới trạm trước 1h). Nhắc Chủ trạm slot sắp có xe tới.
2. **Hóa Đơn:** Invoice cần xác nhận (nhắc Driver trước khi hết thời gian 24h tự động thanh toán bù trừ).
3. **Khiếu nại (Dispute):** Nhắc chủ trạm (Owner) yêu cầu nộp Bằng chứng (khi giờ vàng đếm ngược chỉ còn < 1 tiếng). Nhắc cả Admin vào review.
4. **Rút tiền ví:** Nhắc User chủ động vào xác nhận đã ting ting trong tài khoản NH thực tế, nếu không Backend tự chốt giao dịch vĩnh viễn (sau 24h).

---

## 🛡️ 4. LUỒNG XÁC THỰC DANH TÍNH CHỦ TRẠM (OWNER KYC WORKFLOW)

Owner không còn được tự do kinh doanh ngay lúc mới đăng ký. Bắt buộc KYC (Xác thực pháp lý). Kế hoạch cho FE mở cổng:

1. FE check `kycStatus` từ cục auth `GET /api/auth/me`. 
   - State Machine: `Unverified` -> Mở Form Submit KYC. `Pending` -> Hiện chữ đang Duyệt. `Approved` -> Done.
2. Form Upload cho Owner (Gửi dạng `multipart/form-data`) lên URL `POST /api/owner/kyc/submit`:
   - Gắn file `identityDocumentFront` (File IFormFile - CMND Mặt trước)
   - Gắn file `identityDocumentBack` (File IFormFile - Mắt sau)
   - Gắn file `businessLicense` (Lựa chọn thêm IFormFile nếu là cty)
   - Kẹp Body Text `taxCode` (Mã số thuế).
3. Trang Admin sẽ có bảng list KYC -> Bấm nút Check duyệt thông qua API `PUT /api/admin/kyc/{ownerUserId}/review`. 

---

## 👑 5. CHẾ ĐỘ THẦN NHÃN ADMIN (GOD MODE DATAGRIDS OVERVIEW)

Cung cấp toàn bộ URL cho bộ Datagrids Siêu tốc (Giảm 99% RAM load từ DB). Mảng này hoàn toàn độc lập cho team Admin FE xây Dashboard:

- `GET /api/admin/operations/bookings`: Bảng Bookings Tổng (Filter nhét thẳng Query: status, date, driverUserId, stationId...)
- `GET /api/admin/operations/sessions`: Bảng Chẩn đoán Phiên Sạc Thực Tế (Phát hiện cáp lỗi, ngắt cầu dao...).
- `GET /api/admin/operations/invoices`: Phân tích hóa đơn, đặc biệt lọc theo biến bool `isPaid`.
- `GET /api/admin/finance/wallets`: Radar giám sát Vốn (Các Ví Tạm Giữ Escrow, Ví trung gian, Ví Driver/Owner). 
- `GET /api/admin/finance/wallets/{walletId}/transactions`: Soi lịch sử dòng vốn Transaction ra vào của 1 Ví cụ thể.
- `GET /api/admin/operations/stations/revenue`: BI Report Doanh thu trạm.
- `GET /api/admin/system/disputes`: Trung tâm hòa giải Dispute toàn server.

---

## ✏️ 6. THOÁT NGẠN MỤC UPDATE TRẠM SẠC (UNBLOCKED UPDATE STATION)

- **Vấn Đề Cũ:** Trạm khi đã `Approved` bị Admin khóa Cứng API Update không cho Sửa Tên/Ảnh.
- **Tính Năng Lõi Mới:** Chủ Trạm tự do thay đổi Giờ Mở Cửa, Ảnh Trạm, Địa Chỉ. 
- API `PUT/PATCH` Trạm hỗ trợ Payload Data dạng `multipart/form-data` để Owner được phép tái Up File Ảnh mà không lỗi Server (Khắc phục format lỗi).

---

## 🚀 ROADMAP MỤC TIÊU NGẮN HẠN CHO FE (TODAY'S CHECKLIST)
- [x] Đọc kĩ và chuyển tất cả mảng State List cũ sang bắt property `items` nằm trong cục PagedResult. Tận dụng `totalCount` cho UI số trang phân trang AntD/MUI/...
- [x] Chỉnh Options TimePicker trong Modal đặt sạc Booking, step=30 phút. Form Input chỉ truyền 1 biến `DurationHours`.
- [x] Code Web/App cho trang nộp KYC.
- [x] Làm giao diện Badge/Label "HẾT SLOT" nếu Backend truyền mảng `availableSlotsCount` = 0.
- [x] Cắt giao diện Table trang Admin theo các Link Datagrid kể trên.
