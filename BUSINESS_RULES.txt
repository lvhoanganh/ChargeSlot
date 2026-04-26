# TÀI LIỆU QUY TẮC NGHIỆP VỤ (BUSINESS RULES) — HỆ THỐNG CHARGESLOT

> **Phiên bản:** 1.1  
> **Cập nhật lần cuối:** 16/04/2026  
> **Phạm vi:** Toàn bộ quy tắc nghiệp vụ được hiện thực hóa trong mã nguồn Backend (ASP.NET Core Web API)

Tài liệu này mô tả chi tiết toàn bộ các quy tắc nghiệp vụ (Business Rules), máy trạng thái (State Machine), ngưỡng cấu hình mặc định (Default Thresholds) và các tác vụ tự động nền (Background Jobs) đang vận hành trong hệ thống ChargeSlot. Tất cả các chỉ số định lượng đều có thể được Admin điều chỉnh thông qua module Cấu hình Hệ thống (SystemConfig); giá trị ghi trong tài liệu là **giá trị mặc định khi triển khai lần đầu**.

---

## 1. ĐĂNG KÝ & XÁC THỰC TÀI KHOẢN (Authentication)

### 1.1 Đăng ký tài khoản mới
| Quy tắc | Mô tả |
|---|---|
| BR-01 | Người dùng đăng ký bằng **Số điện thoại** (xác thực qua Firebase Phone OTP) kết hợp với **Email** (xác thực qua liên kết email). |
| BR-02 | Khi đăng ký, tài khoản mang trạng thái `PENDING_EMAIL_VERIFICATION`. Người dùng phải bấm liên kết xác thực trong email để chuyển sang `ACTIVE`. |
| BR-03 | Tài khoản `PENDING_EMAIL_VERIFICATION` quá **24 giờ** mà chưa xác thực sẽ bị hệ thống **tự động xóa vĩnh viễn** (bao gồm hồ sơ Driver/Owner, Refresh Token, User Roles) bởi `EmailVerificationCleanupJob` — chạy mỗi 30 phút. |
| BR-04 | Mỗi số điện thoại và mỗi địa chỉ email chỉ được phép gắn với **duy nhất 1 tài khoản** trong toàn hệ thống. |
| BR-05 | Người dùng chọn vai trò `Driver` (Tài xế) hoặc `Owner` (Chủ trạm) khi đăng ký. Vai trò `Admin` chỉ được cấp nội bộ. |

### 1.2 Đăng nhập & Phiên làm việc
| Quy tắc | Mô tả |
|---|---|
| BR-06 | Hệ thống cấp cặp **Access Token** (JWT, hết hạn mặc định 120 phút) và **Refresh Token** (hết hạn mặc định 7 ngày). |
| BR-07 | Khi Refresh Token được sử dụng, token cũ bị thu hồi ngay lập tức và token mới được cấp (Token Rotation). Có lưu vết liên kết (`ReplacedByToken`) phục vụ kiểm toán. |
| BR-08 | Tài khoản ở trạng thái `BANNED` hoặc `SUSPENDED` bị **cấm đăng nhập** hoàn toàn. |
| BR-09 | Tài khoản `PENDING_EMAIL_VERIFICATION` bị **cấm đăng nhập** cho đến khi xác thực email thành công. |

### 1.3 Bảo mật OTP
| Quy tắc | Giá trị mặc định |
|---|---|
| BR-10 — Thời hạn hiệu lực OTP | **5 phút** (`OTP_Expiry_Minutes = 5`) |
| BR-11 — Khoảng cách tối thiểu giữa 2 lần gửi OTP | **30 giây** (`OTP_Cooldown_Seconds = 30`) |

### 1.4 Đổi Email
| Quy tắc | Mô tả |
|---|---|
| BR-12 | Khi người dùng yêu cầu đổi email, email mới được lưu vào trường `PendingEmail`, email cũ vẫn hoạt động bình thường cho đến khi email mới được xác thực qua liên kết. |

---

## 2. XÁC MINH DANH TÍNH DOANH NGHIỆP (KYC — Know Your Customer)

### 2.1 Máy trạng thái KYC
```
Unverified → Pending → Approved
                    ↘ Rejected
Approved → PendingUpdate → Approved (thông tin mới)
                         ↘ Approved (rollback về thông tin cũ)
```

### 2.2 Quy tắc chi tiết
| Quy tắc | Mô tả |
|---|---|
| BR-13 | Chỉ người dùng có vai trò `Owner` mới được phép gửi hồ sơ KYC. Hồ sơ bao gồm: Tên doanh nghiệp, Mã số thuế, Số CCCD, Ngày cấp CCCD, Số giấy phép kinh doanh, Địa chỉ. |
| BR-14 | Khi gửi hồ sơ lần đầu, trạng thái chuyển từ `Unverified` sang `Pending`. Admin duyệt (`Approved`) hoặc từ chối (`Rejected` kèm lý do). |
| BR-15 | Owner đã được `Approved` có quyền cập nhật thông tin KYC bất kỳ lúc nào. Khi cập nhật, trạng thái chuyển sang `PendingUpdate`. Thông tin cũ được **lưu Snapshot** vào các trường `Prev_*` (Prev_BusinessName, Prev_TaxCode, Prev_IdCardNumber...). |
| BR-16 | Trong thời gian `PendingUpdate`, Owner **vẫn duy trì toàn bộ quyền kinh doanh** (tạo trạm, nhận booking, rút tiền) dựa trên dữ liệu cũ. |
| BR-17 | Nếu Admin **từ chối** bản cập nhật, hệ thống tự động **rollback** (khôi phục) thông tin từ Snapshot cũ và trạng thái trở lại `Approved`. |

---

## 3. HỢP ĐỒNG HỢP TÁC B2B (Electronic Contract)

### 3.1 Máy trạng thái Hợp đồng
```
(Không có) → Pending → Signed → Expired (tự động gia hạn nếu đủ điều kiện)
                              ↘ Terminated (chấm dứt)
```

### 3.2 Quy tắc chi tiết
| Quy tắc | Mô tả |
|---|---|
| BR-18 | Khi KYC được Admin phê duyệt lần đầu (`Approved`), hệ thống **tự động sinh** một bản hợp đồng điện tử ở trạng thái `Pending`, chứa đầy đủ thông tin pháp nhân của Owner. |
| BR-19 | Owner ký hợp đồng bằng **chữ ký điện tử** (vẽ tay trên Canvas, chuyển đổi thành ảnh Base64 lưu trữ). Sau khi ký, trạng thái chuyển sang `Signed`, file PDF khóa cứng (không được chỉnh sửa). |
| BR-20 | Thời hạn hợp đồng mặc định là **12 tháng** kể từ ngày ký. |
| BR-21 | Nếu KYC có bản cập nhật mới được duyệt trong khi hợp đồng đang `Pending`: thông tin trong hợp đồng được **tự động cập nhật** theo dữ liệu KYC mới. Nếu hợp đồng đã `Signed`: **không can thiệp**, bảo toàn tính toàn vẹn văn bản pháp lý. |

