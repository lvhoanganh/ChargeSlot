-- =====================================================
-- RESET DATABASE
-- =====================================================
-- IF DB_ID('DemoSlot') IS NOT NULL
-- BEGIN
--     ALTER DATABASE DemoSlot SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
--     DROP DATABASE DemoSlot;
-- END
-- GO

CREATE DATABASE DemoSlot;
GO
USE DemoSlot;
GO

-- =====================================================
-- ROLE (1 - N USER)
-- =====================================================
CREATE TABLE Role (
    role_id   INT IDENTITY PRIMARY KEY,
    name      NVARCHAR(50) NOT NULL UNIQUE
);

-- =====================================================
-- USER
-- =====================================================
CREATE TABLE [User] (
    id                INT IDENTITY PRIMARY KEY,
    role_id           INT NOT NULL,
    fullname          NVARCHAR(200) NOT NULL,
    phone_number      VARCHAR(20)   NOT NULL UNIQUE,
    email             VARCHAR(200)  UNIQUE,
    avatar_url        NVARCHAR(300),
    password_hash     NVARCHAR(300) NOT NULL,
    is_phone_verified BIT           DEFAULT 0,
    status            NVARCHAR(50)  DEFAULT 'ACTIVE',
    created_at        DATETIME2     DEFAULT SYSDATETIME(),
    updated_at        DATETIME2,
    FOREIGN KEY (role_id) REFERENCES Role(role_id)
);

-- =====================================================
-- OWNER & DRIVER (1-1 WITH USER)
-- =====================================================
CREATE TABLE Owner (
    owner_id      INT IDENTITY PRIMARY KEY,
    user_id       INT UNIQUE NOT NULL,
    business_name NVARCHAR(255) NOT NULL,
    tax_code      NVARCHAR(100) NOT NULL,
    FOREIGN KEY (user_id) REFERENCES [User](id)
);

CREATE TABLE Driver (
    user_id        INT PRIMARY KEY,
    vehicle_type   NVARCHAR(100),
    license_plate  NVARCHAR(50),
    license_number NVARCHAR(50),
    created_at     DATETIME2 DEFAULT SYSDATETIME(),
    FOREIGN KEY (user_id) REFERENCES [User](id)
);

-- =====================================================
-- CHARGING STATION
-- (layout_image_url, layout_width, layout_height cho sơ đồ trạm sạc)
-- =====================================================
CREATE TABLE ChargingStation (
    station_id         INT IDENTITY PRIMARY KEY,
    owner_id           INT NOT NULL,
    name               NVARCHAR(255) NOT NULL,
    address            NVARCHAR(300) NOT NULL,
    description        NVARCHAR(MAX),
    latitude           DECIMAL(9,6),
    longitude          DECIMAL(9,6),
    -- Sơ đồ mặt bằng trạm sạc
    layout_image_url   NVARCHAR(500) NULL,      -- Ảnh nền sơ đồ trạm
    layout_width       DECIMAL(10,2) NULL,       -- Chiều rộng canvas (%)
    layout_height      DECIMAL(10,2) NULL,       -- Chiều cao canvas (%)
    approval_status    NVARCHAR(50)  DEFAULT 'PENDING',
    operational_status NVARCHAR(50)  DEFAULT 'INACTIVE',
    created_at         DATETIME2     DEFAULT SYSDATETIME(),
    FOREIGN KEY (owner_id) REFERENCES Owner(owner_id)
);

CREATE TABLE StationImage (
    image_id   INT IDENTITY PRIMARY KEY,
    station_id INT NOT NULL,
    image_url  NVARCHAR(300) NOT NULL,
    FOREIGN KEY (station_id) REFERENCES ChargingStation(station_id)
);

CREATE TABLE StationOperatingHours (
    station_id  INT,
    day_of_week TINYINT CHECK (day_of_week BETWEEN 1 AND 7),
    is_closed   BIT  DEFAULT 0,
    open_time   TIME NULL,
    close_time  TIME NULL,
    PRIMARY KEY (station_id, day_of_week),
    FOREIGN KEY (station_id) REFERENCES ChargingStation(station_id)
);

