import { useState, useEffect } from "react";
import { useParams, useNavigate } from "react-router-dom";
import { bookingApi, paymentApi, walletApi, disputeApi } from "@/services/api";
import { showToast } from "@/components/Toast";

const statusStyles = {
  WaitingOwner: { label: "Chờ chủ trạm duyệt", color: "#f59e0b", bg: "#fffbeb", bgGrad: "linear-gradient(135deg, #fef3c7, #fde68a)", icon: "⏳" },
  PendingPayment: { label: "Chờ thanh toán", color: "#3b82f6", bg: "#eff6ff", bgGrad: "linear-gradient(135deg, #dbeafe, #bfdbfe)", icon: "💳" },
  Paid: { label: "Đã thanh toán", color: "#22c55e", bg: "#f0fdf4", bgGrad: "linear-gradient(135deg, #dcfce7, #bbf7d0)", icon: "✅" },
  Expired: { label: "Hết hạn", color: "#9ca3af", bg: "#f3f4f6", bgGrad: "linear-gradient(135deg, #f3f4f6, #e5e7eb)", icon: "⏰" },
  Rejected: { label: "Bị từ chối", color: "#ef4444", bg: "#fef2f2", bgGrad: "linear-gradient(135deg, #fecaca, #fca5a5)", icon: "❌" },
  Cancelled: { label: "Đã hủy", color: "#6b7280", bg: "#f3f4f6", bgGrad: "linear-gradient(135deg, #f3f4f6, #e5e7eb)", icon: "🚫" },
  CheckedIn: { label: "Đã check-in", color: "#06b6d4", bg: "#ecfeff", bgGrad: "linear-gradient(135deg, #cffafe, #a5f3fc)", icon: "⚡" },
  InProgress: { label: "Đang sạc", color: "#06b6d4", bg: "#ecfeff", bgGrad: "linear-gradient(135deg, #cffafe, #a5f3fc)", icon: "🔋" },
  Completed: { label: "Hoàn thành", color: "#8b5cf6", bg: "#f5f3ff", bgGrad: "linear-gradient(135deg, #ede9fe, #ddd6fe)", icon: "🎉" },
  NoShow: { label: "Không đến", color: "#9ca3af", bg: "#f3f4f6", bgGrad: "linear-gradient(135deg, #f3f4f6, #e5e7eb)", icon: "🚷" },
  Disputed: { label: "Tranh chấp", color: "#dc2626", bg: "#fef2f2", bgGrad: "linear-gradient(135deg, #fecaca, #fca5a5)", icon: "⚠️" },
  CompletedPendingInvoice: { label: "Chờ xác nhận hóa đơn", color: "#f97316", bg: "#fff7ed", bgGrad: "linear-gradient(135deg, #fed7aa, #fdba74)", icon: "🧾" },
};

const toLocal = (dt) => {
  if (!dt) return "";
  const s = String(dt);
  return new Date(String(s).replace("Z", "")).toLocaleString("vi-VN", {
    hour: "2-digit", minute: "2-digit",
    day: "2-digit", month: "2-digit", year: "numeric",
    hour12: false,
  });
};

