const API_BASE_URL = "http://localhost:5162/api";

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
}

/**
 * Xóa auth khi logout
 */
export function clearAuth() {
    localStorage.removeItem("accessToken");
    localStorage.removeItem("userId");
    localStorage.removeItem("role");
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
    getAll: () => apiFetch("/public/stations"),
    getById: (id) => apiFetch(`/public/stations/${id}`),
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

    getDriverBookings: () => apiFetch("/Booking/driver"),

    getOwnerBookings: () => apiFetch("/Booking/owner"),

    getById: (id) => apiFetch(`/Booking/${id}`),

    accept: (id) =>
        apiFetch(`/Booking/${id}/accept`, { method: "PUT" }),

    reject: (id, rejectionReason) =>
        apiFetch(`/Booking/${id}/reject`, {
            method: "PUT",
            body: JSON.stringify({ rejectionReason }),
        }),
};

// ============================
// PAYMENT
// ============================

export const paymentApi = {
    createPaymentUrl: (bookingId) =>
        apiFetch(`/Payment/${bookingId}/create-payment-url`, { method: "POST" }),
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

    withdraw: (amount) =>
        apiFetch("/Wallet/withdraw", {
            method: "POST",
            body: JSON.stringify({ amount }),
        }),

    getTransactions: () => apiFetch("/Wallet/transactions"),
};

// ============================
// NOTIFICATION
// ============================

export const notificationApi = {
    getAll: () => apiFetch("/Notification"),
    markAsRead: (id) => apiFetch(`/Notification/${id}/read`, { method: "PUT" }),
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