### 3.3 Gia hạn & Hết hạn hợp đồng
| Quy tắc | Mô tả |
|---|---|
| BR-22 | Hệ thống gửi **thông báo nhắc nhở** cho Owner **30 ngày** trước khi hợp đồng hết hạn (`ContractExpiryJob` — chạy mỗi 6 giờ). |
| BR-23 | Khi hợp đồng đến ngày hết hạn, nếu Owner có KYC trạng thái `Approved` hoặc `PendingUpdate`: hợp đồng được **tự động gia hạn thêm 12 tháng** (Điều 5.2). |
| BR-24 | Nếu KYC ở trạng thái `Rejected` tại thời điểm hết hạn: hợp đồng chuyển sang `Expired`, **không được gia hạn**. |

### 3.4 Chấm dứt hợp đồng
| Quy tắc | Mô tả |
|---|---|
| BR-25 | **Owner yêu cầu chấm dứt** (Điều 6.3): chỉ được phép khi **toàn bộ N trạm sạc** của Owner không còn bất kỳ Booking nào ở trạng thái hoạt động (`WaitingOwner`, `PendingPayment`, `Paid`, `CheckedIn`, `CompletedPendingInvoice`). |
| BR-26 | **Admin chấm dứt** (Điều 6.4): có hiệu lực ngay lập tức. Hệ thống tự động hủy toàn bộ Booking đang hoạt động của Owner đó kèm hoàn tiền 100% cho tài xế. |
| BR-27 | Khi hợp đồng bị chấm dứt (`Terminated`): toàn bộ trạm sạc của Owner chuyển sang `Inactive`, các trạm đang `PendingApproval` bị chuyển sang `Rejected`. |
| BR-28 | **Admin chấm dứt → chặn ký mới** (Điều 6.7): nếu hợp đồng bị Admin chấm dứt do vi phạm, Owner **không được ký hợp đồng mới** — hệ thống chặn hoàn toàn. |
| BR-29 | **Owner tự nguyện chấm dứt → được ký mới** (Điều 6.8): Owner tự chấm dứt vẫn có quyền đăng ký hợp đồng mới khi đáp ứng đủ điều kiện (KYC Approved). |

### 3.5 Luồng tái ký hợp đồng (sau khi Owner tự chấm dứt)
| Bước | Hành động | Chi tiết |
|---|---|---|
| 1 | Owner nộp cập nhật KYC | KYC chuyển từ `Approved` → `PendingUpdate`. Owner vẫn giữ toàn bộ quyền kinh doanh trong thời gian chờ. |
| 2 | Admin duyệt KYC | KYC → `Approved`. Hệ thống **tự động tạo hợp đồng mới** (`Pending`). |
| 3 | Owner ký hợp đồng điện tử | Hợp đồng → `Signed`, có hiệu lực 12 tháng. |
| 4 | Owner tự bật trạm sạc | Trạm sạc vẫn còn nguyên (`ApprovalStatus = Approved`, dữ liệu Slot/Dịch vụ/Giá giữ nguyên) nhưng đang `Inactive`. Owner vào **bật thủ công** từng trạm → `Active`. **Không cần duyệt lại.** |

---

## 4. QUẢN LÝ TRẠM SẠC & CỌC SẠC (Station & Slot)

### 4.1 Máy trạng thái Trạm sạc
```
Draft → PendingApproval → Approved (OperationalStatus: Active)
                        ↘ Rejected → Draft (sửa lại) → PendingApproval
```

### 4.2 Quy tắc tạo & gửi duyệt trạm
| Quy tắc | Mô tả |
|---|---|
| BR-30 | Owner **bắt buộc phải có hợp đồng ở trạng thái `Signed`** mới được phép tạo trạm sạc. Nếu hợp đồng chưa ký hoặc đã bị chấm dứt, hệ thống chặn hoàn toàn. |
| BR-31 | Owner phải có KYC ở trạng thái `Approved` hoặc `PendingUpdate` mới được phép tạo trạm sạc. |
| BR-32 | Trạm sạc mới tạo mang trạng thái `Draft`. Chỉ khi Owner gửi duyệt mới chuyển sang `PendingApproval`. |
| BR-33 | Chỉ được gửi duyệt từ trạng thái `Draft` hoặc `Rejected`. Trạm đã `Approved` hoặc đang `PendingApproval` không được gửi lại. |

### 4.3 Quy tắc phê duyệt & vận hành
| Quy tắc | Mô tả |
|---|---|
| BR-34 | Khi Admin phê duyệt: trạm chuyển sang `Approved` + `Active`. Toàn bộ Slot bên trong tự động chuyển sang trạng thái `Active` và được cấp **mã QR Token** (GUID 32 ký tự). |
| BR-35 | Khi Admin từ chối: bắt buộc phải nhập lý do. Thông báo gửi đến Owner kèm lý do từ chối. |
| BR-36 | Admin có quyền **khóa thủ công** (Manual Ban) bất kỳ trạm sạc nào. Trạm bị khóa sẽ có `BannedUntil` = ngày hiện tại + 100 năm (tức khóa vĩnh viễn cho đến khi Admin mở). |
| BR-37 | Chỉ được phép **xóa trạm** khi trạm ở trạng thái `Draft` hoặc `Rejected`, và không có Booking đang hoạt động. Ảnh trên Firebase Storage được xóa đồng thời. |

