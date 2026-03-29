const API_BASE_URL = import.meta.env.VITE_BASE_URL || "https://chargeslot-api-f8b5brexe2b0ekhp.japaneast-01.azurewebsites.net/api";

/**
 * Lấy token từ localStorage
 */
function getToken() {
    return localStorage.getItem("accessToken");
}

/**
 * Lưu thông tin auth sau khi login
 */
export function saveAuth(authResponse) {
    localStorage.setItem("accessToken", authResponse.accessToken);
    localStorage.setItem("userId", authResponse.userId);
    localStorage.setItem("role", authResponse.role);
    if (authResponse.refreshToken) {
        localStorage.setItem("refreshToken", authResponse.refreshToken);
    }
    if (authResponse.expiresAtUtc) {
        localStorage.setItem("expiresAtUtc", authResponse.expiresAtUtc);
    }
}

/**
 * Xóa auth khi logout
 */
export function clearAuth() {
    localStorage.removeItem("accessToken");
    localStorage.removeItem("userId");
    localStorage.removeItem("role");
    localStorage.removeItem("refreshToken");
    localStorage.removeItem("expiresAtUtc");
    localStorage.removeItem("activeChargingBookingId"); // Xóa cache phiên sạc
}

/**
 * Kiểm tra đã login chưa
 */
export function isAuthenticated() {
    return !!getToken();
}

/**
 * Lấy role hiện tại
 */
export function getCurrentRole() {
    return localStorage.getItem("role");
}

/**
 * Fetch wrapper — tự động gắn JWT token + xử lý lỗi
 */
export async function apiFetch(endpoint, options = {}) {
    const token = getToken();

    const headers = {
        "Content-Type": "application/json",
        ...options.headers,
    };

    if (token) {
        headers["Authorization"] = `Bearer ${token}`;
    }

    const response = await fetch(`${API_BASE_URL}${endpoint}`, {
        ...options,
        headers,
    });

    // 401 → token hết hạn → logout
    if (response.status === 401) {
        clearAuth();
        window.location.href = "/login";
        throw new Error("Phiên đăng nhập hết hạn");
    }

    // 403 → tài khoản bị vô hiệu hóa → force logout
    if (response.status === 403) {
        const body = await response.json().catch(() => ({}));
        if (body.message && body.message.includes("vô hiệu hóa")) {
            clearAuth();
            window.location.href = "/login?banned=true";
            throw new Error("Tài khoản đã bị vô hiệu hóa");
        }
        throw new Error(body.message || "Bạn không có quyền thực hiện thao tác này");
    }

    // 204 No Content
    if (response.status === 204) {
        return null;
    }

    // Lỗi khác
    if (!response.ok) {
        const error = await response.json().catch(() => ({}));
        throw new Error(error.message || error.error || `Lỗi ${response.status}`);
    }

    return response.json();
}

/**
 * Fetch wrapper cho multipart/form-data (upload file)
 * KHÔNG set Content-Type — browser tự thêm boundary
 */
async function apiFetchFormData(endpoint, formData, method = "POST") {
    const token = getToken();
    const headers = {};
    if (token) {
        headers["Authorization"] = `Bearer ${token}`;
    }

    const response = await fetch(`${API_BASE_URL}${endpoint}`, {
        method,
        headers,
        body: formData,
    });

    if (response.status === 401) {
        clearAuth();
        window.location.href = "/login";
        throw new Error("Phiên đăng nhập hết hạn");
    }

    if (response.status === 403) {
        const body = await response.json().catch(() => ({}));
        if (body.message && body.message.includes("vô hiệu hóa")) {
            clearAuth();
            window.location.href = "/login?banned=true";
            throw new Error("Tài khoản đã bị vô hiệu hóa");
        }
        throw new Error(body.message || "Bạn không có quyền thực hiện thao tác này");
    }

    if (response.status === 204) return null;

    if (!response.ok) {
        const error = await response.json().catch(() => ({}));
        throw new Error(error.message || error.error || `Lỗi ${response.status}`);
    }

    return response.json();
}

