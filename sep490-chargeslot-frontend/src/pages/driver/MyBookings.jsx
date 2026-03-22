import { useState, useEffect } from "react";
import { useNavigate } from "react-router-dom";
import { bookingApi } from "@/services/api";

const statusStyles = {
  WaitingOwner: { label: "Chờ chủ trạm duyệt", color: "#f59e0b", bg: "#fffbeb", icon: "⏳" },
  PendingPayment: { label: "Chờ thanh toán", color: "#3b82f6", bg: "#eff6ff", icon: "💳" },
  Paid: { label: "Đã thanh toán", color: "#22c55e", bg: "#f0fdf4", icon: "✅" },
  Expired: { label: "Hết hạn", color: "#9ca3af", bg: "#f3f4f6", icon: "⏰" },
  Rejected: { label: "Bị từ chối", color: "#ef4444", bg: "#fef2f2", icon: "❌" },
  Cancelled: { label: "Đã hủy", color: "#6b7280", bg: "#f3f4f6", icon: "🚫" },
  CheckedIn: { label: "Đã check-in", color: "#06b6d4", bg: "#ecfeff", icon: "⚡" },
  InProgress: { label: "Đang sạc", color: "#06b6d4", bg: "#ecfeff", icon: "🔋" },
  Completed: { label: "Hoàn thành", color: "#8b5cf6", bg: "#f5f3ff", icon: "🎉" },
  NoShow: { label: "Không đến", color: "#9ca3af", bg: "#f3f4f6", icon: "🚷" },
  Disputed: { label: "Tranh chấp", color: "#dc2626", bg: "#fef2f2", icon: "⚠️" },
};

// Parse API DateTime (không có Z) → UTC → local
const toLocal = (dt) => {
  if (!dt) return "";
  const s = String(dt);
  return new Date(s.endsWith("Z") ? s : s + "Z").toLocaleString("vi-VN");
};

export default function MyBookings() {
  const navigate = useNavigate();
  const [bookings, setBookings] = useState([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    bookingApi.getDriverBookings()
      .then((data) => setBookings(Array.isArray(data) ? data : []))
      .catch(() => setBookings([]))
      .finally(() => setLoading(false));
  }, []);

  if (loading) {
    return (
      <div style={{ minHeight: "100vh", background: "#f8fafc", paddingTop: 100, textAlign: "center" }}>
        <div style={{ fontSize: 40 }}>⚡</div>
        <p style={{ color: "#6b7280" }}>Đang tải danh sách booking...</p>
      </div>
    );
  }

  return (
    <div style={{ minHeight: "100vh", background: "#f8fafc", paddingTop: 90 }}>
      <div style={{ maxWidth: 700, margin: "0 auto", padding: "0 16px 40px" }}>
        <h1 style={{ fontSize: 24, fontWeight: 800, color: "#1e293b", marginBottom: 20 }}>Booking của tôi</h1>

        {bookings.length === 0 ? (
          <div style={{ textAlign: "center", padding: 40, background: "#fff", borderRadius: 16, boxShadow: "0 2px 8px rgba(0,0,0,0.04)" }}>
            <div style={{ fontSize: 48, marginBottom: 8 }}>📋</div>
            <p style={{ color: "#6b7280", marginBottom: 16 }}>Bạn chưa có booking nào</p>
            <button onClick={() => navigate("/driver/map")} style={{ padding: "10px 20px", borderRadius: 10, border: "none", background: "#f97316", color: "#fff", fontWeight: 600, cursor: "pointer" }}>
              Tìm trạm sạc
            </button>
          </div>
        ) : (
          bookings.map((b) => {
            const st = statusStyles[b.status] || statusStyles.WaitingOwner;
            return (
              <div
                key={b.id}
                onClick={() => navigate(`/driver/booking/${b.id}`)}
                style={{
                  background: "#fff", borderRadius: 16, padding: 20, marginBottom: 12,
                  boxShadow: "0 2px 8px rgba(0,0,0,0.04)", cursor: "pointer",
                  border: "2px solid transparent", transition: "all .15s",
                }}
                onMouseEnter={(e) => e.currentTarget.style.borderColor = "#f97316"}
                onMouseLeave={(e) => e.currentTarget.style.borderColor = "transparent"}
              >
                <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 8 }}>
                  <span style={{ fontWeight: 700, fontSize: 16, color: "#1e293b" }}>{b.stationName}</span>
                  <span style={{ fontSize: 12, fontWeight: 600, color: st.color, background: st.bg, padding: "4px 10px", borderRadius: 20 }}>
                    {st.icon} {st.label}
                  </span>
                </div>
                <div style={{ fontSize: 13, color: "#64748b", marginBottom: 4 }}>Slot: {b.slotName}</div>
                <div style={{ display: "flex", gap: 16, fontSize: 13, color: "#64748b" }}>
                  <span>🕐 {toLocal(b.startTime)} — {b.durationHours}h</span>
                  <span style={{ fontWeight: 700, color: "#f97316" }}>{(b.totalAmount || 0).toLocaleString("vi-VN")}đ</span>
                </div>
              </div>
            );
          })
        )}
      </div>
    </div>
  );
}