### 4.4 Quản lý Cọc sạc (Slot)
| Quy tắc | Mô tả |
|---|---|
| BR-38 | Khi tạo Slot mới trong trạm đã `Approved`: Slot tự động `Active` kèm mã QR Token. Trong trạm `Draft`/`Rejected`: Slot mặc định `Inactive`, chưa có QR. |
| BR-39 | **Không được phép** đổi Slot sang trạng thái `Inactive` hoặc `Maintenance` nếu đang có Booking hoạt động (`Paid`, `CheckedIn`). Owner phải hủy Booking liên quan trước. |
| BR-40 | **Cấm** Owner thủ công set trạng thái `Booked` — trạng thái này chỉ do hệ thống quản lý tự động khi có Booking thanh toán thành công. |
| BR-41 | **Không được xóa** Slot trong trạm đã `Approved` nếu Slot đó **đã từng có** bất kỳ Booking nào (kể cả đã `Completed`). Chỉ cho phép chuyển sang `Inactive`. |
| BR-42 | Owner có quyền **tái tạo mã QR Token** cho Slot (ví dụ khi QR cũ bị lộ). Chỉ thực hiện được khi trạm ở trạng thái `Approved`. Token cũ bị vô hiệu ngay lập tức. |
| BR-43 | Hệ thống cung cấp API **Slot Availability**: truyền vào ngày cụ thể → trả về danh sách các khung giờ đã bị đặt (bao gồm buffer 15 phút) và thời gian trống gần nhất (`NextAvailableAt`). |

### 4.5 Giá sạc theo khung giờ (Dynamic Pricing)
| Quy tắc | Mô tả |
|---|---|
| BR-44 | Giá sạc do Owner thiết lập theo từng khung giờ trong ngày (Time-tier), có thể khác nhau theo thứ trong tuần (`DayOfWeek`). Hệ thống hỗ trợ mức ưu tiên (`Priority`) khi các khung giờ chồng lấn. |
| BR-45 | Nếu trạm sạc **chưa có bảng giá** nào, hệ thống **chặn hoàn toàn** mọi lệnh Booking vào trạm đó. |
| BR-46 | Với Booking kéo dài qua nhiều khung giờ, hệ thống tính giá **từng đoạn** theo tier tương ứng. Ví dụ: Booking 11h–14h, tier 5h–12h=10K/h + tier 12h–15h=12K/h → Tổng = 1×10K + 2×12K = 34K. |

### 4.6 Ngày nghỉ & Dịch vụ phụ
| Quy tắc | Mô tả |
|---|---|
| BR-47 | Owner có thể đánh dấu **Ngày không khả dụng** (`StationUnavailableDate`). Booking rơi vào các ngày này bị từ chối. |
| BR-48 | Owner có thể thiết lập **Dịch vụ phụ** (Extra Services) như nước uống, đồ ăn, phụ kiện sạc với hạn mức tồn kho (`TotalStock`). Dịch vụ có cờ `IsRental` (cho thuê) sẽ được **hoàn kho** sau khi phiên sạc kết thúc. |

---

## 5. ĐẶT CHỖ SẠC XE (Booking)

### 5.1 Máy trạng thái Booking
```
WaitingOwner → PendingPayment → Paid → CheckedIn → CompletedPendingInvoice → Completed
            ↘ Rejected        ↘ Expired                                     ↗ (Disputed → Resolved)
  WaitingOwner (quá hạn) → Expired
  Paid (quá EndTime không check-in) → CompletedPendingInvoice → NoShow (nếu không dispute)
                                                              → Disputed → Completed (nếu Driver thắng)
Bất kỳ → Cancelled (tùy chính sách hoàn tiền)
```

### 5.2 Quy tắc tạo Booking
| Quy tắc | Giá trị mặc định |
|---|---|
| BR-49 — Khung giờ chuẩn | Giờ bắt đầu sạc bắt buộc là bội số của **30 phút** (00 hoặc 30). Thời lượng sạc từ **0.5h đến 24h**, bội số 0.5h. |
| BR-50 — Đặt trước tối thiểu | **30 phút** (`Min_Booking_Lead_Minutes = 30`). Ví dụ: 10h00 hiện tại → chỉ đặt được từ 10h30. |
| BR-51 — Giới hạn Booking đồng thời | Mỗi tài xế tối đa **3 Booking** đang chờ xử lý. |
| BR-52 — Chống trùng giờ cá nhân | Một tài xế **không được** đặt 2 Booking trùng khung giờ, dù ở 2 Slot khác nhau. |
| BR-53 — Khoảng đệm giữa 2 Booking | **15 phút** (`Slot_Buffer_Minutes = 15`) giữa 2 Booking liên tiếp trên cùng 1 Slot. |
| BR-54 — Chống Double-booking | Hệ thống sử dụng **khóa bi quan cấp DB** (`AcquireSlotLockAsync`) kết hợp truy vấn chồng lấn thời gian (Overlap Query) thay vì dựa vào cờ `SlotStatus.Booked`. |
| BR-55 — Kiểm tra trạm | Chỉ cho phép đặt tại trạm có `ApprovalStatus = Approved` và `OperationalStatus = Active`. Slot phải không ở trạng thái `Inactive` hoặc `Maintenance`. |
| BR-56 — Kiểm tra ngày nghỉ | Hệ thống kiểm tra ngày đặt có nằm trong danh sách `StationUnavailableDate` không. Nếu có → từ chối. |
| BR-57 — Kiểm tra giờ mở cửa | Hệ thống kiểm tra trạm có mở cửa vào ngày/giờ đặt không (dựa trên `OperatingHours` theo `DayOfWeek`). Ngày được đánh dấu `IsClosed` → từ chối. |

### 5.3 Owner duyệt & từ chối Booking
| Quy tắc | Mô tả |
|---|---|
| BR-58 | Booking ở trạng thái `WaitingOwner` chờ Owner duyệt. Owner có thể **Duyệt** (→ `PendingPayment`) hoặc **Từ chối** (→ `Rejected`, Owner bắt buộc nhập lý do). |
| BR-59 — Quá hạn duyệt | Nếu Owner không phản hồi trong khoảng thời gian cho phép (`Waiting_Owner_Timeout_Minutes`), Booking tự động hết hạn (`Expired`), tồn kho và điểm tích lũy được hoàn trả. |

### 5.4 Trừ tồn kho Dịch vụ phụ
| Quy tắc | Mô tả |
|---|---|
| BR-60 | Tồn kho dịch vụ phụ được **trừ ngay tại thời điểm tạo Booking** (Reservation Pattern), sử dụng **Semaphore Lock** (`_stockLock`) để chống bán vượt kho (Overselling) khi nhiều Booking tạo đồng thời. |
| BR-61 | Nếu Booking bị hủy hoặc hết hạn thanh toán, tồn kho được **hoàn trả ngay lập tức**. |

---

## 6. THANH TOÁN (Payment)