export default function BookingStatus() {
  const { id } = useParams();
  const navigate = useNavigate();
  const [booking, setBooking] = useState(null);
  const [loading, setLoading] = useState(true);
  const [payLoading, setPayLoading] = useState(false);
  const [cancelLoading, setCancelLoading] = useState(false);
  const [showCancelForm, setShowCancelForm] = useState(false);
  const [cancelReason, setCancelReason] = useState("");

  function fetchBooking() {
    bookingApi.getById(Number(id))
      .then(setBooking)
      .catch(() => setBooking(null))
      .finally(() => setLoading(false));
  }

  useEffect(() => {
    fetchBooking();
    const interval = setInterval(fetchBooking, 10000);
    return () => clearInterval(interval);
  }, [id]);

  async function handlePayVNPay() {
    setPayLoading(true);
    try {
      const res = await paymentApi.createPaymentUrl(Number(id));
      if (res.paymentUrl) window.location.href = res.paymentUrl;
    } catch (err) {
      showToast.error(err.message || "Lỗi tạo link thanh toán");
    } finally { setPayLoading(false); }
  }

  async function handlePayWallet() {
    setPayLoading(true);
    try {
      await walletApi.payBooking(Number(id));
      fetchBooking();
    } catch (err) {
      showToast.error(err.message || "Lỗi thanh toán bằng ví");
    } finally { setPayLoading(false); }
  }

  async function submitCancel() {
    setCancelLoading(true);
    try {
      await bookingApi.driverCancel(Number(id), cancelReason || "");
      setShowCancelForm(false);
      setCancelReason("");
      fetchBooking();
    } catch (err) {
      showToast.error(err.message || "Lỗi hủy booking");
    } finally { setCancelLoading(false); }
  }

  if (loading) {
    return (
      <div style={{ minHeight: "100vh", background: "#f8fafc", display: "flex", justifyContent: "center", alignItems: "center" }}>
        <div style={{ textAlign: "center" }}>
          <div style={{ width: 48, height: 48, border: "4px solid #e5e7eb", borderTopColor: "#f97316", borderRadius: "50%", animation: "spin 1s linear infinite", margin: "0 auto 16px" }} />
          <p style={{ color: "#64748b", fontSize: 14 }}>Đang tải booking...</p>
        </div>
        <style>{`@keyframes spin { to { transform: rotate(360deg); } }`}</style>
      </div>
    );
  }

  if (!booking) {
    return (
      <div style={{ minHeight: "100vh", background: "#f8fafc", display: "flex", justifyContent: "center", alignItems: "center", flexDirection: "column" }}>
        <div style={{ fontSize: 56, marginBottom: 12 }}>📋</div>
        <h2 style={{ fontSize: 20, fontWeight: 700, color: "#1e293b", margin: "0 0 16px" }}>Booking không tồn tại</h2>
        <button
          onClick={() => navigate("/driver/my-bookings")}
          style={{ padding: "12px 24px", borderRadius: 12, border: "none", background: "#f97316", color: "#fff", fontWeight: 700, cursor: "pointer" }}
        >
          ← Danh sách booking
        </button>
      </div>
    );
  }

  const st = statusStyles[booking.status] || statusStyles.WaitingOwner;

  return (
    <div style={{ minHeight: "100vh", background: "#f8fafc", paddingTop: 84 }}>
      <div style={{ maxWidth: 600, margin: "0 auto", padding: "0 16px 40px" }}>

        {/* Back button */}
        <button
          onClick={() => navigate("/driver/my-bookings")}
          style={{
            background: "none", border: "none", cursor: "pointer",
            color: "#64748b", fontSize: 13, marginBottom: 16,
            display: "flex", alignItems: "center", gap: 6,
            fontWeight: 500,
          }}
        >
          <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><path d="M19 12H5M12 19l-7-7 7-7"/></svg>
          Danh sách booking
        </button>

        {/* Status hero */}
        <div style={{
          borderRadius: 20, padding: "28px 24px", marginBottom: 16,
          background: st.bgGrad, textAlign: "center",
        }}>
          <div style={{ fontSize: 52, marginBottom: 8 }}>{st.icon}</div>
          <span style={{
            fontSize: 16, fontWeight: 800, color: st.color,
            background: "#fff", padding: "6px 20px", borderRadius: 20,
            display: "inline-block",
          }}>
            {st.label}
          </span>
          <div style={{ marginTop: 10, fontSize: 20, fontWeight: 800, color: "#1e293b" }}>
            {booking.stationName}
          </div>
          <div style={{ fontSize: 13, color: "#64748b", marginTop: 2 }}>
            Booking #{booking.id}
          </div>
        </div>

        {/* Details card */}
        <div style={{
          background: "#fff", borderRadius: 20, padding: 24,
          boxShadow: "0 2px 12px rgba(0,0,0,0.06)", marginBottom: 16,
        }}>
          <h3 style={{ fontSize: 14, fontWeight: 700, color: "#374151", marginBottom: 12, display: "flex", alignItems: "center", gap: 6 }}>
            📋 Chi tiết booking
          </h3>

          <div style={{ display: "flex", flexDirection: "column", gap: 0 }}>
            <InfoRow icon="⚡" label="Slot" value={booking.slotName} />
            <InfoRow icon="📅" label="Bắt đầu" value={toLocal(booking.startTime)} />
            <InfoRow icon="🏁" label="Kết thúc" value={toLocal(booking.endTime)} />
            <InfoRow icon="⏱" label="Thời lượng" value={`${booking.durationHours} giờ`} />
            <InfoRow icon="💰" label="Tổng tiền" value={`${(booking.totalAmount || 0).toLocaleString("vi-VN")}đ`} highlight />
            {booking.note && <InfoRow icon="📝" label="Ghi chú" value={booking.note} />}
            {booking.rejectionReason && <InfoRow icon="❌" label="Lý do từ chối" value={booking.rejectionReason} error />}
            {booking.cancelReason && <InfoRow icon="🚫" label="Lý do hủy" value={booking.cancelReason} error />}
            {booking.paymentExpiresAt && booking.status === "PendingPayment" && (
              <InfoRow icon="⏰" label="Hạn thanh toán" value={toLocal(booking.paymentExpiresAt)} warning />
            )}
          </div>
        </div>

        {/* Action cards */}
        {booking.status === "PendingPayment" && (
          <div style={{
            background: "#fff", borderRadius: 20, padding: 24,
            boxShadow: "0 2px 12px rgba(0,0,0,0.06)", marginBottom: 16,
          }}>
            <h3 style={{ fontSize: 14, fontWeight: 700, color: "#374151", marginBottom: 12, display: "flex", alignItems: "center", gap: 6 }}>
              💳 Thanh toán
            </h3>
            <div style={{ display: "flex", flexDirection: "column", gap: 10 }}>
              <ActionButton
                onClick={handlePayVNPay}
                disabled={payLoading}
                bg="linear-gradient(135deg, #3b82f6, #2563eb)"
                shadow="rgba(59,130,246,0.25)"
              >
                {payLoading ? "Đang xử lý..." : "💳 Thanh toán VNPay"}
              </ActionButton>
              <ActionButton
                onClick={handlePayWallet}
                disabled={payLoading}
                bg="linear-gradient(135deg, #f97316, #ea580c)"
                shadow="rgba(249,115,22,0.25)"
              >
                {payLoading ? "Đang xử lý..." : "👛 Thanh toán bằng ví"}
              </ActionButton>
            </div>
          </div>
        )}

        {/* Charging session link */}
        {(booking.status === "CheckedIn" || booking.status === "InProgress") && (
          <ActionButton
            onClick={() => {
              localStorage.setItem("activeChargingBookingId", String(booking.id));
              navigate("/driver/charging");
            }}
            bg="linear-gradient(135deg, #3b82f6, #2563eb)"
            shadow="rgba(59,130,246,0.25)"
            style={{ marginBottom: 16 }}
          >
            ⚡ Xem phiên sạc
          </ActionButton>
        )}

        {/* Cancel section */}
        {(booking.status === "Paid" || booking.status === "WaitingOwner" || booking.status === "PendingPayment") && (
          <div style={{ marginBottom: 16 }}>
            {!showCancelForm ? (
              <button
                onClick={() => setShowCancelForm(true)}
                style={{
                  width: "100%", padding: "14px 0", borderRadius: 14,
                  border: "2px solid #fca5a5", background: "#fff",
                  color: "#ef4444", fontWeight: 700, fontSize: 14,
                  cursor: "pointer", transition: "all 0.2s",
                }}
                onMouseEnter={(e) => { e.currentTarget.style.background = "#fef2f2"; }}
                onMouseLeave={(e) => { e.currentTarget.style.background = "#fff"; }}
              >
                🚫 Hủy booking
              </button>
            ) : (
              <div style={{
                border: "2px solid #fecaca", borderRadius: 16,
                padding: 20, background: "#fef2f2",
              }}>
                <p style={{ fontSize: 14, fontWeight: 700, color: "#dc2626", marginBottom: 10 }}>
                  Lý do hủy booking
                </p>
                <textarea
                  value={cancelReason}
                  onChange={(e) => setCancelReason(e.target.value)}
                  placeholder="Nhập lý do hủy (không bắt buộc)..."
                  rows={3}
                  style={{
                    width: "100%", padding: 12, borderRadius: 10,
                    border: "1px solid #fca5a5", fontSize: 14,
                    resize: "vertical", outline: "none", boxSizing: "border-box",
                  }}
                />
                <div style={{ display: "flex", gap: 8, marginTop: 12 }}>
                  <button
                    onClick={submitCancel}
                    disabled={cancelLoading}
                    style={{
                      flex: 1, padding: "12px 0", borderRadius: 10, border: "none",
                      background: cancelLoading ? "#d1d5db" : "#ef4444",
                      color: "#fff", fontWeight: 700, fontSize: 14,
                      cursor: cancelLoading ? "not-allowed" : "pointer",
                    }}
                  >
                    {cancelLoading ? "Đang hủy..." : "Xác nhận hủy"}
                  </button>
                  <button
                    onClick={() => { setShowCancelForm(false); setCancelReason(""); }}
                    disabled={cancelLoading}
                    style={{
                      flex: 1, padding: "12px 0", borderRadius: 10,
                      border: "1px solid #d1d5db", background: "#fff",
                      color: "#6b7280", fontWeight: 600, fontSize: 14,
                      cursor: "pointer",
                    }}
                  >
                    Không hủy
                  </button>
                </div>
              </div>
            )}
          </div>
        )}

        {/* Dispute */}
        {(booking.status === "CompletedPendingInvoice" || booking.status === "Completed") && (
          <ActionButton
            onClick={() => navigate(`/driver/dispute/submit/${booking.id}`)}
            bg="linear-gradient(135deg, #dc2626, #b91c1c)"
            shadow="rgba(220,38,38,0.25)"
            style={{ marginBottom: 16 }}
          >
            ⚠️ Khiếu nại
          </ActionButton>
        )}

        {booking.status === "Disputed" && (
          <DisputeLink bookingId={booking.id} navigate={navigate} />
        )}
      </div>
    </div>
  );
}