// ============================
// AUTH
// ============================

export const authApi = {
    login: (phoneNumber, password) =>
        apiFetch("/auth/login", {
            method: "POST",
            body: JSON.stringify({ phoneNumber, password }),
        }),

    adminLogin: (username, password) =>
        apiFetch("/auth/admin/login", {
            method: "POST",
            body: JSON.stringify({ username, password }),
        }),

    register: (data) =>
        apiFetch("/auth/register", {
            method: "POST",
            body: JSON.stringify(data),
        }),

    resetPassword: (data) =>
        apiFetch("/auth/reset-password", {
            method: "POST",
            body: JSON.stringify(data),
        }),

    /** Đổi mật khẩu (cần đăng nhập, nhập mật khẩu cũ) */
    changePassword: (currentPassword, newPassword) =>
        apiFetch("/auth/change-password", {
            method: "POST",
            body: JSON.stringify({ currentPassword, newPassword }),
        }),
};

// ============================
// CHARGING STATION (Owner)
// ============================

export const stationApi = {
    getAll: () => apiFetch("/stations"),

    getById: (id) => apiFetch(`/stations/${id}`),

    create: (data) =>
        apiFetch("/stations", {
            method: "POST",
            body: JSON.stringify(data),
        }),

    update: (id, data) =>
        apiFetch(`/stations/${id}`, {
            method: "PUT",
            body: JSON.stringify(data),
        }),

    delete: (id) =>
        apiFetch(`/stations/${id}`, { method: "DELETE" }),

    submitForApproval: (id) =>
        apiFetch(`/stations/${id}/submit`, { method: "POST" }),

    /** Owner bật/tắt trạm (Active/Inactive) */
    updateStatus: (id, operationalStatus) =>
        apiFetch(`/stations/${id}/status`, {
            method: "PATCH",
            body: JSON.stringify({ operationalStatus }),
        }),
};

// ============================
// CHARGING STATION (Admin)
// ============================

export const adminStationApi = {
    getPending: () => apiFetch("/admin/stations/pending"),

    getById: (id) => apiFetch(`/admin/stations/${id}`),

    review: (id, isApproved, adminNote) =>
        apiFetch(`/admin/stations/${id}/review`, {
            method: "POST",
            body: JSON.stringify({ isApproved, adminNote }),
        }),
};

// ============================
// CHARGING SLOT
// ============================

export const slotApi = {
    getAll: (stationId) => apiFetch(`/stations/${stationId}/slots`),

    getById: (stationId, slotId) =>
        apiFetch(`/stations/${stationId}/slots/${slotId}`),

    create: (stationId, data) =>
        apiFetch(`/stations/${stationId}/slots`, {
            method: "POST",
            body: JSON.stringify(data),
        }),

    update: (stationId, slotId, data) =>
        apiFetch(`/stations/${stationId}/slots/${slotId}`, {
            method: "PUT",
            body: JSON.stringify(data),
        }),

    updateStatus: (stationId, slotId, data) =>
        apiFetch(`/stations/${stationId}/slots/${slotId}/status`, {
            method: "PATCH",
            body: JSON.stringify(data),
        }),

    delete: (stationId, slotId) =>
        apiFetch(`/stations/${stationId}/slots/${slotId}`, { method: "DELETE" }),

    getAvailability: (stationId, slotId, date) =>
        apiFetch(`/stations/${stationId}/slots/${slotId}/availability${date ? `?date=${date}` : ""}`),
};

// ============================
// STATION PRICING (giá theo khung giờ — per station)
// ============================

export const stationPricingApi = {
    getAll: (stationId) =>
        apiFetch(`/stations/${stationId}/pricing`),

    create: (stationId, data) =>
        apiFetch(`/stations/${stationId}/pricing`, {
            method: "POST",
            body: JSON.stringify(data),
        }),

    update: (stationId, pricingId, data) =>
        apiFetch(`/stations/${stationId}/pricing/${pricingId}`, {
            method: "PUT",
            body: JSON.stringify(data),
        }),

    delete: (stationId, pricingId) =>
        apiFetch(`/stations/${stationId}/pricing/${pricingId}`, { method: "DELETE" }),
};

