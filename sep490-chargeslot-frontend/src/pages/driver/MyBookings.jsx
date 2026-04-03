import { useState, useEffect } from "react";
import { useNavigate } from "react-router-dom";
import { bookingApi } from "@/services/api";
import { showToast } from "@/components/Toast";

const statusStyles = {
  WaitingOwner: { label: "Chờ duyệt", color: "#f59e0b", bg: "#fffbeb", icon: "⏳", group: "active" },
  PendingPayment: { label: "Chờ thanh toán", color: "#3b82f6", bg: "#eff6ff", icon: "💳", group: "active" },
  Paid: { label: "Đã thanh toán", color: "#22c55e", bg: "#f0fdf4", icon: "✅", group: "active" },
  CheckedIn: { label: "Đã check-in", color: "#06b6d4", bg: "#ecfeff", icon: "⚡", group: "active" },
  InProgress: { label: "Đang sạc", color: "#06b6d4", bg: "#ecfeff", icon: "🔋", group: "active" },
  CompletedPendingInvoice: { label: "Chờ xác nhận", color: "#f97316", bg: "#fff7ed", icon: "🧾", group: "active" },
  Completed: { label: "Hoàn thành", color: "#8b5cf6", bg: "#f5f3ff", icon: "🎉", group: "done" },
  Expired: { label: "Hết hạn", color: "#9ca3af", bg: "#f3f4f6", icon: "⏰", group: "done" },
  Rejected: { label: "Từ chối", color: "#ef4444", bg: "#fef2f2", icon: "❌", group: "done" },
  Cancelled: { label: "Đã hủy", color: "#6b7280", bg: "#f3f4f6", icon: "🚫", group: "done" },
  NoShow: { label: "Không đến", color: "#9ca3af", bg: "#f3f4f6", icon: "🚷", group: "done" },
  Disputed: { label: "Tranh chấp", color: "#dc2626", bg: "#fef2f2", icon: "⚠️", group: "done" },
};

const TABS = [
  { key: "all", label: "Tất cả" },
  { key: "active", label: "Đang xử lý" },
  { key: "done", label: "Đã kết thúc" },
];

const toLocal = (dt) => {
  if (!dt) return "";
  const s = String(dt);
  const d = new Date(String(s).replace("Z", ""));
  return d.toLocaleString("vi-VN", {
    hour: "2-digit", minute: "2-digit",
    day: "2-digit", month: "2-digit", year: "numeric",
    hour12: false,
  });
};

