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
    Object.keys(localStorage).forEach(k => {
        if (k.startsWith("activeChargingBooking_") || k === "activeChargingBookingId") {
            localStorage.removeItem(k);
        }
    });
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

    // 401 → thử refresh token 1 lần trước khi logout
    if (response.status === 401) {
        const refreshToken = localStorage.getItem("refreshToken");
        const oldToken = localStorage.getItem("accessToken");
        if (refreshToken && oldToken) {
            try {
                const refreshRes = await fetch(`${API_BASE_URL}/auth/refresh-token`, {
                    method: "POST",
                    headers: { "Content-Type": "application/json" },
                    body: JSON.stringify({ accessToken: oldToken, refreshToken }),
                });
                if (refreshRes.ok) {
                    const refreshData = await refreshRes.json();
                    localStorage.setItem("accessToken", refreshData.accessToken);
                    if (refreshData.refreshToken) localStorage.setItem("refreshToken", refreshData.refreshToken);
                    if (refreshData.expiresAtUtc) localStorage.setItem("expiresAtUtc", refreshData.expiresAtUtc);
                    // Retry original request with new token
                    const retryResponse = await fetch(`${API_BASE_URL}${endpoint}`, {
                        ...options,
                        headers: { ...headers, Authorization: `Bearer ${refreshData.accessToken}` },
                    });
                    if (retryResponse.status === 204) return null;
                    if (retryResponse.ok) return retryResponse.json();
                }
            } catch { /* refresh failed */ }
        }
        // Không refresh được → emit event để SessionGuard navigate mượt (không hard reload)
        clearAuth();
        window.dispatchEvent(new CustomEvent("cs:logout", { detail: { reason: "expired" } }));
        throw new Error("Phiên đăng nhập hết hạn");
    }

    // 403 → tài khoản bị vô hiệu hóa → force logout
    if (response.status === 403) {
        const body = await response.json().catch(() => ({}));
        if (body.message && (body.message.includes("vô hiệu hóa") || body.message.includes("khoá"))) {
            clearAuth();
            // Emit event để SessionGuard navigate mượt (không hard reload)
            window.dispatchEvent(new CustomEvent("cs:logout", { detail: { reason: "banned" } }));
            throw new Error("Tài khoản bị khoá do vi phạm tiêu chuẩn hệ thống!");
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
        // Xử lý ASP.NET model validation errors: { errors: { Field: ["msg"] }, title: "..." }
        const msg = error.message
            || error.error
            || (error.errors ? Object.values(error.errors).flat().join('; ') : null)
            || error.title
            || `Lỗi ${response.status}`;
        throw new Error(msg);
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
        // Emit event để SessionGuard navigate mượt (không hard reload)
        window.dispatchEvent(new CustomEvent("cs:logout", { detail: { reason: "expired" } }));
        throw new Error("Phiên đăng nhập hết hạn");
    }

    if (response.status === 403) {
        const body = await response.json().catch(() => ({}));
        if (body.message && (body.message.includes("vô hiệu hóa") || body.message.includes("khoá"))) {
            clearAuth();
            // Emit event để SessionGuard navigate mượt (không hard reload)
            window.dispatchEvent(new CustomEvent("cs:logout", { detail: { reason: "banned" } }));
            throw new Error("Tài khoản bị khoá do vi phạm tiêu chuẩn hệ thống!");
        }
        throw new Error(body.message || "Bạn không có quyền thực hiện thao tác này");
    }

    if (response.status === 204) return null;

    if (!response.ok) {
        const error = await response.json().catch(() => ({}));
        const msg = error.message
            || error.error
            || (error.errors ? Object.values(error.errors).flat().join('; ') : null)
            || error.title
            || `Lỗi ${response.status}`;
        throw new Error(msg);
    }

    return response.json();
}

// ============================
// AUTH
// ============================