// ============================
// PUBLIC STATIONS (Driver browse)
// ============================

export const publicStationApi = {
    /**
     * Tìm trạm có phân trang + GPS
     * Response: { total, page, pageSize, items }
     */
    getAll: ({ keyword, minRating, sortBy, lat, lng, radiusKm, page = 1, pageSize = 20 } = {}) => {
        const params = new URLSearchParams();
        if (keyword)   params.set("keyword", keyword);
        if (minRating) params.set("minRating", String(minRating));
        if (sortBy)    params.set("sortBy", sortBy);
        if (lat != null) params.set("lat", String(lat));
        if (lng != null) params.set("lng", String(lng));
        if (radiusKm)  params.set("radiusKm", String(radiusKm));
        params.set("page", String(page));
        params.set("pageSize", String(pageSize));
        return apiFetch(`/public/stations?${params.toString()}`);
    },

    getById: (id) => apiFetch(`/public/stations/${id}`),

    /**
     * Nearby — gọn cho Map View
     * Response: array (không phân trang)
     */
    getNearby: (lat, lng, radiusKm = 5, top = 20) =>
        apiFetch(`/public/stations/nearby?lat=${lat}&lng=${lng}&radiusKm=${radiusKm}&top=${top}`),
};


// ============================
// BOOKING
// ============================

export const bookingApi = {
    create: (data) =>
        apiFetch("/Booking", {
            method: "POST",
            body: JSON.stringify(data),
        }),

    /**
     * Danh sách booking driver (có phân trang)
     * Response: { total, page, pageSize, items }
     */
    getDriverBookings: (status, page = 1, pageSize = 50) => {
        const params = new URLSearchParams();
        if (status) params.set("status", status);
        params.set("page", String(page));
        params.set("pageSize", String(pageSize));
        return apiFetch(`/Booking/driver?${params.toString()}`);
    },

    /**
     * Lịch sử booking (có phân trang)
     * Response: { total, page, pageSize, items }
     */
    getDriverHistory: (page = 1, pageSize = 50) =>
        apiFetch(`/Booking/driver/history?page=${page}&pageSize=${pageSize}`),

    getOwnerBookings: () => apiFetch("/Booking/owner"),

    getById: (id) => apiFetch(`/Booking/${id}`),

    accept: (id) =>
        apiFetch(`/Booking/${id}/accept`, { method: "PUT" }),

    reject: (id, rejectionReason) =>
        apiFetch(`/Booking/${id}/reject`, {
            method: "PUT",
            body: JSON.stringify({ rejectionReason }),
        }),

    driverCancel: (id, cancelReason) =>
        apiFetch(`/Booking/${id}/driver-cancel`, {
            method: "PUT",
            body: JSON.stringify({ cancelReason }),
        }),

    ownerCancel: (id, cancelReason) =>
        apiFetch(`/Booking/${id}/owner-cancel`, {
            method: "PUT",
            body: JSON.stringify({ cancelReason }),
        }),

    /** Lấy thông tin phí hủy trước khi xác nhận hủy (Driver) */
    cancelPreview: (id) => apiFetch(`/Booking/${id}/cancel-preview`),

    /**
     * Lấy lịch đặt chỗ của một slot theo ngày
     * GET /api/Booking/slot/{slotId}/schedule?date=YYYY-MM-DD
     * Không truyền date → mặc định hôm nay
     */
    getSlotSchedule: (slotId, date) => {
        const qs = date ? `?date=${date}` : "";
        return apiFetch(`/Booking/slot/${slotId}/schedule${qs}`);
    },
};


// ============================
// PAYMENT
// ============================

