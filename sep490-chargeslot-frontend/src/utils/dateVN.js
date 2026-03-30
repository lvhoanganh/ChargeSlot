/**
 * Tiện ích format ngày giờ theo múi giờ Việt Nam (UTC+7)
 * Hoạt động đúng dù BE trả về UTC có Z hay không có Z
 */

const VN_TZ = "Asia/Ho_Chi_Minh";

/** Format đầy đủ: HH:mm dd/MM/yyyy */
export function formatVN(dt) {
    if (!dt) return "";
    return new Date(dt).toLocaleString("vi-VN", {
        timeZone: VN_TZ,
        hour: "2-digit",
        minute: "2-digit",
        day: "2-digit",
        month: "2-digit",
        year: "numeric",
        hour12: false,
    });
}

/** Chỉ format ngày: dd/MM/yyyy */
export function formatDateVN(dt) {
    if (!dt) return "";
    return new Date(dt).toLocaleDateString("vi-VN", {
        timeZone: VN_TZ,
        day: "2-digit",
        month: "2-digit",
        year: "numeric",
    });
}

/** Chỉ format giờ: HH:mm */
export function formatTimeVN(dt) {
    if (!dt) return "";
    return new Date(dt).toLocaleTimeString("vi-VN", {
        timeZone: VN_TZ,
        hour: "2-digit",
        minute: "2-digit",
        hour12: false,
    });
}

/** Lấy đối tượng Date đã đổi về VN (dùng cho tính toán) */
export function toVNDate(dt) {
    if (!dt) return null;
    return new Date(new Date(dt).toLocaleString("en-US", { timeZone: VN_TZ }));
}
