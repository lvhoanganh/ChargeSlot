import { useState, useEffect } from "react";
import { useNavigate } from "react-router-dom";
import { bookingApi } from "@/services/api";

const statusStyles = {
  WaitingOwner: { label: "Chờ duyệt", color: "#f59e0b", bg: "#fffbeb", icon: "⏳" },
  PendingPayment: { label: "Chờ thanh toán", color: "#3b82f6", bg: "#eff6ff", icon: "💳" },
  Paid: { label: "Đã thanh toán", color: "#22c55e", bg: "#f0fdf4", icon: "✅" },
  Expired: { label: "Hết hạn", color: "#9ca3af", bg: "#f3f4f6", icon: "⏰" },
  Rejected: { label: "Đã từ chối", color: "#ef4444", bg: "#fef2f2", icon: "❌" },
  Cancelled: { label: "Đã hủy", color: "#6b7280", bg: "#f3f4f6", icon: "🚫" },
  CheckedIn: { label: "Đã check-in", color: "#06b6d4", bg: "#ecfeff", icon: "⚡" },
  InProgress: { label: "Đang sạc", color: "#06b6d4", bg: "#ecfeff", icon: "🔋" },
  Completed: { label: "Hoàn thành", color: "#8b5cf6", bg: "#f5f3ff", icon: "🎉" },
  NoShow: { label: "Không đến", color: "#9ca3af", bg: "#f3f4f6", icon: "🚷" },
  Disputed: { label: "Tranh chấp", color: "#dc2626", bg: "#fef2f2", icon: "⚠️" },
};

const toLocal = (dt) => {
  if (!dt) return "";
  const s = String(dt);
  return new Date(String(s).replace("Z", "")).toLocaleString("vi-VN");
};

export default function BookingRequests() {
  const navigate = useNavigate();
  const [bookings, setBookings] = useState([]);
  const [loading, setLoading] = useState(true);
  const [filter, setFilter] = useState("all");

  useEffect(() => {
    // BE trả { total, page, pageSize, items } — phải unpack .items
    bookingApi.getOwnerBookings()
      .then((data) => {
        const list = data?.items ?? (Array.isArray(data) ? data : []);
        setBookings(list);
      })
      .catch(() => setBookings([]))
      .finally(() => setLoading(false));
  }, []);

  const filtered = filter === "all" ? bookings : bookings.filter((b) => b.status === filter);
  const pendingCount = bookings.filter((b) => b.status === "WaitingOwner").length;

  if (loading) {
    return (
      <div style={{ minHeight: "100vh", background: "#f8fafc", paddingTop: 100, textAlign: "center" }}>
        <div style={{ fontSize: 40 }}>⚡</div>
        <p style={{ color: "#6b7280" }}>Đang tải yêu cầu booking...</p>
      </div>
    );
  }

  return (
    <div style={{ minHeight: "100vh", background: "#f8fafc", paddingTop: 90 }}>
      <div style={{ maxWidth: 800, margin: "0 auto", padding: "0 16px 40px" }}>
        <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 20 }}>
          <h1 style={{ fontSize: 24, fontWeight: 800, color: "#1e293b" }}>
            Yêu cầu Booking
            {pendingCount > 0 && (
              <span style={{ fontSize: 13, fontWeight: 600, color: "#f97316", background: "#fff7ed", padding: "4px 10px", borderRadius: 20, marginLeft: 8 }}>
                {pendingCount} chờ duyệt
              </span>
            )}
          </h1>
        </div>

        {/* Filter tabs */}
        <div style={{ display: "flex", gap: 6, marginBottom: 16, flexWrap: "wrap" }}>
          {[
            { key: "all", label: "Tất cả" },
            { key: "WaitingOwner", label: "Chờ duyệt" },
            { key: "PendingPayment", label: "Chờ thanh toán" },
            { key: "Paid", label: "Đã thanh toán" },
            { key: "Completed", label: "Hoàn thành" },
            { key: "Rejected", label: "Từ chối" },
          ].map((f) => (
            <button
              key={f.key}
              onClick={() => setFilter(f.key)}
              style={{
                padding: "6px 14px", borderRadius: 20, fontSize: 13, fontWeight: 600, cursor: "pointer",
                border: filter === f.key ? "none" : "1.5px solid #e5e7eb",
                background: filter === f.key ? "#f97316" : "#fff",
                color: filter === f.key ? "#fff" : "#374151",
              }}
            >{f.label}</button>
          ))}
        </div>

        {filtered.length === 0 ? (
          <div style={{ textAlign: "center", padding: 40, background: "#fff", borderRadius: 16 }}>
            <div style={{ fontSize: 48, marginBottom: 8 }}>📋</div>
            <p style={{ color: "#6b7280" }}>Không có booking nào</p>
          </div>
        ) : (
          filtered.map((b) => {
            const st = statusStyles[b.status] || statusStyles.WaitingOwner;
            return (
              <div
                key={b.id}
                onClick={() => navigate(`/owner/booking/${b.id}`)}
                style={{
                  background: "#fff", borderRadius: 16, padding: 20, marginBottom: 12,
                  boxShadow: "0 2px 8px rgba(0,0,0,0.04)", cursor: "pointer",
                  border: b.status === "WaitingOwner" ? "2px solid #fbbf24" : "2px solid transparent",
                  transition: "all .15s",
                }}
                onMouseEnter={(e) => e.currentTarget.style.boxShadow = "0 4px 16px rgba(0,0,0,0.08)"}
                onMouseLeave={(e) => e.currentTarget.style.boxShadow = "0 2px 8px rgba(0,0,0,0.04)"}
              >
                <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 8 }}>
                  <span style={{ fontWeight: 700, fontSize: 15, color: "#1e293b" }}>Booking #{b.id}</span>
                  <span style={{ fontSize: 12, fontWeight: 600, color: st.color, background: st.bg, padding: "4px 10px", borderRadius: 20 }}>
                    {st.icon} {st.label}
                  </span>
                </div>
                <div style={{ fontSize: 13, color: "#64748b", marginBottom: 4 }}>
                  Driver: {b.driverName} | Slot: {b.slotName}
                </div>
                <div style={{ display: "flex", gap: 16, fontSize: 13, color: "#64748b" }}>
                  <span>🕐 {toLocal(b.startTime)} — {Math.round(b.durationHours * 60)} phút</span>
                  <span style={{ fontWeight: 700, color: "#f97316" }}>{(b.totalAmount || 0).toLocaleString("vi-VN")}đ</span>
                </div>

                {/* Extra Services summary */}
                {b.extraServices && b.extraServices.length > 0 && (
                  <div style={{ marginTop: 6, paddingTop: 6, borderTop: "1px dashed #e5e7eb" }}>
                    <div style={{ fontSize: 11, color: "#7c3aed", fontWeight: 600, marginBottom: 2 }}>🛒 Dịch vụ bổ sung:</div>
                    {b.extraServices.map((es, idx) => (
                      <div key={idx} style={{ fontSize: 11, color: "#64748b", display: "flex", justifyContent: "space-between" }}>
                        <span>{es.serviceName} ×{es.quantity}</span>
                        <span style={{ fontWeight: 600, color: "#7c3aed" }}>{es.totalPrice?.toLocaleString("vi-VN")}đ</span>
                      </div>
                    ))}
                  </div>
                )}
              </div>
            );
          })
        )}
      </div>
    </div>
  );
}