export const paymentApi = {
    createPaymentUrl: (bookingId) =>
        apiFetch(`/Payment/${bookingId}/create-payment-url`, { method: "POST" }),

    /**
     * Thanh toán SePay VietQR
     * POST /api/Payment/sepay/{bookingId}
     * Response: { qrUrl, bookingId, amount, description }
     */
    createSepayUrl: (bookingId) =>
        apiFetch(`/Payment/sepay/${bookingId}`, { method: "POST" }),

    /** Thanh toán bằng ví */
    payWithWallet: (bookingId) =>
        apiFetch(`/Payment/pay-wallet?bookingId=${bookingId}`, { method: "POST" }),
};


// ============================
// CHARGING SESSION
// ============================

export const chargingApi = {
    checkIn: (qrCodeToken) =>
        apiFetch("/charging/check-in", {
            method: "POST",
            body: JSON.stringify({ qrCodeToken }),
        }),

    stopCharging: (sessionId) =>
        apiFetch(`/charging/${sessionId}/stop`, { method: "PUT" }),

    confirmCompletion: (sessionId) =>
        apiFetch(`/charging/${sessionId}/confirm`, { method: "PUT" }),

    /** Driver yêu cầu kết thúc sạc sớm */
    requestEarlyEnd: (sessionId) =>
        apiFetch(`/charging/${sessionId}/request-early-end`, { method: "PUT" }),

    getActiveSessions: () => apiFetch("/charging/active"),

    getByBookingId: (bookingId) => apiFetch(`/charging/booking/${bookingId}`),

    getInvoice: (bookingId) => apiFetch(`/charging/invoice/${bookingId}`),
};

// ============================
// WALLET
// ============================

export const walletApi = {
    getWallet: () => apiFetch("/Wallet"),

    topUp: (amount) =>
        apiFetch("/Wallet/top-up", {
            method: "POST",
            body: JSON.stringify({ amount }),
        }),

    payBooking: (bookingId) =>
        apiFetch(`/Wallet/pay-booking/${bookingId}`, { method: "POST" }),

    /** Rút tiền (Driver) — cần info ngân hàng */
    withdraw: ({ amount, bankName, bankAccountNumber, bankAccountHolder, userNote }) =>
        apiFetch("/Wallet/withdraw", {
            method: "POST",
            body: JSON.stringify({ amount, bankName, bankAccountNumber, bankAccountHolder, userNote }),
        }),

    getTransactions: () => apiFetch("/Wallet/transactions"),

    getWithdrawRequests: () => apiFetch("/Wallet/withdraw-requests"),
};

// ============================
// NOTIFICATION
// ============================

export const notificationApi = {
    getAll: () => apiFetch("/Notification"),
    markAsRead: (id) => apiFetch(`/Notification/${id}/read`, { method: "PUT" }),
    // markAllAsRead: chưa có endpoint trên BE — cần bổ sung sau
};

// ============================
// DISPUTE
// ============================

export const disputeApi = {
    /** Driver tạo khiếu nại + upload bằng chứng (multipart/form-data) */
    submit: (bookingId, reason, description, files = []) => {
        const formData = new FormData();
        formData.append("BookingId", String(bookingId));
        formData.append("Reason", reason);
        formData.append("Description", description);
        files.forEach((file) => formData.append("Files", file));
        return apiFetchFormData("/dispute", formData, "POST");
    },

    /** Owner phản hồi + nộp bằng chứng (multipart/form-data) */
    submitOwnerEvidence: (disputeId, response, files = []) => {
        const formData = new FormData();
        formData.append("Response", response);
        files.forEach((file) => formData.append("Files", file));
        return apiFetchFormData(`/dispute/${disputeId}/owner-evidence`, formData, "PUT");
    },

    /** Admin phán quyết khiếu nại */
    resolve: (disputeId, data) =>
        apiFetch(`/dispute/${disputeId}/resolve`, {
            method: "POST",
            body: JSON.stringify(data),
        }),

    /** Danh sách dispute chờ xử lý (Admin) */
    getPending: () => apiFetch("/dispute/pending"),

    /** Chi tiết dispute */
    getById: (disputeId) => apiFetch(`/dispute/${disputeId}`),

    /** Dispute theo booking */
    getByBookingId: (bookingId) => apiFetch(`/dispute/booking/${bookingId}`),

    /** Tất cả dispute — Admin, filter theo status nếu cần */
    getAll: (status) => apiFetch(`/dispute/all${status ? `?status=${status}` : ""}`),
};