-- =====================================================
-- SERVICES (dịch vụ đi kèm: rửa xe, wifi, cà phê...)
-- =====================================================
CREATE TABLE Services (
    service_id   INT IDENTITY PRIMARY KEY,
    station_id   INT NOT NULL,
    service_name NVARCHAR(200) NOT NULL,
    description  NVARCHAR(MAX),
    price        DECIMAL(18,2) NOT NULL,
    is_active    BIT DEFAULT 1,
    FOREIGN KEY (station_id) REFERENCES ChargingStation(station_id)
);

-- =====================================================
-- CHARGING SLOT
-- (position_x, position_y cho vị trí trụ sạc trên sơ đồ)
-- (price_per_hour là giá mặc định/fallback khi không có SlotPricing match)
-- =====================================================
CREATE TABLE ChargingSlot (
    slot_id        INT IDENTITY PRIMARY KEY,
    station_id     INT NOT NULL,
    slot_name      NVARCHAR(100) NOT NULL,
    connector_type NVARCHAR(100) NOT NULL,
    price_per_hour DECIMAL(18,2) NOT NULL,       -- Giá mặc định
    -- Vị trí trụ sạc trên sơ đồ (tọa độ tương đối %, responsive)
    position_x     DECIMAL(10,2) NULL,           -- % từ trái qua
    position_y     DECIMAL(10,2) NULL,           -- % từ trên xuống
    is_active      BIT           DEFAULT 1,
    status         NVARCHAR(50),
    FOREIGN KEY (station_id) REFERENCES ChargingStation(station_id)
);

-- =====================================================
-- SLOT PRICING (giá theo giờ cao điểm / thấp điểm)
--
-- Ví dụ:
--   06:00-17:00  →  20,000đ/giờ  (giờ thường)
--   17:00-21:00  →  35,000đ/giờ  (giờ cao điểm)
--   21:00-06:00  →  15,000đ/giờ  (giờ khuya)
--   CN 08:00-18:00 → 40,000đ/giờ (cuối tuần đắt hơn)
--
-- day_of_week: NULL = mọi ngày, 1=T2 ... 7=CN
-- priority: khi 2 khung giờ chồng nhau, priority cao hơn thắng
-- effective_from/to: hiệu lực theo ngày (lịch sử giá)
-- =====================================================
CREATE TABLE SlotPricing (
    pricing_id     INT IDENTITY PRIMARY KEY,
    slot_id        INT NOT NULL,
    price_per_hour DECIMAL(18,2) NOT NULL CHECK (price_per_hour >= 0),
    start_time     TIME          NOT NULL,       -- Bắt đầu khung giờ (VD: 06:00)
    end_time       TIME          NOT NULL,       -- Kết thúc khung giờ (VD: 17:00)
    day_of_week    TINYINT       NULL CHECK (day_of_week BETWEEN 1 AND 7),
    priority       INT           DEFAULT 0,      -- Ưu tiên cao hơn override
    effective_from DATETIME2     NOT NULL,        -- Có hiệu lực từ ngày
    effective_to   DATETIME2     NULL,            -- NULL = vô thời hạn
    created_at     DATETIME2     DEFAULT SYSDATETIME(),
    FOREIGN KEY (slot_id) REFERENCES ChargingSlot(slot_id)
);

-- =====================================================
-- BOOKING (N-1 DRIVER)
-- =====================================================
CREATE TABLE Booking (
    booking_id     INT IDENTITY PRIMARY KEY,
    driver_id      INT NOT NULL,
    slot_id        INT NOT NULL,
    start_time     DATETIME2    NOT NULL,
    end_time       DATETIME2    NOT NULL,
    duration_hours DECIMAL(5,2),
    status         NVARCHAR(50) DEFAULT 'PENDING',
    created_at     DATETIME2    DEFAULT SYSDATETIME(),
    FOREIGN KEY (driver_id) REFERENCES Driver(user_id),
    FOREIGN KEY (slot_id)   REFERENCES ChargingSlot(slot_id)
);

