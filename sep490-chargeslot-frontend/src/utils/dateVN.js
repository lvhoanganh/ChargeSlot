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
    try {
        // Backend trả VN time (UTC+7) stringify ko Z: "2026-04-10T12:04:00"
        // 
        // Flow:
        // 1. Parse as UTC bằng +Z: "2026-04-10T12:04:00Z" (interpreted as UTC 12:04)
        // 2. Convert to actual UTC: subtract 7 hours (vì VN 12:04 = UTC 05:04)
        // 3. toLocaleTimeString với timeZone=Asia/Ho_Chi_Minh: convert UTC 05:04 → VN 12:04
        
        const dateStr = String(dt);
        const dateAsUTC = new Date(dateStr + 'Z'); // Parse as UTC
        
        if (isNaN(dateAsUTC.getTime())) {
            throw new Error("Invalid Date");
        }
        
        // Adjust -7 hours: VN time = UTC time + 7, so UTC time = VN time - 7
        const actualUtcTime = new Date(dateAsUTC.getTime() - 7 * 60 * 60 * 1000);
        
        const formatted = actualUtcTime.toLocaleTimeString('vi-VN', {
            timeZone: VN_TZ,
            hour: '2-digit',
            minute: '2-digit',
            hour12: false,
        });
        
        return formatted;
    } catch (e) {
        return "";
    }
}

/** Lấy đối tượng Date đã đổi về VN (dùng cho tính toán) */
export function toVNDate(dt) {
    if (!dt) return null;
    return new Date(new Date(dt).toLocaleString("en-US", { timeZone: VN_TZ }));
}