// ============================
// ADMIN REVENUE (Báo cáo tài chính)
// ============================

export const adminRevenueApi = {
    getSummary: (period = "all") =>
        apiFetch(`/admin/revenue/summary?period=${period}`),

    getMonthly: (period = "all") =>
        apiFetch(`/admin/revenue/monthly?period=${period}`),

    getTopStations: (period = "all", limit = 5) =>
        apiFetch(`/admin/revenue/top-stations?period=${period}&limit=${limit}`),

    getRecentTransactions: (limit = 10) =>
        apiFetch(`/admin/revenue/recent-transactions?limit=${limit}`),

    getVatReport: (period = "all") =>
        apiFetch(`/admin/revenue/vat-report?period=${period}`),
};

// ============================
// REVIEW (Đánh giá trạm sạc)
// ============================

export const reviewApi = {
    /** Driver đánh giá trạm (sau booking Completed) */
    create: (data) =>
        apiFetch("/reviews", {
            method: "POST",
            body: JSON.stringify(data),
        }),

    /** Owner phản hồi đánh giá */
    reply: (reviewId, reply) =>
        apiFetch(`/reviews/${reviewId}/reply`, {
            method: "PUT",
            body: JSON.stringify({ reply }),
        }),

    /** Danh sách đánh giá của trạm (Public) */
    getByStation: (stationId, page = 1, pageSize = 10) =>
        apiFetch(`/reviews/station/${stationId}?page=${page}&pageSize=${pageSize}`),

    /** Tổng quan rating — breakdown theo sao */
    getSummary: (stationId) =>
        apiFetch(`/reviews/station/${stationId}/summary`),

    /** Đánh giá của driver — TODO: BE chưa có endpoint này */
    // getMyReviews: () => apiFetch("/Review/my"),

    /** Top trạm xếp hạng (cho trang chủ) */
    getTopStations: (limit = 10) =>
        apiFetch(`/reviews/top-stations?limit=${limit}`),
};

// ============================
// PROFILE — Avatar Upload
// ============================

export const driverProfileApi = {
    uploadAvatar: (file) => {
        const formData = new FormData();
        formData.append("file", file);
        return apiFetchFormData("/driver/profile/avatar", formData, "POST");
    },
};

export const ownerProfileApi = {
    uploadAvatar: (file) => {
        const formData = new FormData();
        formData.append("file", file);
        return apiFetchFormData("/owner/profile/avatar", formData, "POST");
    },
};

// ============================
// FAVORITE (Trạm yêu thích)
// ============================

export const favoriteApi = {
    /** Thêm trạm yêu thích */
    add: (stationId) =>
        apiFetch(`/favorites/${stationId}`, { method: "POST" }),

    /** Xóa khỏi yêu thích */
    remove: (stationId) =>
        apiFetch(`/favorites/${stationId}`, { method: "DELETE" }),

    /** Danh sách trạm yêu thích của Driver */
    getMyFavorites: () => apiFetch("/favorites"),

    /** Top trạm yêu thích nhất */
    getTop: (limit = 10) => apiFetch(`/favorites/top?limit=${limit}`),

    /** Check đã yêu thích chưa */
    check: (stationId) => apiFetch(`/favorites/${stationId}/check`),
};

// ============================
// EXTRA SERVICE (Owner CRUD)
// ============================