-- =====================================================
-- BOOKING SERVICES (dịch vụ đi kèm khi đặt lịch)
-- =====================================================
CREATE TABLE BookingServices (
    bookingservices_id INT IDENTITY PRIMARY KEY,
    booking_id         INT NOT NULL,
    service_id         INT NOT NULL,
    quantity           INT           DEFAULT 1,
    unit_price         DECIMAL(18,2) NOT NULL,
    total_price        DECIMAL(18,2) NOT NULL,
    FOREIGN KEY (booking_id) REFERENCES Booking(booking_id),
    FOREIGN KEY (service_id) REFERENCES Services(service_id)
);

-- =====================================================
-- CHARGING SESSION
-- =====================================================
CREATE TABLE ChargingSession (
    session_id        INT IDENTITY PRIMARY KEY,
    booking_id        INT UNIQUE NOT NULL,
    actual_start_time DATETIME2,
    actual_end_time   DATETIME2,
    actual_duration   DECIMAL(5,2),
    FOREIGN KEY (booking_id) REFERENCES Booking(booking_id)
);

-- =====================================================
-- INVOICE
-- =====================================================
CREATE TABLE Invoice (
    invoice_id      INT IDENTITY PRIMARY KEY,
    booking_id      INT UNIQUE NOT NULL,
    charging_amount DECIMAL(18,2),
    service_amount  DECIMAL(18,2),
    vat_amount      DECIMAL(18,2),
    platform_fee    DECIMAL(18,2),
    total_amount    DECIMAL(18,2),
    status          NVARCHAR(50),
    created_at      DATETIME2 DEFAULT SYSDATETIME(),
    FOREIGN KEY (booking_id) REFERENCES Booking(booking_id)
);

-- =====================================================
-- PAYMENT
-- =====================================================
CREATE TABLE Payment (
    payment_id     INT IDENTITY PRIMARY KEY,
    booking_id     INT NOT NULL,
    amount         DECIMAL(18,2) NOT NULL,
    payment_method NVARCHAR(50),
    gateway_txn_ref NVARCHAR(100),
    status         NVARCHAR(50),
    paid_at        DATETIME2,
    FOREIGN KEY (booking_id) REFERENCES Booking(booking_id)
);

-- =====================================================
-- RATING
-- =====================================================
CREATE TABLE Rating (
    rating_id    INT IDENTITY PRIMARY KEY,
    booking_id   INT UNIQUE NOT NULL,
    driver_id    INT NOT NULL,
    station_id   INT NOT NULL,
    score        INT CHECK (score BETWEEN 1 AND 5),
    comment      NVARCHAR(MAX),
    is_anonymous BIT DEFAULT 0,
    created_at   DATETIME2 DEFAULT SYSDATETIME(),
    FOREIGN KEY (booking_id)  REFERENCES Booking(booking_id),
    FOREIGN KEY (driver_id)   REFERENCES Driver(user_id),
    FOREIGN KEY (station_id)  REFERENCES ChargingStation(station_id)
);

-- =====================================================
-- WALLET (hỗ trợ cả User wallet và System wallet)
-- user_id NULL = System wallet (ESCROW, PLATFORM_REVENUE, CLEARING)
-- =====================================================
CREATE TABLE UserWallet (
    wallet_id         INT IDENTITY PRIMARY KEY,
    user_id           INT NULL,
    wallet_type       NVARCHAR(20) NOT NULL DEFAULT 'USER',   -- USER | SYSTEM
    system_code       NVARCHAR(50) NULL,                       -- ESCROW, PLATFORM_REVENUE, CLEARING
    available_balance DECIMAL(18,2) DEFAULT 0,
    frozen_balance    DECIMAL(18,2) DEFAULT 0,
    created_at        DATETIME2    DEFAULT SYSDATETIME(),
    FOREIGN KEY (user_id) REFERENCES [User](id)
);