export default function MyBookings() {
  const navigate = useNavigate();
  const [bookings, setBookings] = useState([]);
  const [loading, setLoading] = useState(true);
  const [tab, setTab] = useState("all");

  const [showCancelConfirm, setShowCancelConfirm] = useState(false);
  const [cancelPreviewData, setCancelPreviewData] = useState(null);
  const [selectedCancelId, setSelectedCancelId] = useState(null);
  const [selectedCancelBooking, setSelectedCancelBooking] = useState(null);
  const [cancelLoading, setCancelLoading] = useState(false);

  const fetchBookings = () => {
    bookingApi.getDriverBookings()
      .then((data) => setBookings(Array.isArray(data) ? data : (data?.items ?? [])))
      .catch(() => setBookings([]))
      .finally(() => setLoading(false));
  };

  useEffect(() => {
    fetchBookings();
  }, []);

  const handleStartCancel = async (e, b) => {
    e.stopPropagation();
    setSelectedCancelId(b.id);
    setSelectedCancelBooking(b);
    setCancelLoading(true);
    try {
      const preview = await bookingApi.cancelPreview(b.id);
      setCancelPreviewData(preview);
      setShowCancelConfirm(true);
    } catch {
      setCancelPreviewData(null);
      setShowCancelConfirm(true);
    } finally {
      setCancelLoading(false);
    }
  };

  const handleConfirmCancel = async () => {
    if (!selectedCancelId) return;
    setCancelLoading(true);
    try {
      await bookingApi.driverCancel(selectedCancelId, "");
      showToast.success("Hủy booking thành công");
      setShowCancelConfirm(false);
      setSelectedCancelId(null);
      setSelectedCancelBooking(null);
      fetchBookings();
    } catch (err) {
      showToast.error(err.message || "Hủy thất bại");
    } finally {
      setCancelLoading(false);
    }
  };

  const filtered = tab === "all"
    ? bookings
    : bookings.filter((b) => (statusStyles[b.status]?.group || "done") === tab);

  if (loading) {
    return (
      <div style={{ minHeight: "100vh", background: "#f8fafc", display: "flex", justifyContent: "center", alignItems: "center" }}>
        <div style={{ textAlign: "center" }}>
          <div style={{ width: 48, height: 48, border: "4px solid #e5e7eb", borderTopColor: "#f97316", borderRadius: "50%", animation: "spin 1s linear infinite", margin: "0 auto 16px" }} />
          <p style={{ color: "#64748b", fontSize: 14 }}>Đang tải danh sách booking...</p>
        </div>
        <style>{`@keyframes spin { to { transform: rotate(360deg); } }`}</style>
      </div>
    );
  }

  return (
    <div style={{ minHeight: "100vh", background: "#f8fafc", paddingTop: 84 }}>
      <div style={{ maxWidth: 720, margin: "0 auto", padding: "0 16px 40px" }}>

        {/* Header */}
        <div style={{ marginBottom: 20 }}>
          <h1 style={{ fontSize: 24, fontWeight: 800, color: "#1e293b", margin: 0 }}>
            📋 Booking của tôi
          </h1>
          <p style={{ fontSize: 13, color: "#94a3b8", marginTop: 4 }}>
            {bookings.length} booking · {bookings.filter(b => (statusStyles[b.status]?.group) === "active").length} đang xử lý
          </p>
        </div>

        {/* Tabs */}
        <div style={{
          display: "flex", gap: 4, background: "#f1f5f9", borderRadius: 12,
          padding: 4, marginBottom: 20,
        }}>
          {TABS.map((t) => {
            const count = t.key === "all" ? bookings.length : bookings.filter(b => (statusStyles[b.status]?.group || "done") === t.key).length;
            return (
              <button
                key={t.key}
                onClick={() => setTab(t.key)}
                style={{
                  flex: 1, padding: "10px 8px", borderRadius: 10, border: "none",
                  background: tab === t.key ? "#fff" : "transparent",
                  color: tab === t.key ? "#1e293b" : "#64748b",
                  fontWeight: tab === t.key ? 700 : 500,
                  fontSize: 13, cursor: "pointer",
                  boxShadow: tab === t.key ? "0 1px 4px rgba(0,0,0,0.08)" : "none",
                  transition: "all 0.2s",
                }}
              >
                {t.label} ({count})
              </button>
            );
          })}
        </div>

        {/* Booking list */}
        {filtered.length === 0 ? (
          <div style={{
            textAlign: "center", padding: "60px 20px",
            background: "#fff", borderRadius: 20,
            boxShadow: "0 2px 12px rgba(0,0,0,0.04)",
          }}>
            <div style={{ fontSize: 56, marginBottom: 12 }}>
              {tab === "active" ? "⚡" : tab === "done" ? "🎉" : "📋"}
            </div>
            <p style={{ fontSize: 16, fontWeight: 700, color: "#374151", marginBottom: 4 }}>
              {tab === "active" ? "Không có booking nào đang xử lý" : tab === "done" ? "Chưa có booking đã kết thúc" : "Bạn chưa có booking nào"}
            </p>
            <p style={{ fontSize: 13, color: "#94a3b8", marginBottom: 20 }}>
              Tìm trạm sạc gần bạn để bắt đầu đặt lịch sạc.
            </p>
            <button
              onClick={() => navigate("/driver/map")}
              style={{
                padding: "12px 28px", borderRadius: 12, border: "none",
                background: "linear-gradient(135deg, #f97316, #ea580c)",
                color: "#fff", fontWeight: 700, fontSize: 14,
                cursor: "pointer", boxShadow: "0 4px 14px rgba(249,115,22,0.3)",
              }}
            >
              🔍 Tìm trạm sạc
            </button>
          </div>
        ) : (
          <div style={{ display: "flex", flexDirection: "column", gap: 10 }}>
            {filtered.map((b) => {
              const st = statusStyles[b.status] || statusStyles.WaitingOwner;
              const isActive = st.group === "active";
              return (
                <div
                  key={b.id}
                  onClick={() => navigate(`/driver/booking/${b.id}`)}
                  style={{
                    background: "#fff", borderRadius: 16, padding: "16px 20px",
                    boxShadow: "0 2px 8px rgba(0,0,0,0.04)", cursor: "pointer",
                    borderLeft: `4px solid ${st.color}`,
                    transition: "all .15s",
                  }}
                  onMouseEnter={(e) => {
                    e.currentTarget.style.boxShadow = "0 4px 16px rgba(0,0,0,0.1)";
                    e.currentTarget.style.transform = "translateY(-1px)";
                  }}
                  onMouseLeave={(e) => {
                    e.currentTarget.style.boxShadow = "0 2px 8px rgba(0,0,0,0.04)";
                    e.currentTarget.style.transform = "translateY(0)";
                  }}
                >
                  {/* Top row: station name + status */}
                  <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 8 }}>
                    <div style={{ fontWeight: 700, fontSize: 15, color: "#1e293b", flex: 1, marginRight: 8 }}>
                      {b.stationName}
                    </div>
                    <span style={{
                      fontSize: 11, fontWeight: 700, color: st.color,
                      background: st.bg, padding: "4px 10px", borderRadius: 20,
                      whiteSpace: "nowrap", flexShrink: 0,
                    }}>
                      {st.icon} {st.label}
                    </span>
                  </div>

                  {/* Info grid */}
                  <div style={{
                    display: "grid", gridTemplateColumns: "1fr 1fr",
                    gap: "4px 16px", fontSize: 13, color: "#64748b",
                  }}>
                    <div style={{ display: "flex", alignItems: "center", gap: 4 }}>
                      <span style={{ opacity: 0.7 }}>⚡</span> Slot: <strong style={{ color: "#374151" }}>{b.slotName}</strong>
                    </div>
                    <div style={{ display: "flex", alignItems: "center", gap: 4 }}>
                      <span style={{ opacity: 0.7 }}>⏱</span> {b.durationHours}h
                    </div>
                    <div style={{ display: "flex", alignItems: "center", gap: 4 }}>
                      <span style={{ opacity: 0.7 }}>🕐</span> {toLocal(b.startTime)}
                    </div>
                    <div style={{ display: "flex", alignItems: "center", gap: 4, justifyContent: "flex-end" }}>
                      <span style={{ fontWeight: 800, color: "#f97316", fontSize: 14 }}>
                        {(b.totalAmount || 0).toLocaleString("vi-VN")}đ
                      </span>
                    </div>
                  </div>

                  {/* Extra Services */}
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

                  {/* Action hint for active bookings */}
                  {isActive && (
                    <div style={{
                      marginTop: 10, paddingTop: 8, borderTop: "1px solid #f1f5f9",
                      display: "flex", justifyContent: "space-between", alignItems: "center",
                      fontSize: 12, color: st.color, fontWeight: 600,
                    }}>
                      <div>
                        {(b.status === "Paid" || b.status === "PendingPayment" || b.status === "WaitingOwner") && (
                          <button
                            onClick={(e) => handleStartCancel(e, b)}
                            disabled={cancelLoading}
                            style={{ padding: "4px 10px", borderRadius: "6px", border: "1px solid #fca5a5", color: "#dc2626", background: "#fff", fontSize: 11, fontWeight: 600, cursor: cancelLoading ? "not-allowed" : "pointer" }}
                          >
                            Hủy Booking
                          </button>
                        )}
                      </div>
                      <div>
                        {b.status === "PendingPayment" && "Thanh toán ngay →"}
                        {b.status === "CheckedIn" && "Xem phiên sạc →"}
                        {b.status === "InProgress" && "Xem phiên sạc →"}
                        {b.status === "WaitingOwner" && "Đang chờ duyệt..."}
                        {b.status === "Paid" && "Chờ check-in →"}
                        {b.status === "CompletedPendingInvoice" && "Xác nhận hóa đơn →"}
                      </div>
                    </div>
                  )}
                </div>
              );
            })}
          </div>
        )}
      </div>

      {showCancelConfirm && (
        <div style={{
          position: "fixed", top: 0, left: 0, right: 0, bottom: 0, zIndex: 9999,
          background: "rgba(0,0,0,0.5)", display: "flex", alignItems: "center", justifyContent: "center", padding: 16,
        }}>
          <div style={{ background: "#fff", width: "100%", maxWidth: 380, borderRadius: 24, padding: "28px 24px", boxShadow: "0 24px 64px rgba(0,0,0,0.2)" }}>
            <div style={{ width: 64, height: 64, borderRadius: "50%", background: "#fef2f2", color: "#ef4444", fontSize: 32, display: "flex", alignItems: "center", justifyContent: "center", margin: "0 auto 16px" }}>
              ⚠️
            </div>
            <h3 style={{ margin: "0 0 8px", fontSize: 20, fontWeight: 800, color: "#1e293b", textAlign: "center" }}>Hủy Booking</h3>
            <p style={{ fontSize: 13, color: "#64748b", marginBottom: 24, textAlign: "center", lineHeight: 1.5 }}>
              Logic hoàn tiền được xử lý minh bạch. Bạn vui lòng xem kỹ chi phí trước khi xác nhận hủy.
            </p>
            
            {cancelPreviewData && selectedCancelBooking && (
              <div style={{ background: "#f8fafc", border: "1.5px solid #e2e8f0", borderRadius: 16, padding: "16px", marginBottom: 24 }}>
                <div style={{ display: "flex", justifyContent: "space-between", marginBottom: 12, fontSize: 14, color: "#475569" }}>
                  <span>Tiền đã thanh toán:</span>
                  <span style={{ fontWeight: 600 }}>{selectedCancelBooking.totalAmount?.toLocaleString("vi-VN")}đ</span>
                </div>
                <div style={{ display: "flex", justifyContent: "space-between", marginBottom: 16, fontSize: 14, color: "#dc2626" }}>
                  <span>Phí phạt hủy:</span>
                  <span style={{ fontWeight: 700 }}>- {cancelPreviewData.penaltyAmount?.toLocaleString("vi-VN")}đ</span>
                </div>
                <div style={{ borderTop: "1px dashed #cbd5e1", paddingTop: 16, display: "flex", justifyContent: "space-between", alignItems: "center" }}>
                  <span style={{ fontSize: 14, fontWeight: 700, color: "#0f172a" }}>Thực nhận lại:</span>
                  <span style={{ fontSize: 18, fontWeight: 800, color: "#16a34a" }}>{cancelPreviewData.refundAmount?.toLocaleString("vi-VN")}đ</span>
                </div>
                {cancelPreviewData.penaltyAmount === 0 && (
                  <div style={{ fontSize: 12, color: "#166534", marginTop: 12, textAlign: "center", fontWeight: 600, background: "#dcfce7", padding: "6px 10px", borderRadius: 8 }}>
                    ✅ Không có phí phạt. Hoàn tiền 100%.
                  </div>
                )}
              </div>
            )}

            <div style={{ display: "flex", gap: 12 }}>
              <button
                onClick={() => { setShowCancelConfirm(false); setSelectedCancelId(null); setSelectedCancelBooking(null); setCancelPreviewData(null); }}
                disabled={cancelLoading}
                style={{ flex: 1, padding: "12px 0", borderRadius: 12, border: "1px solid #cbd5e1", background: "#f8fafc", color: "#475569", fontWeight: 700, cursor: cancelLoading ? "not-allowed" : "pointer" }}
              >
                Đóng
              </button>
              <button
                onClick={handleConfirmCancel}
                disabled={cancelLoading}
                style={{ flex: 1, padding: "12px 0", borderRadius: 12, border: "none", background: "#ef4444", color: "#fff", fontWeight: 700, cursor: cancelLoading ? "not-allowed" : "pointer" }}
              >
                {cancelLoading ? "Đang hủy..." : "Xác nhận hủy"}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
