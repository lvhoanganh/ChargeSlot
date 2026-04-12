import { useState, useEffect } from "react";
import { useNavigate } from "react-router-dom";
import { bookingApi } from "@/services/api";
import Pagination from "@/components/Pagination";

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
  const [allBookings, setAllBookings] = useState([]);
  const [loading, setLoading] = useState(true);
  
  // Main group tab
  const [tabType, setTabType] = useState("ongoing"); // "ongoing" | "history"
  // Specific status filter (for history)
  const [statusFilter, setStatusFilter] = useState("all");
  const [dateFrom, setDateFrom] = useState("");
  const [dateTo, setDateTo] = useState("");
  const [page, setPage] = useState(1);

  const fetchBookings = () => {
    setLoading(true);
    bookingApi.getOwnerBookings(null, 1, 500)
      .then((data) => {
        const list = Array.isArray(data) ? data : (data?.items ?? []);
        setAllBookings(list);
      })
      .catch(() => setAllBookings([]))
      .finally(() => setLoading(false));
  };

  useEffect(() => {
    fetchBookings();
  }, []); // Only fetch once initially or when forced

  const ongoingStatuses = ["WaitingOwner", "PendingPayment", "Paid", "CheckedIn", "InProgress"];

  const filteredBookings = allBookings.filter(b => {
    if (tabType === "ongoing") {
      if (!ongoingStatuses.includes(b.status)) return false;
    } else {
      if (ongoingStatuses.includes(b.status)) return false;
      if (statusFilter !== "all" && b.status !== statusFilter) return false;
    }
    const d = (b.startTime || b.bookingDate || "").slice(0, 10);
    if (dateFrom && d < dateFrom) return false;
    if (dateTo && d > dateTo) return false;
    return true;
  });

  const totalCount = filteredBookings.length;
  const paginatedBookings = filteredBookings.slice((page - 1) * 20, page * 20);

  const pendingCount = allBookings.filter((b) => b.status === "WaitingOwner").length;

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

        {/* Tabs for Ongoing / History */}
        <div style={{ display: "flex", gap: 6, marginBottom: 16 }}>
          <button
            onClick={() => { setTabType("ongoing"); setStatusFilter("all"); setPage(1); }}
            style={{
              padding: "8px 16px", borderRadius: 20, fontSize: 14, fontWeight: 700, cursor: "pointer",
              border: tabType === "ongoing" ? "none" : "1.5px solid #e5e7eb",
              background: tabType === "ongoing" ? "#f97316" : "#fff",
              color: tabType === "ongoing" ? "#fff" : "#374151",
            }}
          >
            🔥 Đang xử lý
          </button>
          <button
            onClick={() => { setTabType("history"); setStatusFilter("all"); setPage(1); }}
            style={{
              padding: "8px 16px", borderRadius: 20, fontSize: 14, fontWeight: 700, cursor: "pointer",
              border: tabType === "history" ? "none" : "1.5px solid #e5e7eb",
              background: tabType === "history" ? "#f97316" : "#fff",
              color: tabType === "history" ? "#fff" : "#374151",
            }}
          >
            📚 Lịch sử
          </button>
        </div>

        {tabType === "history" && (
          <div style={{ display: "flex", gap: 6, marginBottom: 16, flexWrap: "wrap" }}>
            {[
              { key: "all", label: "Tất cả lịch sử" },
              { key: "Completed", label: "Hoàn thành" },
              { key: "Rejected", label: "Từ chối" },
              { key: "Cancelled", label: "Đã hủy" },
            ].map((f) => (
              <button
                key={f.key}
                onClick={() => { setStatusFilter(f.key); setPage(1); }}
                style={{
                  padding: "6px 14px", borderRadius: 20, fontSize: 13, fontWeight: 600, cursor: "pointer",
                  border: statusFilter === f.key ? "none" : "1px solid #e5e7eb",
                  background: statusFilter === f.key ? "#475569" : "#fff",
                  color: statusFilter === f.key ? "#fff" : "#374151",
                }}
              >{f.label}</button>
            ))}
          </div>
        )}

        {/* Date range filter */}
        <div style={{ display: "flex", alignItems: "center", gap: 8, marginBottom: 16, flexWrap: "wrap" }}>
          <svg width="15" height="15" fill="none" stroke="#64748b" strokeWidth={2} viewBox="0 0 24 24" style={{ flexShrink: 0 }}>
            <rect x="3" y="4" width="18" height="18" rx="2" /><line x1="16" y1="2" x2="16" y2="6"/><line x1="8" y1="2" x2="8" y2="6"/><line x1="3" y1="10" x2="21" y2="10"/>
          </svg>
          <span style={{ fontSize: 13, color: "#64748b", fontWeight: 600 }}>Ngày sạc:</span>
          <input type="date" value={dateFrom} onChange={(e) => { setDateFrom(e.target.value); setPage(1); }}
            style={{ height: 36, padding: "0 10px", borderRadius: 10, border: "1.5px solid #e2e8f0", background: "#fff", fontSize: 13, outline: "none", cursor: "pointer", color: "#475569" }}
            title="Từ ngày" />
          <span style={{ color: "#94a3b8", fontSize: 12 }}>—</span>
          <input type="date" value={dateTo} onChange={(e) => { setDateTo(e.target.value); setPage(1); }}
            style={{ height: 36, padding: "0 10px", borderRadius: 10, border: "1.5px solid #e2e8f0", background: "#fff", fontSize: 13, outline: "none", cursor: "pointer", color: "#475569" }}
            title="Đến ngày" />
          {(dateFrom || dateTo) && (
            <button onClick={() => { setDateFrom(""); setDateTo(""); setPage(1); }}
              style={{ height: 34, padding: "0 12px", borderRadius: 10, border: "1px solid #fca5a5", background: "#fff", color: "#dc2626", fontSize: 12, fontWeight: 600, cursor: "pointer" }}>
              × Xóa
            </button>
          )}
        </div>

        {/* Local filter only for ongoing to switch between WaitingOwner, PendingPayment... if needed.
            But the user said "Frontend tuyệt đối không gọi full list rồi tự cắt bằng JS nữa".
            If backend ongoing doesn't support status filter, we just show them all in ongoing.
        */}

        {paginatedBookings.length === 0 ? (
          <div style={{ textAlign: "center", padding: 40, background: "#fff", borderRadius: 16 }}>
            <div style={{ fontSize: 48, marginBottom: 8 }}>📋</div>
            <p style={{ color: "#6b7280" }}>Không có booking nào</p>
          </div>
        ) : (
          <div style={{ display: "flex", flexDirection: "column", gap: 12 }}>
            {paginatedBookings.map((b) => {
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
          })}
            <Pagination 
              page={page} 
              totalCount={totalCount} 
              pageSize={20} 
              onPageChange={(p) => setPage(p)} 
            />
          </div>
        )}
      </div>
    </div>
  );
}