-- =====================================================
-- BANK ACCOUNT
-- =====================================================
CREATE TABLE BankAccount (
    bank_id             INT IDENTITY PRIMARY KEY,
    user_id             INT NOT NULL,
    bank_name           VARCHAR(100) NOT NULL,
    bank_account_number VARCHAR(100) NOT NULL,
    bank_account_holder VARCHAR(150) NOT NULL,
    is_default          BIT DEFAULT 0,
    FOREIGN KEY (user_id) REFERENCES [User](id)
);

-- =====================================================
-- PAYOUT REQUEST
-- =====================================================
CREATE TABLE PayoutRequest (
    payout_id            INT IDENTITY PRIMARY KEY,
    user_id              INT NOT NULL,
    bank_id              INT NOT NULL,
    amount               DECIMAL(18,2) NOT NULL,
    status               NVARCHAR(50),
    requested_at         DATETIME2 DEFAULT SYSDATETIME(),
    processed_at         DATETIME2,
    processed_by_user_id INT,                              -- Admin Id=0 (không lưu DB) → không FK
    note                 NVARCHAR(MAX),
    FOREIGN KEY (user_id) REFERENCES [User](id),
    FOREIGN KEY (bank_id) REFERENCES BankAccount(bank_id)
    -- BỎ FK processed_by_user_id vì Admin config trong appsettings, Id=0 không tồn tại trong User
);

-- =====================================================
-- LEDGER (DOUBLE ENTRY)
-- =====================================================
CREATE TABLE LedgerTransaction (
    txn_id         INT IDENTITY PRIMARY KEY,
    payout_id      INT NULL,
    reference_type NVARCHAR(50),
    reference_id   INT,
    description    NVARCHAR(255),
    created_at     DATETIME2 DEFAULT SYSDATETIME(),
    FOREIGN KEY (payout_id) REFERENCES PayoutRequest(payout_id)
);

CREATE TABLE LedgerEntry (
    entry_id   INT IDENTITY PRIMARY KEY,
    txn_id     INT NOT NULL,
    wallet_id  INT NOT NULL,
    direction  NVARCHAR(10) CHECK (direction IN ('DEBIT','CREDIT')),
    amount     DECIMAL(18,2) NOT NULL,
    created_at DATETIME2 DEFAULT SYSDATETIME(),
    FOREIGN KEY (txn_id)    REFERENCES LedgerTransaction(txn_id),
    FOREIGN KEY (wallet_id) REFERENCES UserWallet(wallet_id)
);

-- =====================================================
-- USER OTP (hỗ trợ cả đăng ký chưa có user_id)
-- user_id NULL khi gửi OTP cho số điện thoại chưa đăng ký
-- phone_number bắt buộc để lookup
-- =====================================================
CREATE TABLE UserOtp (
    otp_id       INT IDENTITY PRIMARY KEY,
    user_id      INT NULL,                          -- NULL khi chưa đăng ký (register flow)
    phone_number VARCHAR(20) NOT NULL,              -- Bắt buộc để lookup
    otp_hash     NVARCHAR(200) NOT NULL,
    expired_at   DATETIME2 NOT NULL,
    is_used      BIT DEFAULT 0,
    verified_at  DATETIME2 NULL,                    -- Thời điểm xác thực OTP thành công
    created_at   DATETIME2 DEFAULT SYSDATETIME(),
    purpose      NVARCHAR(50),                      -- REGISTER, RESET_PASSWORD,...
    FOREIGN KEY (user_id) REFERENCES [User](id)
);

-- =====================================================
-- NOTIFICATION
-- =====================================================
CREATE TABLE Notification (
    notification_id INT IDENTITY PRIMARY KEY,
    user_id         INT NOT NULL,
    title           NVARCHAR(256)  NOT NULL,
    content         NVARCHAR(MAX)  NOT NULL,
    is_read         BIT DEFAULT 0,
    created_at      DATETIME2 DEFAULT SYSDATETIME(),
    FOREIGN KEY (user_id) REFERENCES [User](id)
);