export const authApi = {
    getMe: () => apiFetch("/auth/me"),

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

    /**
     * Kiểm tra số điện thoại đã đăng ký chưa (trước khi gửi OTP Firebase)
     * GET /api/Auth/check-phone?phoneNumber=...
     * Response: { exists: boolean }
     * ⚠️ Dùng fetch thuần — endpoint PUBLIC, không cần token.
     *    KHÔNG dùng apiFetch vì sẽ bị redirect /login nếu nhận 401.
     */
    checkPhone: async (phoneNumber) => {
        const res = await fetch(
            `${API_BASE_URL}/Auth/check-phone?phoneNumber=${encodeURIComponent(phoneNumber)}`
        );
        if (!res.ok) {
            const err = await res.json().catch(() => ({}));
            throw new Error(err.message || `Lỗi ${res.status}`);
        }
        return res.json();
    },

    addEmail: (email) =>
        apiFetch("/auth/add-email", {
            method: "POST",
            body: JSON.stringify({ email }),
        }),

    verifyEmail: (userId, token) =>
        apiFetch("/auth/verify-email", {
            method: "POST",
            body: JSON.stringify({ userId, token }),
        }),
};

// ============================
// CHARGING STATION (Owner)
// ============================

export const stationApi = {
    getAll: () => apiFetch("/stations"),

    getById: (id) => apiFetch(`/stations/${id}`),

    create: (formData) =>
        apiFetchFormData("/stations", formData, "POST"),

    /**
     * Cập nhật trạm sạc — BE dùng [FromForm] UpdateStationFormDto
     * Phải gửi multipart/form-data, không được gửi JSON
     */
    update: (id, formData) =>
        apiFetchFormData(`/stations/${id}`, formData, "PUT"),

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
// KYC
// ============================

export const ownerKycApi = {
    getStatus: () => apiFetch("/owner/kyc/status"),
    submit: (formData) => apiFetchFormData("/owner/kyc/submit", formData, "POST"),
};

export const adminKycApi = {
    getPending: () => apiFetch("/admin/kyc/pending"),
    getAll: (status) => {
        const params = new URLSearchParams();
        if (status && status !== "ALL") params.set("status", status);
        return apiFetch(`/admin/kyc/all?${params.toString()}`);
    },
    review: (ownerUserId, isApproved, rejectReason) =>
        apiFetch(`/admin/kyc/${ownerUserId}/review`, {
            method: "PUT",
            body: JSON.stringify({ isApproved, rejectReason }),
        }),
};

// ============================
// ADMIN ACCOUNT DETAILS
// ============================

export const adminAccountsDetailsApi = {
    getOwnerDetails: (userId) => apiFetch(`/AdminAccounts/owner/${userId}`),
    getDriverDetails: (userId) => apiFetch(`/AdminAccounts/driver/${userId}`),
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
     * NOTE: Endpoint này include ExtraServices trong response (GetPublicStationsAsync có .Include(s=>s.ExtraServices))
     */
    getAll: ({ keyword, minRating, sortBy, lat, lng, radiusKm, page = 1, pageSize = 20 } = {}) => {
        const params = new URLSearchParams();
        if (keyword) params.set("keyword", keyword);
        if (minRating) params.set("minRating", String(minRating));
        if (sortBy) params.set("sortBy", sortBy);
        if (lat != null) params.set("lat", String(lat));
        if (lng != null) params.set("lng", String(lng));
        if (radiusKm) params.set("radiusKm", String(radiusKm));
        params.set("page", String(page));
        params.set("pageSize", String(pageSize));
        return apiFetch(`/public/stations?${params.toString()}`);
    },

    /**
     * Chi tiết 1 trạm (chỉ trả về nếu Approved + Active).
     * ⚠️ BE LIMITATION: Endpoint này KHÔNG include ExtraServices trong DB query
     * (ChargingStationRepository.GetByIdAsync thiếu .Include(s=>s.ExtraServices))
     * → Luôn trả về extraServices: [].
     * Workaround: sau khi gọi getById, gọi thêm getAll({keyword: name}) và merge extraServices.
     */
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

    /**
     * Owner xem danh sách booking (kèm filter status)
     * Giảm pageSize 500 → 100 để tránh fetch quá nặng
     */
    getOwnerBookings: (status = null, page = 1, pageSize = 100) => {
        const params = new URLSearchParams();
        if (status) params.set("status", status);
        params.set("page", String(page));
        params.set("pageSize", String(pageSize));
        return apiFetch(`/Booking/owner?${params.toString()}`);
    },

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
    /**
     * Tạo URL thanh toán VNPay (nếu có)
     * POST /api/Payment/{bookingId}/create-payment-url
     */
    createPaymentUrl: (bookingId) =>
        apiFetch(`/Payment/${bookingId}/create-payment-url`, { method: "POST" }),

    /**
     * Tạo QR VietQR (SePay) để thanh toán booking
     * GET /api/Payment/{bookingId}/sepay-qr
     * Response: { qrUrl }
     */
    createSepayUrl: (bookingId) =>
        apiFetch(`/Payment/${bookingId}/sepay-qr`), // GET — không cần method

    // NOTE: Thanh toán bằng ví → dùng walletApi.payBooking(bookingId)
    // POST /api/Wallet/pay-booking/{bookingId} (KHÔNG phải /Payment/pay-wallet)
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

    /** Get active sessions for a specific station (Filter client-side) */
    getByStationId: async (stationId) => {
        try {
            // Backend doesn't have /charging/station/{id} endpoint yet
            // So we fetch all active sessions and filter by stationId
            const activeSessions = await apiFetch("/charging/active");
            const sessionsArray = Array.isArray(activeSessions) ? activeSessions : (activeSessions?.items || []);
            
            console.log("🔌 API: All active sessions:", sessionsArray);
            
            // Filter sessions for this station - try multiple property paths
            const filtered = sessionsArray.filter(s => {
                const chargingSlotStationId = s.chargingSlot?.chargingStationId;
                const slotStationId = s.slot?.chargingStationId || s.slot?.stationId;
                const directStationId = s.stationId;
                
                const matches = chargingSlotStationId === stationId || slotStationId === stationId || directStationId === stationId;
                
                if (matches) {
                    console.log(`✅ Session ${s.id} matches station ${stationId}:`, {
                        chargingSlotStationId,
                        slotStationId,
                        directStationId,
                        slotId: s.slotId || s.slot?.id || s.chargingSlot?.id,
                    });
                }
                
                return matches;
            });
            
            console.log("📍 Filtered sessions for station", stationId, ":", filtered);
            return filtered;
        } catch (err) {
            console.error("❌ Error in getByStationId:", err);
            return [];
        }
    },

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

    /** Rút tiền (Chung cho cả Owner & Driver) */
    withdraw: ({ amount, bankName, bankAccountNumber, bankAccountHolder, userNote }) =>
        apiFetch("/Wallet/withdraw", {
            method: "POST",
            body: JSON.stringify({ amount, bankName, bankAccountNumber, bankAccountHolder, userNote }),
        }),

    /**
     * Lịch sử giao dịch ví (phân trang)
     * Response: { total, page, pageSize, items }
     */
    getTransactions: (page = 1, pageSize = 20) =>
        apiFetch(`/Wallet/transactions?page=${page}&pageSize=${pageSize}`),

    /**
     * Danh sách yêu cầu rút tiền (phân trang)
     * Response: { total, page, pageSize, items }
     */
    getWithdrawRequests: (page = 1, pageSize = 20) =>
        apiFetch(`/Wallet/withdraw-requests?page=${page}&pageSize=${pageSize}`),

    /** Báo đã nhận được tiền */
    confirmWithdrawal: (id) => apiFetch(`/Wallet/withdraw-requests/${id}/confirm`, { method: "PUT" }),

    /** Báo lỗi rút tiền */
    reportWithdrawalIssue: (id, reason) => apiFetch(`/Wallet/withdraw-requests/${id}/report-issue`, {
        method: "PUT",
        body: JSON.stringify({ issueNote: reason }),
    }),
};

// ============================
// NOTIFICATION
// ============================

export const notificationApi = {
    /**
     * GET /api/Notification?page=&pageSize=
     * Response: { total, page, pageSize, items }
     */
    getAll: (page = 1, pageSize = 20) =>
        apiFetch(`/Notification?page=${page}&pageSize=${pageSize}`),
    /** PUT /api/Notification/{id}/read */
    markAsRead: (id) => apiFetch(`/Notification/${id}/read`, { method: "PUT" }),
    // markAllAsRead: BE chưa có endpoint này (chỉ có mark 1 cái)
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

    /**
     * Danh sách dispute chờ xử lý (Admin, phân trang)
     * Response: { total, page, pageSize, items }
     */
    getPending: (page = 1, pageSize = 20) =>
        apiFetch(`/dispute/pending?page=${page}&pageSize=${pageSize}`),

    /** Chi tiết dispute */
    getById: (disputeId) => apiFetch(`/dispute/${disputeId}`),

    /** Dispute theo booking */
    getByBookingId: (bookingId) => apiFetch(`/dispute/booking/${bookingId}`),

    /**
     * Tất cả dispute (Admin, phân trang), filter theo status
     * Response: { total, page, pageSize, items }
     */
    getAll: (status, page = 1, pageSize = 20) => {
        const params = new URLSearchParams();
        if (status && status !== "ALL") params.set("status", status);
        params.set("page", String(page));
        params.set("pageSize", String(pageSize));
        return apiFetch(`/dispute/all?${params.toString()}`);
    },

    /**
     * Danh sách dispute của Driver đang đăng nhập (phân trang)
     * Response: { total, page, pageSize, items }
     */
    getMyDisputes: (page = 1, pageSize = 20) =>
        apiFetch(`/dispute/my?page=${page}&pageSize=${pageSize}`),

    /**
     * Danh sách dispute liên quan đến Owner đang đăng nhập (phân trang)
     * Response: { total, page, pageSize, items }
     */
    getOwnerDisputes: (page = 1, pageSize = 20) =>
        apiFetch(`/dispute/owner?page=${page}&pageSize=${pageSize}`),
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

    getTransactionDetail: (id) =>
        apiFetch(`/admin/finance/transactions/${id}`),

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

export const adminProfileApi = {
    uploadAvatar: (file) => {
        const formData = new FormData();
        formData.append("file", file);
        return apiFetchFormData("/admin/profile/avatar", formData, "POST");
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
    /** GET /api/AdminConfig — trả về object UpdateSystemConfigsDto */
    getAll: () => apiFetch("/AdminConfig"),

    /** PUT /api/AdminConfig — gửi toàn bộ object + secondaryPassword */
    update: (dto) =>
        apiFetch("/AdminConfig", {
            method: "PUT",
            body: JSON.stringify(dto),
        }),

    /** POST /api/AdminConfig/seed — khởi tạo dữ liệu mặc định vào DB */
    seed: () => apiFetch("/AdminConfig/seed", { method: "POST" }),
};


// ============================
// CHAT (Driver ↔ Owner)
// ============================

export const chatApi = {
    /**
     * Danh sách conversations (phân trang)
     * Response: { total, page, pageSize, items }
     */
    getConversations: (page = 1, pageSize = 20) =>
        apiFetch(`/chat?page=${page}&pageSize=${pageSize}`),

    /**
     * Lịch sử tin nhắn theo bookingId (phân trang)
     * Response: { conversationId, total, page, pageSize, messages }
     */
    getMessages: (bookingId, page = 1, pageSize = 50) =>
        apiFetch(`/chat/${bookingId}?page=${page}&pageSize=${pageSize}`),

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
// [DEPRECATED] OWNER PAYOUT — ĐÃ XÓA TỬ BACKEND (Phase 6)
// Dùng walletApi.withdraw() thay thế!
// ============================
// payoutApi ĐÃ Bị XÓA — KHÔNG SỬ DỤNG NỮA
// adminPayoutApi ĐÃ Bị XÓA — Dùng adminWithdrawApi

// ============================
// ADMIN — Quản lý Tài khoản (Secondary Password & User Admin)
// ============================

export const adminAccountApi = {
    setupSecondaryPassword: (currentLoginPassword, newSecondaryPassword) =>
        apiFetch("/AdminAccounts/secondary-password/setup", {
            method: "POST",
            body: JSON.stringify({
                PrimaryPassword: currentLoginPassword,
                NewSecondaryPassword: newSecondaryPassword
            })
        }),

    resetSecondaryPasswordRequest: () =>
        apiFetch("/AdminAccounts/secondary-password/reset-request", { method: "POST" }),

    resetSecondaryPasswordConfirm: (otp, newSecondaryPassword) =>
        apiFetch("/AdminAccounts/secondary-password/reset-confirm", {
            method: "POST",
            body: JSON.stringify({
                OtpCode: otp,
                NewSecondaryPassword: newSecondaryPassword
            })
        }),

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

// ============================
// ADMIN — Duyệt rút tiền đa bước
// ============================

export const adminWithdrawApi = {
    /**
     * Lấy danh sách chờ duyệt (Pending) — BE trả array thẳng (không phân trang)
     * Response: Array (không bọc PagedResult)
     */
    getPending: () => apiFetch("/admin/withdraws/pending"),

    /**
     * Lấy tất cả yêu cầu rút tiền mọi trạng thái — BE trả array thẳng
     * Response: Array (không bọc PagedResult)
     */
    getAll: () => apiFetch("/admin/withdraws"),

    /**
     * Duyệt hoặc từ chối yêu cầu rút tiền.
     * BE DTO: { Approve: bool, AdminNote: string }
     */
    process: (id, isApproved, adminNote, secondaryPassword) =>
        apiFetch(`/admin/withdraws/${id}/process`, {
            method: "PUT",
            headers: { "SecondaryPassword": secondaryPassword || "" },
            body: JSON.stringify({ approve: isApproved, adminNote: adminNote || null }),
        }),

    confirmTransfer: (id, receiptImageFile) => {
        const formData = new FormData();
        formData.append("ReceiptImage", receiptImageFile);
        return apiFetchFormData(`/admin/withdraws/${id}/confirm-transfer`, formData, "PUT");
    },

    getIssueReported: () => apiFetch("/admin/withdraws/issue-reported"),

    /**
     * Giải quyết sự cố rút tiền.
     * BE DTO: { Refund: bool, AdminNote: string }
     * @param {boolean} refund true=hoàn tiền vào ví, false=chuyển khoản lại
     * @param {string} adminNote Ghi chú của admin
     */
    resolveIssue: (id, refund, adminNote) =>
        apiFetch(`/admin/withdraws/${id}/resolve-issue`, {
            method: "PUT",
            body: JSON.stringify({ refund: !!refund, adminNote: adminNote || null }),
        }),
};

// ============================
// ADMIN — Operations DataGrid
// GET /api/admin/operations/{bookings|sessions|invoices}
// Response: { total, page, pageSize, items }
// ============================

export const adminOperationsApi = {
    /**
     * Tất cả Bookings (Lọc động + Phân trang)
     * @param {{ status, driverUserId, ownerUserId, stationId, fromDate, toDate, page, pageSize }} filter
     */
    getBookings: (filter = {}) => {
        const params = new URLSearchParams();
        if (filter.status) params.set("status", filter.status);
        if (filter.driverUserId) params.set("driverUserId", String(filter.driverUserId));
        if (filter.ownerUserId) params.set("ownerUserId", String(filter.ownerUserId));
        if (filter.stationId) params.set("stationId", String(filter.stationId));
        if (filter.fromDate) params.set("fromDate", filter.fromDate);
        if (filter.toDate) params.set("toDate", filter.toDate);
        params.set("page", String(filter.page || 1));
        params.set("pageSize", String(filter.pageSize || 20));
        return apiFetch(`/admin/operations/bookings?${params.toString()}`);
    },

    /**
     * Tất cả Phiên sạc (Lọc động + Phân trang)
     * @param {{ status, bookingId, page, pageSize }} filter
     */
    getSessions: (filter = {}) => {
        const params = new URLSearchParams();
        if (filter.status) params.set("status", filter.status);
        if (filter.bookingId) params.set("bookingId", String(filter.bookingId));
        params.set("page", String(filter.page || 1));
        params.set("pageSize", String(filter.pageSize || 20));
        return apiFetch(`/admin/operations/sessions?${params.toString()}`);
    },

    /**
     * Tất cả Hóa đơn (Lọc động + Phân trang)
     * @param {{ status, isPaid, page, pageSize }} filter
     */
    getInvoices: (filter = {}) => {
        const params = new URLSearchParams();
        if (filter.status) params.set("status", filter.status);
        if (filter.isPaid != null) params.set("isPaid", String(filter.isPaid));
        params.set("page", String(filter.page || 1));
        params.set("pageSize", String(filter.pageSize || 20));
        return apiFetch(`/admin/operations/invoices?${params.toString()}`);
    },
};

// ============================
// ADMIN — Finance DataGrid
// GET /api/admin/finance/{wallets|wallets/{id}/transactions}
// Response: { total, page, pageSize, items }
// ============================

export const adminFinanceApi = {
    /**
     * Soi tình trạng tất cả ví (Lọc động + Phân trang)
     * GET /api/admin/finance/wallets
     * @param {{ walletType, userId, systemCode, page, pageSize, fromDate, toDate }} filter
     */
    getWallets: (filter = {}) => {
        const params = new URLSearchParams();
        if (filter.walletType) params.set("walletType", filter.walletType);
        if (filter.userId) params.set("userId", String(filter.userId));
        if (filter.systemCode) params.set("systemCode", filter.systemCode);
        if (filter.fromDate) params.set("fromDate", filter.fromDate);
        if (filter.toDate) params.set("toDate", filter.toDate);
        params.set("page", String(filter.page || 1));
        params.set("pageSize", String(filter.pageSize || 20));
        return apiFetch(`/admin/finance/wallets?${params.toString()}`);
    },

    /**
     * Sổ cái chi tiết của một ví (Phân trang)
     * GET /api/admin/finance/wallets/{walletId}/transactions
     * @param {number} walletId
     * @param {{ transactionType, page, pageSize, fromDate, toDate }} filter
     */
    getWalletTransactions: (walletId, filter = {}) => {
        const params = new URLSearchParams();
        if (filter.transactionType) params.set("transactionType", filter.transactionType);
        if (filter.fromDate) params.set("fromDate", filter.fromDate);
        if (filter.toDate) params.set("toDate", filter.toDate);
        params.set("page", String(filter.page || 1));
        params.set("pageSize", String(filter.pageSize || 20));
        return apiFetch(`/admin/finance/wallets/${walletId}/transactions?${params.toString()}`);
    },

    /**
     * Chi tiết một giao dịch (sổ cái — double-entry accounting)
     * GET /api/admin/finance/transactions/{transactionId}
     * @param {number} transactionId
     */
    getTransactionDetail: (transactionId) =>
        apiFetch(`/admin/finance/transactions/${transactionId}`),
};

// Removed duplicate adminAccountApi

// ============================
// AI COPILOT CHATBOT (RBAC) — DEPRECATED
// ============================
// NOTE: BE đã xóa CopilotController vào lúc này
// Endpoint này không còn khả dụng nên được deprecated
// 
// export const aiCopilotApi = {
//     /**
//      * Gửi tin nhắn tới AI Copilot theo role
//      * Endpoint: POST /api/Copilot/{role}/chat
//      * @param {"driver"|"owner"|"admin"} role
//      * @param {Array<{role:string, content:string}>} history  — tối đa 6 tin nhắn gần nhất
//      * @param {string} currentMessage                         — tối đa 500 ký tự
//      */
//     chat: (role, history, currentMessage, options = {}) =>
//         apiFetch(`/chat/${role}`, {
//             method: "POST",
//             body: JSON.stringify({ history, currentMessage }),
//             ...options
//         }),
// };