### 6.1 Thanh toán qua Chuyển khoản (SePay/VietQR)
| Quy tắc | Giá trị mặc định |
|---|---|
| BR-62 — Thời hạn thanh toán | **30 phút** (`Payment_Expiry_Minutes = 30`). Quá hạn → Booking tự động hủy bởi `PaymentExpiryJob` (chạy mỗi 1 phút). |
| BR-63 — Nội dung chuyển khoản | Mã giao dịch nhúng trong nội dung: `CS{bookingId}` cho thanh toán Booking, `W{userId}` cho nạp tiền vào ví. Hệ thống dùng Regex trích xuất mã này từ webhook SePay. |
| BR-64 — Xử lý Race Condition | Nếu tài xế chuyển khoản thành công nhưng Booking đã hết hạn (webhook SePay đến trễ), hệ thống kiểm tra Slot còn trống không. Nếu còn → khôi phục Booking. Nếu hết → **hoàn tiền 100% vào ví tài xế** tự động. |
| BR-65 — Chuyển khoản thiếu tiền | Nếu số tiền nhận được < `TotalAmount` của Booking: **không thanh toán**, toàn bộ số tiền được nạp vào ví Driver kèm thông báo yêu cầu thanh toán lại bằng ví. |
| BR-66 — Chuyển khoản trùng lặp | Nếu Booking đã được thanh toán hoặc đã hoàn tiền trước đó mà Driver chuyển thêm: tiền tự động **hoàn vào ví Driver**, không xử lý lần hai. |
| BR-67 — Chống trùng Webhook (Idempotency) | Mỗi giao dịch SePay được ghi nhận theo `SePay#{id}` vào Ledger. Nếu webhook bắn lại cùng mã → bỏ qua. Đồng thời kiểm tra `GatewayTxnRef` trên Payment để tránh xử lý trùng. |
| BR-68 — Tiền không xác định | Mọi khoản chuyển khoản SePay mà hệ thống không tìm được mã `CS` hoặc `W` trong nội dung sẽ được ghi nhận vào **ví đối soát CLEARING** để Admin xử lý thủ công. Không một đồng nào bị mất. |

### 6.2 Nạp tiền vào Ví (Top-Up)
| Quy tắc | Giá trị |
|---|---|
| BR-69 — Số tiền nạp tối thiểu | **10.000 VND** |
| BR-70 — Số tiền nạp tối đa | **50.000.000 VND** |
| BR-71 — Quy trình | Tài xế tạo mã QR VietQR có nội dung `W{userId}`, chuyển khoản qua ngân hàng. Khi tiền đến, webhook SePay xử lý và **nạp thẳng vào ví Driver** kèm ghi Ledger kép (CLEARING ↔ Driver Wallet). |

### 6.3 Thanh toán bằng Ví nội bộ
| Quy tắc | Mô tả |
|---|---|
| BR-72 | Tài xế có thể thanh toán Booking bằng số dư ví nội bộ. Tiền bị trừ nguyên tử (Atomic Deduct) bằng SQL trực tiếp để chống Race Condition. |
| BR-73 | Tiền thanh toán từ ví được chuyển thẳng vào **ví ESCROW** (ký quỹ) của hệ thống, tương tự luồng chuyển khoản ngân hàng. |

---

## 7. CHECK-IN & PHIÊN SẠC (Charging Session)

### 7.1 Check-in bằng mã QR
| Quy tắc | Giá trị mặc định |
|---|---|
| BR-74 — Cửa sổ Check-in | Tài xế được scan QR sớm nhất **15 phút** trước giờ sạc (`CheckIn_Window_Minutes = 15`). Không được check-in sớm hơn. |
| BR-75 — Paid quá EndTime không check-in | Khi Booking đã `Paid` nhưng quá `EndTime` mà Driver chưa check-in: hệ thống tự động chuyển sang `CompletedPendingInvoice`, tạo hóa đơn `PendingConfirm`, Slot được **giải phóng ngay**. Toàn bộ tồn kho dịch vụ phụ được **hoàn trả**. Driver có **24 giờ** để khiếu nại (Dispute). |
| BR-76 — Không check-in + không Dispute | Nếu Driver **không khiếu nại** trong 24h: hóa đơn tự động xác nhận, Booking chuyển sang trạng thái **`NoShow`**. Tiền được quyết toán cho Owner (Driver mất tiền). Driver **không được tích điểm** vì chưa check-in. |
| BR-77 — Không check-in + Dispute thắng | Nếu Driver khiếu nại và thắng: Booking chuyển sang **`Completed`**, tiền hoàn 100% vào ví Driver. |

### 7.2 Overtime (CheckedIn quá giờ)
| Quy tắc | Mô tả |
|---|---|
| BR-78 | Khi Booking đã `CheckedIn` nhưng quá `EndTime`: hệ thống **tự động dừng phiên sạc** (`ActualEndTime = EndTime`), tạo hóa đơn `PendingConfirm`, Slot giải phóng ngay lập tức. |
| BR-79 | Dịch vụ phụ loại cho thuê (`IsRental`) được **hoàn kho** khi overtime auto-stop. Dịch vụ không phải rental (đồ ăn, nước uống) **không hoàn kho** vì khách đã check-in sử dụng. |

### 7.3 Kết thúc phiên sạc thủ công
| Quy tắc | Mô tả |
|---|---|
| BR-80 | Owner **không được quyền** kết thúc phiên sạc sớm trừ khi: (a) đã hết thời gian sạc (`EndTime`), hoặc (b) Tài xế đã bấm nút **"Yêu cầu kết thúc sớm"**. |
| BR-81 | Khi phiên kết thúc: Slot trả về `Active`, dịch vụ phụ loại cho thuê (`IsRental`) được hoàn kho, hóa đơn (Invoice) được tạo tự động ở trạng thái `PendingConfirm`. |

---

## 8. CHÍNH SÁCH HỦY & HOÀN TIỀN (Cancellation & Refund)

### 8.1 Tài xế hủy Booking đã thanh toán
| Điều kiện | Tỷ lệ hoàn tiền |
|---|---|
| Còn ≥ **2 giờ** trước giờ sạc (`RefundPolicy100_Hrs = 2`) | **100%** |
| Còn từ **1 đến 2 giờ** (`RefundPolicy50_Hrs = 1`) | **50%** (50% còn lại chuyển cho Owner) |
| Còn **dưới 1 giờ** | **0%** (Owner nhận toàn bộ) |

### 8.2 Hủy Booking chưa thanh toán
| Quy tắc | Mô tả |
|---|---|
| BR-82 | Tài xế hủy Booking ở trạng thái `WaitingOwner` hoặc `PendingPayment`: hủy miễn phí, tồn kho dịch vụ phụ và điểm tích lũy (nếu đã dùng) được hoàn trả. |

