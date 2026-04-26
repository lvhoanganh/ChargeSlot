/**
 * BanStatusBadge — Badge hiển thị trạng thái ban/vi phạm của User hoặc Trạm sạc.
 * Props:
 *   banCount    {number}       — số lần bị phạt tích lũy
 *   bannedUntil {string|null}  — ISO date string nếu đang bị khóa, null nếu không
 */
export function BanStatusBadge({ banCount = 0, bannedUntil = null }) {
  if (bannedUntil) {
    const untilDate = new Date(bannedUntil).toLocaleDateString("vi-VN");
    return (
      <span style={{
        display: "inline-flex",
        alignItems: "center",
        gap: 4,
        background: "#fef2f2",
        color: "#dc2626",
        border: "1px solid #fecaca",
        fontSize: 11,
        fontWeight: 700,
        padding: "2px 8px",
        borderRadius: 50,
        whiteSpace: "nowrap",
        lineHeight: 1.5,
      }}>
         Khóa đến {untilDate}
      </span>
    );
  }

  if (banCount > 0) {
    return (
      <span style={{
        display: "inline-flex",
        alignItems: "center",
        gap: 4,
        background: "#fffbeb",
        color: "#d97706",
        border: "1px solid #fde68a",
        fontSize: 11,
        fontWeight: 700,
        padding: "2px 8px",
        borderRadius: 50,
        whiteSpace: "nowrap",
        lineHeight: 1.5,
      }}>
        ️ Vi phạm: {banCount}/3
      </span>
    );
  }

  return (
    <span style={{
      display: "inline-flex",
      alignItems: "center",
      gap: 4,
      background: "#f0fdf4",
      color: "#16a34a",
      border: "1px solid #bbf7d0",
      fontSize: 11,
      fontWeight: 700,
      padding: "2px 8px",
      borderRadius: 50,
      whiteSpace: "nowrap",
      lineHeight: 1.5,
    }}>
       Hoạt động
    </span>
  );
}