function InfoRow({ icon, label, value, highlight, error, warning }) {
  return (
    <div style={{
      display: "flex", justifyContent: "space-between", alignItems: "center",
      fontSize: 14, padding: "10px 0",
      borderBottom: "1px solid #f8fafc",
    }}>
      <span style={{ color: "#64748b", display: "flex", alignItems: "center", gap: 6 }}>
        <span style={{ fontSize: 13 }}>{icon}</span> {label}
      </span>
      <span style={{
        fontWeight: 600, textAlign: "right", maxWidth: "55%",
        color: error ? "#ef4444" : warning ? "#f59e0b" : highlight ? "#f97316" : "#1e293b",
      }}>
        {value}
      </span>
    </div>
  );
}

function ActionButton({ onClick, disabled, bg, shadow, children, style = {} }) {
  return (
    <button
      onClick={onClick}
      disabled={disabled}
      style={{
        width: "100%", padding: "14px 0", borderRadius: 14, border: "none",
        background: disabled ? "#d1d5db" : bg,
        color: "#fff", fontWeight: 700, fontSize: 15,
        cursor: disabled ? "not-allowed" : "pointer",
        boxShadow: `0 4px 14px ${shadow || "rgba(0,0,0,0.1)"}`,
        transition: "all 0.2s",
        ...style,
      }}
    >
      {children}
    </button>
  );
}

function DisputeLink({ bookingId, navigate }) {
  const [disputeId, setDisputeId] = useState(null);
  useEffect(() => {
    disputeApi.getByBookingId(bookingId)
      .then((d) => { if (d?.id) setDisputeId(d.id); })
      .catch(() => {});
  }, [bookingId]);

  if (!disputeId) return null;
  return (
    <button
      onClick={() => navigate(`/driver/dispute/${disputeId}`)}
      style={{
        width: "100%", padding: "14px 0", borderRadius: 14,
        border: "2px solid #dc2626", background: "#fff",
        color: "#dc2626", fontWeight: 700, fontSize: 15,
        cursor: "pointer", marginBottom: 16,
      }}
    >
      ⚠️ Xem khiếu nại
    </button>
  );
}