### 8.3 Chủ trạm hủy khẩn cấp (Emergency Cancel)
| Quy tắc | Giá trị |
|---|---|
| BR-83 — Hạn mức | Mỗi Owner tối đa **1 lần / tháng**. Tài xế được hoàn **100%** tiền. |
| BR-84 — Phạt vi phạm | Nếu vượt quá 1 lần/tháng: tài khoản Owner bị chuyển sang `SUSPENDED`, trạm bị khóa **30 ngày** (`BannedUntil = Now + 30 ngày`), tự động hủy toàn bộ Booking đang hoạt động. |

---

## 9. HÓA ĐƠN & QUYẾT TOÁN TÀI CHÍNH (Invoice & Settlement)

### 9.1 Tạo & Xác nhận Hóa đơn
| Quy tắc | Giá trị mặc định |
|---|---|
| BR-85 | Khi phiên sạc kết thúc, hệ thống tự động tạo hóa đơn ở trạng thái `PendingConfirm`. Tài xế có **24 giờ** (`Invoice_AutoConfirm_Hours = 24`) để xác nhận hoặc khiếu nại. |
| BR-86 | Quá 24 giờ không phản hồi: hóa đơn được **tự động xác nhận** bởi `InvoiceAutoConfirmJob` (chạy mỗi 5 phút), tiền được quyết toán theo Rule 9.2. |
| BR-87 | Hệ thống gửi **thông báo nhắc nhở** cho tài xế **1 giờ** trước khi tự động xác nhận (`Reminder_Window_Hours = 1`). |

### 9.2 Phân chia doanh thu
| Thành phần | Tỷ lệ mặc định | Ví đích |
|---|---|---|
| Thuế GTGT (VAT) | **8%** (`VAT_Rate = 0.08`) | `TAX_HOLD` |
| Phí nền tảng (Platform Fee) | **5%** (`Platform_Fee_Rate = 0.05`) | `PLATFORM_REVENUE` |
| Doanh thu ròng Owner | **87%** (phần còn lại) | Ví Owner |

> **Công thức:** GrossAmount = TotalAmount + PointsDiscountAmount → VAT = Gross × 8% → Fee = Gross × 5% → NetOwner = Gross − VAT − Fee.

### 9.3 Hệ thống Ví nội bộ (Wallet)
| Ví | Mục đích |
|---|---|
| `ESCROW` | Tạm giữ tiền tài xế từ lúc thanh toán đến lúc quyết toán hoặc giải quyết tranh chấp. |
| `CLEARING` | Ví đối soát — nhận tiền webhook không khớp Booking để Admin xử lý thủ công. |
| `PLATFORM_REVENUE` | Doanh thu nền tảng (phí 5%). |
| `TAX_HOLD` | Giữ thuế GTGT 8%. |
| Ví `Driver` / `Owner` | Ví cá nhân của người dùng. |

### 9.4 Điểm tích lũy (Loyalty Points)
| Quy tắc | Giá trị mặc định |
|---|---|
| BR-88 | Sau mỗi phiên sạc hoàn thành, tài xế được tích lũy **5%** tổng số tiền đã thanh toán thành điểm (`Loyalty_Earn_Rate = 0.05`). Ví dụ: thanh toán 100.000đ → nhận 5.000 điểm. |
| BR-89 | Điểm tích lũy có thể được sử dụng để **giảm giá** cho các Booking tiếp theo (tối đa 100% giá trị Booking). |
| BR-90 | Điểm đã sử dụng được **hoàn trả** nếu Booking bị hủy hoặc hết hạn thanh toán. |

---

## 10. RÚT TIỀN TỪ VÍ (Withdraw)

### 10.1 Quy tắc Rút tiền
| Quy tắc | Giá trị |
|---|---|
| BR-91 — Số tiền tối thiểu | **50.000 VND** |
| BR-92 — Chống rửa tiền (AML) | Owner phải có `KycStatus = Approved` mới được rút tiền. Owner chưa duyệt KYC bị **chặn 100% chức năng rút tiền**. |
| BR-93 — Đóng băng số dư | Khi tạo yêu cầu rút, số tiền tương ứng ngay lập tức chuyển từ `AvailableBalance` sang `FrozenBalance` bằng **SQL Atomic** (`FreezeIfSufficientAsync`) để chống Race Condition khi rút 2 yêu cầu cùng lúc. |

### 10.2 Máy trạng thái Rút tiền
```
Pending → Approved → TransferCompleted → Completed
       ↘ Rejected (hoàn tiền về AvailableBalance)
                                       ↘ IssueReported → ForceCompleted (Admin ép hoàn tất)
                                                       ↘ Resolved (Admin hoàn tiền)
```

### 10.3 Tự động xác nhận
| Quy tắc | Giá trị mặc định |
|---|---|
| BR-94 | Khi Admin chuyển khoản xong và upload ảnh bill (`TransferCompleted`), User có **24 giờ** (`Withdraw_AutoConfirm_Hours = 24`) để xác nhận nhận tiền. |
| BR-95 | Quá 24 giờ: hệ thống tự động xác nhận `Completed` bởi `WithdrawAutoConfirmJob` (chạy mỗi 5 phút). Tiền bị trừ vĩnh viễn khỏi `FrozenBalance`. |
| BR-96 | Nhắc nhở User **1 giờ** trước khi tự động xác nhận. |

### 10.4 Admin ép hoàn tất (Force Complete)
| Quy tắc | Mô tả |
|---|---|
| BR-97 | Khi User spam báo lỗi giả (status `IssueReported`), Admin có quyền **ép hoàn tất** yêu cầu rút tiền bằng cách upload ảnh bằng chứng chuyển khoản thành công. |
| BR-98 | Endpoint: `PUT /api/admin/withdraws/{id}/force-complete` (multipart/form-data). Chỉ chấp nhận file ảnh (.jpg/.png/.webp, tối đa 5MB). |
| BR-99 | Sau khi ép hoàn tất: tiền bị trừ vĩnh viễn khỏi `FrozenBalance`, ghi Ledger, gửi thông báo cho User. |

### 10.5 Bảo lưu tài sản sau khi bị khóa/chấm dứt
| Quy tắc | Mô tả |
|---|---|
| BR-100 | Khi tài khoản bị `BANNED` hoặc hợp đồng bị `Terminated`, hệ thống đình chỉ toàn bộ quyền sinh lời tương lai (khóa trạm, hủy Booking, cấm đăng nhập). **Tuy nhiên**, số dư `AvailableBalance` đã tích lũy hợp pháp trước đó **vẫn được bảo lưu quyền rút tiền** (không tịch thu). Lý do: các khoản tranh chấp đang mở đều đã được giữ tại ví `ESCROW`, số tiền trong AvailableBalance là tiền "sạch". |

