import { useState, useEffect } from "react";
import { useParams, useNavigate } from "react-router-dom";
import { bookingApi, paymentApi, walletApi, disputeApi } from "@/services/api";

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
  CompletedPendingInvoice: { label: "Chờ xác nhận hóa đơn", color: "#f97316", bg: "#fff7ed", icon: "🧾" },
};

// Parse API DateTime (không có Z) → UTC → local
const toLocal = (dt) => {
  if (!dt) return "";
  const s = String(dt);
  return new Date(s.endsWith("Z") ? s : s + "Z").toLocaleString("vi-VN");
};

export default function BookingStatus() {
  const { id } = useParams();
  const navigate = useNavigate();
  const [booking, setBooking] = useState(null);
  const [loading, setLoading] = useState(true);
  const [payLoading, setPayLoading] = useState(false);

  function fetchBooking() {
    bookingApi.getById(Number(id))
      .then(setBooking)
      .catch(() => setBooking(null))
      .finally(() => setLoading(false));
  }

  useEffect(() => {
    fetchBooking();
    const interval = setInterval(fetchBooking, 10000); // Poll every 10s
    return () => clearInterval(interval);
  }, [id]);

  async function handlePayVNPay() {
    setPayLoading(true);
    try {
      const res = await paymentApi.createPaymentUrl(Number(id));
      if (res.paymentUrl) window.location.href = res.paymentUrl;
    } catch (err) {
      alert(err.message || "Lỗi tạo link thanh toán");
    } finally {
      setPayLoading(false);
    }
  }

  async function handlePayWallet() {
    setPayLoading(true);
    try {
      await walletApi.payBooking(Number(id));
      fetchBooking();
    } catch (err) {
      alert(err.message || "Lỗi thanh toán bằng ví");
    } finally {
      setPayLoading(false);
    }
  }

  if (loading) {
    return (
      <div style={{ minHeight: "100vh", background: "#f8fafc", paddingTop: 100, textAlign: "center" }}>
        <div style={{ fontSize: 40 }}>⚡</div>
        <p style={{ color: "#6b7280" }}>Đang tải booking...</p>
      </div>
    );
  }

  if (!booking) {
    return (
      <div style={{ minHeight: "100vh", background: "#f8fafc", paddingTop: 100, textAlign: "center" }}>
        <div style={{ fontSize: 48 }}>📋</div>
        <h2 style={{ fontSize: 20, fontWeight: 700, color: "#1e293b" }}>Booking không tồn tại</h2>
        <button onClick={() => navigate("/driver/my-bookings")} style={btnStyle}>← Danh sách booking</button>
      </div>
    );
  }

  const st = statusStyles[booking.status] || statusStyles.WaitingOwner;

  return (
    <div style={{ minHeight: "100vh", background: "#f8fafc", paddingTop: 90 }}>
      <div style={{ maxWidth: 600, margin: "0 auto", padding: "0 16px 40px" }}>
        <button onClick={() => navigate("/driver/my-bookings")} style={{ background: "none", border: "none", cursor: "pointer", color: "#6b7280", fontSize: 14, marginBottom: 12, display: "flex", alignItems: "center", gap: 4 }}>
          <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><path d="M19 12H5M12 19l-7-7 7-7"/></svg>
          Danh sách booking
        </button>

        <div style={{ background: "#fff", borderRadius: 20, boxShadow: "0 2px 12px rgba(0,0,0,0.06)", padding: 24 }}>
          {/* Status badge */}
          <div style={{ textAlign: "center", marginBottom: 20 }}>
            <div style={{ fontSize: 48, marginBottom: 8 }}>{st.icon}</div>
            <span style={{ fontSize: 16, fontWeight: 700, color: st.color, background: st.bg, padding: "6px 16px", borderRadius: 20 }}>
              {st.label}
            </span>
          </div>

          {/* Details */}
          <div style={{ display: "flex", flexDirection: "column", gap: 12 }}>
            <InfoRow label="Trạm sạc" value={booking.stationName} />
            <InfoRow label="Slot" value={booking.slotName} />
            <InfoRow label="Bắt đầu" value={toLocal(booking.startTime)} />
            <InfoRow label="Kết thúc" value={toLocal(booking.endTime)} />
            <InfoRow label="Thời lượng" value={`${booking.durationHours} giờ`} />
            <InfoRow label="Tổng tiền" value={`${(booking.totalAmount || 0).toLocaleString("vi-VN")}đ`} highlight />
            {booking.note && <InfoRow label="Ghi chú" value={booking.note} />}
            {booking.rejectionReason && <InfoRow label="Lý do từ chối" value={booking.rejectionReason} error />}
            {booking.paymentExpiresAt && booking.status === "PendingPayment" && (
              <InfoRow label="Hạn thanh toán" value={toLocal(booking.paymentExpiresAt)} />
            )}
          </div>

          {/* Payment buttons */}
          {booking.status === "PendingPayment" && (
            <div style={{ marginTop: 20, display: "flex", flexDirection: "column", gap: 10 }}>
              <button
                onClick={handlePayVNPay}
                disabled={payLoading}
                style={{
                  width: "100%", padding: "14px 0", borderRadius: 14, border: "none",
                  background: payLoading ? "#d1d5db" : "linear-gradient(135deg, #3b82f6, #2563eb)",
                  color: "#fff", fontWeight: 700, fontSize: 15, cursor: payLoading ? "not-allowed" : "pointer",
                }}
              >
                {payLoading ? "Đang xử lý..." : "💳 Thanh toán VNPay"}
              </button>
              <button
                onClick={handlePayWallet}
                disabled={payLoading}
                style={{
                  width: "100%", padding: "14px 0", borderRadius: 14, border: "none",
                  background: payLoading ? "#d1d5db" : "linear-gradient(135deg, #f97316, #ea580c)",
                  color: "#fff", fontWeight: 700, fontSize: 15, cursor: payLoading ? "not-allowed" : "pointer",
                }}
              >
                {payLoading ? "Đang xử lý..." : "👛 Thanh toán bằng ví"}
              </button>
            </div>
          )}

          {/* Dispute button - CompletedPendingInvoice */}
          {booking.status === "CompletedPendingInvoice" && (
            <div style={{ marginTop: 20 }}>
              <button
                onClick={() => navigate(`/driver/dispute/submit/${booking.id}`)}
                style={{
                  width: "100%", padding: "14px 0", borderRadius: 14, border: "none",
                  background: "linear-gradient(135deg, #dc2626, #b91c1c)",
                  color: "#fff", fontWeight: 700, fontSize: 15, cursor: "pointer",
                  boxShadow: "0 4px 14px rgba(220,38,38,0.25)",
                }}
              >
                ⚠️ Khiếu nại
              </button>
            </div>
          )}

          {/* View dispute - Disputed */}
          {booking.status === "Disputed" && (
            <DisputeLink bookingId={booking.id} navigate={navigate} />
          )}
        </div>
      </div>
    </div>
  );
}