-- =====================================================
-- DISPUTE
-- =====================================================
CREATE TABLE Dispute (
    dispute_id     INT IDENTITY PRIMARY KEY,
    invoice_id     INT NOT NULL,
    driver_id      INT NOT NULL,
    reason         NVARCHAR(50)  NOT NULL,
    description    NVARCHAR(MAX) NOT NULL,
    status         NVARCHAR(50)  NOT NULL DEFAULT 'PENDING',
    admin_note     NVARCHAR(MAX),
    owner_response NVARCHAR(MAX),
    resolved_by    INT NULL,                              -- Admin Id=0 (không lưu DB) → không FK
    resolved_at    DATETIME2 NULL,
    created_at     DATETIME2 DEFAULT SYSDATETIME(),
    FOREIGN KEY (invoice_id) REFERENCES Invoice(invoice_id),
    FOREIGN KEY (driver_id)  REFERENCES Driver(user_id)
    -- BỎ FK resolved_by vì Admin config trong appsettings, Id=0 không tồn tại trong User
);

-- =====================================================
-- DISPUTE EVIDENCE
-- =====================================================
CREATE TABLE DisputeEvidence (
    evidence_id INT IDENTITY PRIMARY KEY,
    dispute_id  INT NOT NULL,
    file_url    NVARCHAR(MAX) NOT NULL,
    file_type   NVARCHAR(100),
    created_at  DATETIME2 DEFAULT SYSDATETIME(),
    FOREIGN KEY (dispute_id) REFERENCES Dispute(dispute_id)
);

-- =====================================================
-- INDEXES (tối ưu hiệu suất truy vấn)
-- =====================================================

-- Tìm station theo owner
CREATE INDEX IX_ChargingStation_OwnerId ON ChargingStation(owner_id);

-- Tìm slot theo station
CREATE INDEX IX_ChargingSlot_StationId ON ChargingSlot(station_id);

-- Tìm booking theo driver / slot / status
CREATE INDEX IX_Booking_DriverId ON Booking(driver_id);
CREATE INDEX IX_Booking_SlotId   ON Booking(slot_id);
CREATE INDEX IX_Booking_Status   ON Booking(status);

-- Tìm pricing theo slot
CREATE INDEX IX_SlotPricing_SlotId ON SlotPricing(slot_id);

-- Tìm payment theo booking
CREATE INDEX IX_Payment_BookingId ON Payment(booking_id);

-- Tìm notification theo user (chưa đọc trước)
CREATE INDEX IX_Notification_UserId ON Notification(user_id, is_read);

-- Tìm ledger entry theo wallet
CREATE INDEX IX_LedgerEntry_WalletId ON LedgerEntry(wallet_id);

-- Tìm OTP theo phone number
CREATE INDEX IX_UserOtp_PhoneNumber ON UserOtp(phone_number);

-- Tìm rating theo station
CREATE INDEX IX_Rating_StationId ON Rating(station_id);

-- Tìm dịch vụ theo station
CREATE INDEX IX_Services_StationId ON Services(station_id);

-- Tìm station image theo station
CREATE INDEX IX_StationImage_StationId ON StationImage(station_id);

-- Tìm bank account theo user
CREATE INDEX IX_BankAccount_UserId ON BankAccount(user_id);

-- Tìm payout theo user
CREATE INDEX IX_PayoutRequest_UserId ON PayoutRequest(user_id);

-- Tìm dispute theo invoice
CREATE INDEX IX_Dispute_InvoiceId ON Dispute(invoice_id);

-- =====================================================
-- SEED: System Wallets
-- =====================================================
-- Admin config trong appsettings.json → không cần seed vào DB
INSERT INTO Role (name) VALUES ('Owner'), ('Driver');

-- System wallets (không thuộc user nào)
INSERT INTO UserWallet (user_id, wallet_type, system_code, available_balance, frozen_balance)
VALUES
    (NULL, 'SYSTEM', 'ESCROW',            0, 0),
    (NULL, 'SYSTEM', 'PLATFORM_REVENUE',  0, 0),
    (NULL, 'SYSTEM', 'CLEARING',          0, 0);
GO