---

## 11. QUẢN LÝ TÀI KHOẢN NGÂN HÀNG (Bank Account)

| Quy tắc | Mô tả |
|---|---|
| BR-101 | Mỗi người dùng có thể đăng ký nhiều tài khoản ngân hàng. Tại mỗi thời điểm chỉ có **1 tài khoản mặc định** (`IsDefault = true`). |
| BR-102 | **Không được xóa** tài khoản ngân hàng nếu đang có yêu cầu rút tiền ở trạng thái chưa hoàn tất (`Pending`, `Approved`, `TransferCompleted`) liên quan đến tài khoản đó. |

---

## 12. KHIẾU NẠI & TRANH CHẤP (Dispute)

### 12.1 Máy trạng thái Dispute
```
WaitingOwnerEvidence → PendingReview → ResolvedRefund (Driver thắng)
                                     ↘ ResolvedPayout (Owner thắng)
```

### 12.2 Quy tắc chi tiết
| Quy tắc | Giá trị mặc định |
|---|---|
| BR-103 — Hạn mức khiếu nại | Mỗi tài xế tối đa **3 lần / tháng** (`Dispute_Limit_Per_Month = 3`). |
| BR-104 — Điều kiện khiếu nại | Chỉ được khiếu nại khi Booking ở trạng thái `CompletedPendingInvoice`. Mỗi Booking chỉ được tạo **1 Dispute duy nhất**. |
| BR-105 — Thời hạn nộp bằng chứng (Owner) | **24 giờ** (`Dispute_OwnerEvidence_Hours = 24`). Quá hạn: tự động chuyển sang `PendingReview`. |
| BR-106 — Thời hạn phán quyết (Admin) | **48 giờ** (`Dispute_AdminReview_Hours = 48`). Quá hạn: hệ thống **tự động xử lý** bởi `DisputeAutoResolveJob` (chạy mỗi 5 phút). |
| BR-107 — Đóng băng tài chính | Khi Dispute được tạo: hóa đơn → `UnderDispute`, tiền trong ESCROW chuyển từ `AvailableBalance` sang `FrozenBalance`. Tiền **bị đóng băng hoàn toàn**, chưa phân bổ cho bên nào. |
| BR-108 — Kết quả `ResolvedRefund` | Tiền hoàn 100% từ ESCROW về ví tài xế. Điểm tích lũy (nếu đã dùng) cũng được hoàn trả. |
| BR-109 — Kết quả `ResolvedPayout` | Tiền quyết toán từ ESCROW cho Owner theo công thức phân chia doanh thu (Mục 9.2). Nền tảng bù tiền chiết khấu điểm thưởng (`PointsSubsidy`) từ `PLATFORM_REVENUE` vào ESCROW trước khi settle. |
| BR-110 — Nhắc nhở deadline | Hệ thống tự động gửi nhắc nhở cho Owner (nộp bằng chứng) và Admin (phán quyết) **1 giờ** trước mỗi deadline. |
| BR-111 — Admin chỉ resolve khi PendingReview | Admin **chỉ được phán quyết** khi Dispute ở trạng thái `PendingReview` (tức Owner đã nộp bằng chứng hoặc quá hạn 24h). Không thể resolve khi còn `WaitingOwnerEvidence`. |

### 12.3 Phán quyết tự động mặc định (Auto-Resolve)
| Kịch bản | Kết quả mặc định |
|---|---|
| Owner **không phản hồi** trong 24h | **Driver thắng** (`ResolvedRefund`): hoàn 100% tiền + hoàn điểm cho Driver. |
| Admin **không phân xử** trong 48h (kể từ khi Owner nộp bằng chứng) | **Cả 2 bên được đền bù**: Driver hoàn 100% tiền từ ESCROW (`ResolvedRefund`). Owner nhận doanh thu ròng (đã trừ thuế + phí) từ ví `PLATFORM_REVENUE`. Nền tảng chịu chi phí do Admin chậm trễ. |

### 12.4 Hệ thống xử phạt vi phạm (Strike System)
| Đối tượng | Ngưỡng | Hình phạt | Chi tiết |
|---|---|---|---|
| **Tài xế** | Thua ≥ **3 lần / tháng** | `SUSPENDED` **30 ngày** | Tự động hủy tất cả Booking đang hoạt động, hoàn tiền + hoàn kho. |
| **Trạm sạc** | Thua ≥ **5 lần / tháng** | `Inactive` **30 ngày** (`BannedUntil`) | Tự động hủy tất cả Booking đang hoạt động trên trạm đó. |

> **Cảnh cáo sớm:** Hệ thống tự động gửi **thông báo cảnh cáo** mỗi khi Driver/Station thua 1 lượt, cho biết còn bao nhiêu lượt trước khi bị phạt. Ví dụ: "Bạn đã thua 2/3 lượt khiếu nại trong tháng. Còn 1 lượt nữa tài khoản sẽ bị đình chỉ."

> **Chống phạt dồn:** Hệ thống kiểm tra trạng thái hiện tại trước khi phạt — nếu User/Station đang trong thời gian bị phạt rồi, sẽ **không** tăng `BanCount` thêm lần nữa.

### 12.5 Bằng chứng & Tệp đính kèm
| Quy tắc | Giá trị |
|---|---|
| BR-112 — Kích thước tối đa | **10 MB / file** |
| BR-113 — Loại file cho phép | `.jpg`, `.jpeg`, `.png`, `.gif`, `.webp`, `.bmp`, `.mp4`, `.avi`, `.mov`, `.webm`, `.pdf` |
| BR-114 — Lưu trữ | Upload lên Firebase Storage, đường dẫn `disputes/{disputeId}/`. Phân loại tự động: `image`, `video`, `document`. |

---

## 13. NHẮN TIN THỜI GIAN THỰC (Chat — SignalR)

| Quy tắc | Mô tả |
|---|---|
| BR-115 | Mỗi cuộc hội thoại Chat được **gắn cố định vào 1 BookingId**. Cùng cặp Driver–Owner nhưng khác Booking sẽ tạo ra các cuộc hội thoại riêng biệt (phục vụ truy xuất bằng chứng khi tranh chấp). |
| BR-116 | Chỉ **đúng 2 người** (Driver và Owner của Booking đó) có quyền đọc/ghi trong cuộc hội thoại. Admin không có quyền truy cập. |
| BR-117 | Tin nhắn được truyền **thời gian thực** qua kênh SignalR Hub (`chat_{conversationId}`). Giới hạn nội dung tin nhắn: **1–1.000 ký tự**. |
| BR-118 | Hỗ trợ đánh dấu **đã đọc** (`MarkAsRead`): đánh dấu tất cả tin nhắn của người kia là đã đọc, kèm broadcast sự kiện `MessagesRead` cho cả nhóm. |
| BR-119 | **Giới hạn gửi tin nhắn theo trạng thái Booking**: Chỉ cho phép gửi tin nhắn mới khi Booking ở trạng thái `Paid`, `CheckedIn`, `CompletedPendingInvoice`, hoặc `Disputed`. Các trạng thái khác (`WaitingOwner`, `PendingPayment`, `Completed`, `Cancelled`, `NoShow`, `Rejected`, `Expired`) chỉ cho phép **xem lại lịch sử chat**, không gửi được tin nhắn mới. |