function InfoRow({ label, value, highlight, error }) {
  return (
    <div style={{ display: "flex", justifyContent: "space-between", fontSize: 14, padding: "8px 0", borderBottom: "1px solid #f1f5f9" }}>
      <span style={{ color: "#64748b" }}>{label}</span>
      <span style={{ fontWeight: 600, color: error ? "#ef4444" : highlight ? "#f97316" : "#1e293b" }}>{value}</span>
    </div>
  );
}

const btnStyle = { marginTop: 16, padding: "10px 20px", borderRadius: 10, border: "none", background: "#f97316", color: "#fff", fontWeight: 600, cursor: "pointer" };

function DisputeLink({ bookingId, navigate }) {
  const [disputeId, setDisputeId] = useState(null);
  useEffect(() => {
    disputeApi.getByBookingId(bookingId)
      .then((d) => { if (d?.id) setDisputeId(d.id); })
      .catch(() => {});
  }, [bookingId]);

  if (!disputeId) return null;
  return (
    <div style={{ marginTop: 20 }}>
      <button
        onClick={() => navigate(`/driver/dispute/${disputeId}`)}
        style={{
          width: "100%", padding: "14px 0", borderRadius: 14, border: "2px solid #dc2626",
          background: "#fff", color: "#dc2626", fontWeight: 700, fontSize: 15, cursor: "pointer",
        }}
      >
        ⚠️ Xem khiếu nại
      </button>
    </div>
  );
}