export const extraServiceApi = {
    getAll: (stationId) => apiFetch(`/stations/${stationId}/extra-services`),

    create: (stationId, data) =>
        apiFetch(`/stations/${stationId}/extra-services`, {
            method: "POST",
            body: JSON.stringify(data),
        }),

    update: (stationId, id, data) =>
        apiFetch(`/stations/${stationId}/extra-services/${id}`, {
            method: "PUT",
            body: JSON.stringify(data),
        }),

    delete: (stationId, id) =>
        apiFetch(`/stations/${stationId}/extra-services/${id}`, { method: "DELETE" }),
};

// ============================
// LOYALTY (Driver)
// ============================

export const loyaltyApi = {
    getInfo: () => apiFetch("/loyalty"),
};

// ============================
// ADMIN CONFIG
// ============================

export const adminConfigApi = {
    getAll: () => apiFetch("/admin/config"),

    update: (key, value) =>
        apiFetch(`/admin/config/${key}`, {
            method: "PUT",
            body: JSON.stringify({ value }),
        }),
};

// ============================
// CHAT (Driver ↔ Owner)
// ============================

export const chatApi = {
    /** Danh sách conversations */
    getConversations: () => apiFetch("/chat"),

    /** Lịch sử tin nhắn theo booking */
    getMessages: (bookingId) => apiFetch(`/chat/${bookingId}`),

    /** Gửi tin nhắn */
    sendMessage: (bookingId, content) =>
        apiFetch(`/chat/${bookingId}`, {
            method: "POST",
            body: JSON.stringify({ content }),
        }),
};

// ============================
// BANK ACCOUNTS
// ============================

export const bankAccountApi = {
    getAll: () => apiFetch("/bank-accounts"),

    create: (data) =>
        apiFetch("/bank-accounts", {
            method: "POST",
            body: JSON.stringify(data),
        }),

    setDefault: (id) =>
        apiFetch(`/bank-accounts/${id}/set-default`, { method: "PUT" }),

    delete: (id) =>
        apiFetch(`/bank-accounts/${id}`, { method: "DELETE" }),
};

// ============================
// OWNER PAYOUT (rút tiền Owner)
// ============================

export const payoutApi = {
    create: (data) =>
        apiFetch("/payouts", {
            method: "POST",
            body: JSON.stringify(data),
        }),

    getAll: () => apiFetch("/payouts"),
};

// ============================
// ADMIN — Duyệt payout (Owner)
// ============================

export const adminPayoutApi = {
    getPending: () => apiFetch("/admin/payouts"),

    process: (id, approve, note) =>
        apiFetch(`/admin/payouts/${id}/process`, {
            method: "PUT",
            body: JSON.stringify({ approve, note }),
        }),
};

// ============================
// ADMIN — Duyệt rút tiền (Driver)
// ============================

export const adminWithdrawApi = {
    getPending: () => apiFetch("/admin/withdraws"),

    process: (id, approve, adminNote) =>
        apiFetch(`/admin/withdraws/${id}/process`, {
            method: "PUT",
            body: JSON.stringify({ approve, adminNote }),
        }),
};

// ============================
// ADMIN — Quản lý tài khoản
// ============================

export const adminAccountApi = {
    /** GET /api/AdminAccounts?search=&role=&status=&page=&pageSize= */
    getAll: (search, role, status, page = 1, pageSize = 10) => {
        const params = new URLSearchParams();
        if (search) params.set("search", search);
        if (role && role !== "ALL") params.set("role", role);
        if (status && status !== "ALL") params.set("status", status);
        params.set("page", String(page));
        params.set("pageSize", String(pageSize));
        return apiFetch(`/AdminAccounts?${params.toString()}`);
    },

    /** GET /api/AdminAccounts/statistics */
    getStatistics: () => apiFetch("/AdminAccounts/statistics"),

    /** PATCH /api/AdminAccounts/{id}/toggle-ban — bật/tắt ban */
    toggleBan: (id) =>
        apiFetch(`/AdminAccounts/${id}/toggle-ban`, { method: "PATCH" }),
};