---

## 14. ĐÁNH GIÁ & XẾP HẠNG TRẠM SẠC (Review & Rating)

| Quy tắc | Mô tả |
|---|---|
| BR-120 | Tài xế chỉ được đánh giá trạm sạc khi Booking ở trạng thái `Completed`. Mỗi Booking chỉ được đánh giá **1 lần duy nhất**. |
| BR-121 | Đánh giá bao gồm: Điểm sao (1–5), Nhận xét (text), và tùy chọn **Ẩn danh** (`IsAnonymous`). Khi ẩn danh, tên hiển thị là "Ẩn danh" và không hiển thị ảnh đại diện. |
| BR-122 | Owner có quyền **phản hồi** mỗi đánh giá đúng 1 lần (`OwnerReply`). |
| BR-123 | Sau mỗi đánh giá mới, hệ thống **tự động tính lại** điểm trung bình (`AverageRating`) và tổng số lượt đánh giá (`TotalReviews`) của trạm sạc. |

---

## 15. YÊU THÍCH TRẠM SẠC (Favorite)

| Quy tắc | Mô tả |
|---|---|
| BR-124 | Tài xế có thể thêm/xóa trạm sạc khỏi danh sách yêu thích. Mỗi cặp (Driver, Station) chỉ tồn tại **1 bản ghi** duy nhất. |
| BR-125 | Hệ thống cung cấp API **Top trạm được yêu thích nhất** (theo số lượt Favorite), phục vụ gợi ý trên trang chủ. |

---

## 16. TÌM KIẾM TRẠM SẠC CÔNG KHAI (Public Station)

| Quy tắc | Mô tả |
|---|---|
| BR-126 | Chỉ hiển thị trạm có `ApprovalStatus = Approved` và `OperationalStatus = Active`. |
| BR-127 | Hỗ trợ lọc theo: từ khóa, điểm rating tối thiểu, khoảng cách (Haversine), khung giờ khả dụng, và ngày nghỉ. |
| BR-128 | Khi lọc theo khung giờ: hệ thống kiểm tra từng Slot trong trạm xem có Booking chồng lấn không (có tính Buffer), chỉ trả về trạm có ít nhất **1 Slot trống**. |
| BR-129 | Sắp xếp theo: khoảng cách (`distance`), điểm đánh giá (`rating`), số lượt đánh giá (`reviews`). Mặc định: khoảng cách nếu có tọa độ, ngược lại theo tên. |

---

## 17. QUẢN LÝ TÀI KHOẢN BỞI ADMIN (Account Management)

### 17.1 Khóa/Mở khóa tài khoản
| Quy tắc | Mô tả |
|---|---|
| BR-130 | Admin **không được phép** khóa tài khoản Admin khác hoặc chính mình. |
| BR-131 | Khi khóa tài khoản Driver: toàn bộ Booking đang hoạt động (`WaitingOwner`, `PendingPayment`, `Paid`) được **tự động hủy**, hoàn tiền + hoàn điểm + hoàn kho. |
| BR-132 | Khi khóa tài khoản Owner: toàn bộ trạm sạc chuyển `Inactive`, toàn bộ Booking đang hoạt động trên các trạm đó được **tự động hủy**, hoàn tiền cho tài xế. |
| BR-133 | Khi mở khóa: trạng thái trả về `ACTIVE`, bộ đếm vi phạm (`BanCount`) được reset về 0. |

### 17.2 Tự động mở khóa (Auto-Unban)
| Quy tắc | Mô tả |
|---|---|
| BR-134 | `UnbanAutoJob` (chạy mỗi 1 phút) tự động mở khóa cho User hoặc Station có `BannedUntil ≤ Now`. Trạng thái User trả về `ACTIVE`, Station trả về `Active`. |

---

## 18. MẬT KHẨU CẤP 2 & BẢO MẬT ADMIN (Secondary Password)

| Quy tắc | Mô tả |
|---|---|
| BR-135 | Mọi thao tác thay đổi Cấu hình Hệ thống (SystemConfig) đều **bắt buộc nhập đúng Mật khẩu Cấp 2**. Đây là lớp bảo vệ chống kịch bản hacker đã chiếm được phiên đăng nhập Admin. |
| BR-136 | Mật khẩu Cấp 2 chỉ được thiết lập **1 lần** (sau đó không thể đổi trực tiếp). Để reset, Admin phải xác thực qua OTP gửi tới email. |

---

## 19. THÔNG BÁO ĐA KÊNH (Notification)

| Quy tắc | Mô tả |
|---|---|
| BR-137 | Mọi sự kiện nghiệp vụ quan trọng (duyệt trạm, thanh toán thành công, phiên sạc kết thúc, tranh chấp, khóa tài khoản...) đều tự động gửi **Thông báo trong ứng dụng** (In-app Notification). |
| BR-138 | Đồng thời, nếu người dùng đã xác thực email: hệ thống gửi **Email CC** với nội dung HTML chuyên nghiệp. Nếu gửi email thất bại, thông báo in-app vẫn được lưu (fail-safe, không throw exception). |
| BR-139 | Phân loại thông báo: `System`, `Booking`, `Payment`, `Dispute`, `StationApproval`, `Wallet`. |

---

## 20. NHẮC NHỞ TỰ ĐỘNG TRƯỚC DEADLINE (Deadline Reminder)

`DeadlineReminderJob` chạy mỗi **5 phút**, quét và gửi nhắc nhở **1 giờ** trước mỗi sự kiện tự động:

| Kịch bản | Người nhận | Nội dung |
|---|---|---|
| Hóa đơn sắp tự động xác nhận | Tài xế | "Hóa đơn sẽ tự động xác nhận sau 1 giờ. Nếu có vấn đề, hãy khiếu nại ngay." |
| Yêu cầu rút tiền sắp tự động xác nhận | User | "Yêu cầu rút tiền sẽ tự động xác nhận. Nếu chưa nhận, hãy báo cáo." |
| Deadline nộp bằng chứng tranh chấp | Owner | "Khiếu nại sẽ tự động phán quyết do không nhận được phản hồi." |
| Deadline phán quyết tranh chấp | Tất cả Admin | "Dispute sắp tự động xử lý. Hệ thống sẽ hoàn tiền cho Driver và đền bù Owner từ ví nền tảng." |
| Sắp đến giờ sạc (Booking đã thanh toán) | Tài xế + Owner | "Sắp đến giờ sạc! Chuẩn bị check-in / Chuẩn bị slot sẵn sàng." |

---

## 21. PHÂN TÍCH DỮ LIỆU (Analytics & Dashboard)

| Quy tắc | Mô tả |
|---|---|
| BR-140 | Hệ thống cung cấp Dashboard phân tích cho cả **Admin** (doanh thu toàn sàn, số trạm, tỷ lệ hủy) và **Owner** (doanh thu theo trạm, dịch vụ phụ bán chạy, hiệu suất rating). |
| BR-141 | Hỗ trợ xem doanh thu theo giai đoạn: Tháng (30 ngày), Quý (90 ngày), Năm (365 ngày), hoặc Toàn bộ. Bao gồm báo cáo VAT chi tiết theo tháng. |

---

## 22. HỒ SƠ CÁ NHÂN (Driver Profile & Owner Profile)

| Quy tắc | Mô tả |
|---|---|
| BR-142 | Tài xế có thể bổ sung/cập nhật thông tin xe: Loại xe (`VehicleType`), Biển số (`LicensePlate`), Số giấy phép lái xe (`LicenseNumber`). |
| BR-143 | Tất cả người dùng có thể upload **ảnh đại diện** lên Firebase Storage. Ảnh cũ tự động bị xóa khi upload ảnh mới. |

---

## PHỤ LỤC A: TỔNG HỢP GIÁ TRỊ CẤU HÌNH MẶC ĐỊNH (SYSTEM CONFIG)

| Khóa cấu hình | Giá trị mặc định | Đơn vị | Mô tả |
|---|---|---|---|
| `Min_Booking_Lead_Minutes` | 30 | phút | Thời gian đặt trước tối thiểu |
| `Slot_Buffer_Minutes` | 15 | phút | Khoảng đệm giữa 2 Booking |
| `Payment_Expiry_Minutes` | 30 | phút | Thời hạn thanh toán |
| `CheckIn_Window_Minutes` | 15 | phút | Cho phép check-in sớm |
| `RefundPolicy100_Hrs` | 2 | giờ | Ngưỡng hoàn 100% |
| `RefundPolicy50_Hrs` | 1 | giờ | Ngưỡng hoàn 50% |
| `VAT_Rate` | 0.08 | tỷ lệ | Thuế GTGT 8% |
| `Platform_Fee_Rate` | 0.05 | tỷ lệ | Phí nền tảng 5% |
| `Loyalty_Earn_Rate` | 0.05 | tỷ lệ | Tích điểm 5% |
| `Invoice_AutoConfirm_Hours` | 24 | giờ | Tự động xác nhận hóa đơn |
| `Withdraw_AutoConfirm_Hours` | 24 | giờ | Tự động xác nhận rút tiền |
| `Reminder_Window_Hours` | 1 | giờ | Nhắc nhở trước deadline |
| `Dispute_Limit_Per_Month` | 3 | lần | Hạn mức khiếu nại/tháng |
| `Dispute_OwnerEvidence_Hours` | 24 | giờ | Deadline nộp bằng chứng |
| `Dispute_AdminReview_Hours` | 48 | giờ | Deadline phán quyết |
| `OTP_Expiry_Minutes` | 5 | phút | Thời hạn OTP |
| `OTP_Cooldown_Seconds` | 30 | giây | Cooldown gửi lại OTP |
| `Waiting_Owner_Timeout_Minutes` | (cấu hình) | phút | Thời gian chờ Owner duyệt Booking |

---

## PHỤ LỤC B: DANH SÁCH TÁC VỤ NỀN TỰ ĐỘNG (BACKGROUND JOBS)

| Job | Tần suất | Chức năng |
|---|---|---|
| `PaymentExpiryJob` | Mỗi 1 phút | Hủy Booking hết hạn thanh toán, hoàn kho + hoàn điểm. |
| `NoShowJob` | Mỗi 1 phút | Xử lý Booking quá giờ: Paid không check-in quá EndTime, CheckedIn quá EndTime (auto-stop). |
| `UnbanAutoJob` | Mỗi 1 phút | Tự động mở khóa User/Station hết hạn đình chỉ. |
| `InvoiceAutoConfirmJob` | Mỗi 5 phút | Tự động xác nhận hóa đơn quá 24h, quyết toán doanh thu. |
| `WithdrawAutoConfirmJob` | Mỗi 5 phút | Tự động xác nhận rút tiền quá 24h. |
| `DisputeAutoResolveJob` | Mỗi 5 phút | Tự động xử lý tranh chấp quá deadline. |
| `DeadlineReminderJob` | Mỗi 5 phút | Gửi nhắc nhở 1h trước mọi deadline tự động. |
| `ContractExpiryJob` | Mỗi 6 giờ | Nhắc nhở hợp đồng sắp hết hạn (30 ngày), tự động gia hạn. |
| `EmailVerificationCleanupJob` | Mỗi 30 phút | Xóa tài khoản chưa xác thực email quá 24h. |

---

## PHỤ LỤC C: SƠ ĐỒ LUỒNG TIỀN (FUND FLOW DIAGRAM)

```
Tài xế chuyển khoản (SePay/VietQR)
        │
        ▼
   ┌─────────┐    Webhook     ┌──────────┐
   │  Ngân   │ ────────────►  │ CLEARING │ (ví đối soát)
   │  Hàng   │                └────┬─────┘
   └─────────┘                     │
                     ┌─────────────┼─────────────────┐
                     │ Booking     │ Top-Up           │ Không rõ
                     ▼             ▼                  ▼
               ┌──────────┐  ┌──────────┐     Giữ CLEARING
               │  ESCROW  │  │ Ví Driver│     (Admin xử lý)
               └────┬─────┘  └──────────┘
                    │
           Khi Invoice xác nhận
         ┌──────┬───┴────┬──────┐
         ▼      ▼        ▼      ▼
     TAX_HOLD  PLATFORM  Ví     (Dispute
      (8%)    REVENUE   Owner    → giữ
              (5%)      (87%)    ESCROW)
```

