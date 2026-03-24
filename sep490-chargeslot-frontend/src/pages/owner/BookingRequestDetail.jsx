import { useState, useEffect } from "react";
import { useParams, useNavigate } from "react-router-dom";
import { bookingApi, disputeApi } from "@/services/api";
import { showToast } from "@/components/Toast";

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
  return new Date(s.endsWith("Z") ? s : s + "Z").toLocaleString("vi-VN");
};

export default function BookingRequestDetail() {
  const { id } = useParams();
  const navigate = useNavigate();
  const [booking, setBooking] = useState(null);
  const [loading, setLoading] = useState(true);
  const [actionLoading, setActionLoading] = useState(false);
  const [rejectReason, setRejectReason] = useState("");
  const [showRejectForm, setShowRejectForm] = useState(false);

  useEffect(() => {
    bookingApi.getById(Number(id))
      .then(setBooking)
      .catch(() => setBooking(null))
      .finally(() => setLoading(false));
  }, [id]);

  async function handleAccept() {
    setActionLoading(true);
    try {
      const updated = await bookingApi.accept(Number(id));
      setBooking(updated);
    } catch (err) {
      showToast.error(err.message || "Lỗi khi chấp nhận");
    } finally {
      setActionLoading(false);
    }
  }

  async function handleReject() {
    if (!rejectReason.trim()) return showToast.warning("Vui lòng nhập lý do từ chối");
    setActionLoading(true);
    try {
      const updated = await bookingApi.reject(Number(id), rejectReason);
      setBooking(updated);
      setShowRejectForm(false);
    } catch (err) {
      showToast.error(err.message || "Lỗi khi từ chối");
    } finally {
      setActionLoading(false);
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
      </div>
    );
  }

  const st = statusStyles[booking.status] || statusStyles.WaitingOwner;

  return (
    <div style={{ minHeight: "100vh", background: "#f8fafc", paddingTop: 90 }}>
      <div style={{ maxWidth: 600, margin: "0 auto", padding: "0 16px 40px" }}>
        <button onClick={() => navigate("/owner/booking-requests")} style={{ background: "none", border: "none", cursor: "pointer", color: "#6b7280", fontSize: 14, marginBottom: 12, display: "flex", alignItems: "center", gap: 4 }}>
          <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><path d="M19 12H5M12 19l-7-7 7-7"/></svg>
          Quay lại
        </button>

        <div style={{ background: "#fff", borderRadius: 20, boxShadow: "0 2px 12px rgba(0,0,0,0.06)", padding: 24 }}>
          <div style={{ textAlign: "center", marginBottom: 20 }}>
            <div style={{ fontSize: 48, marginBottom: 8 }}>{st.icon}</div>
            <span style={{ fontSize: 16, fontWeight: 700, color: st.color, background: st.bg, padding: "6px 16px", borderRadius: 20 }}>
              {st.label}
            </span>
          </div>

          <div style={{ display: "flex", flexDirection: "column", gap: 12 }}>
            <InfoRow label="Booking #" value={booking.id} />
            <InfoRow label="Driver" value={booking.driverName} />
            <InfoRow label="Trạm" value={booking.stationName} />
            <InfoRow label="Slot" value={booking.slotName} />
            <InfoRow label="Bắt đầu" value={toLocal(booking.startTime)} />
            <InfoRow label="Kết thúc" value={toLocal(booking.endTime)} />
            <InfoRow label="Thời lượng" value={`${booking.durationHours} giờ`} />
            <InfoRow label="Tổng tiền" value={`${(booking.totalAmount || 0).toLocaleString("vi-VN")}đ`} highlight />
            {booking.note && <InfoRow label="Ghi chú" value={booking.note} />}
            {booking.rejectionReason && <InfoRow label="Lý do từ chối" value={booking.rejectionReason} error />}
          </div>

          {/* Accept/Reject buttons */}
          {booking.status === "WaitingOwner" && (
            <div style={{ marginTop: 24 }}>
              {showRejectForm ? (
                <div>
                  <textarea
                    value={rejectReason}
                    onChange={(e) => setRejectReason(e.target.value)}
                    placeholder="Lý do từ chối..."
                    rows={3}
                    style={{ width: "100%", padding: 12, borderRadius: 10, border: "1.5px solid #e5e7eb", fontSize: 14, marginBottom: 12, resize: "vertical", boxSizing: "border-box" }}
                  />
                  <div style={{ display: "flex", gap: 8 }}>
                    <button onClick={handleReject} disabled={actionLoading} style={{ flex: 1, padding: 12, borderRadius: 12, border: "none", background: "#ef4444", color: "#fff", fontWeight: 600, cursor: "pointer" }}>
                      {actionLoading ? "Đang xử lý..." : "Xác nhận từ chối"}
                    </button>
                    <button onClick={() => setShowRejectForm(false)} style={{ padding: "12px 20px", borderRadius: 12, border: "1.5px solid #e5e7eb", background: "#fff", color: "#374151", fontWeight: 600, cursor: "pointer" }}>
                      Hủy
                    </button>
                  </div>
                </div>
              ) : (
                <div style={{ display: "flex", gap: 8 }}>
                  <button onClick={handleAccept} disabled={actionLoading} style={{ flex: 1, padding: 14, borderRadius: 14, border: "none", background: "linear-gradient(135deg, #22c55e, #16a34a)", color: "#fff", fontWeight: 700, fontSize: 15, cursor: "pointer" }}>
                    {actionLoading ? "Đang xử lý..." : "✅ Chấp nhận"}
                  </button>
                  <button onClick={() => setShowRejectForm(true)} style={{ flex: 1, padding: 14, borderRadius: 14, border: "none", background: "linear-gradient(135deg, #ef4444, #dc2626)", color: "#fff", fontWeight: 700, fontSize: 15, cursor: "pointer" }}>
                    ❌ Từ chối
                  </button>
                </div>
              )}
            </div>
          )}

          {/* Dispute link */}
          {booking.status === "Disputed" && (
            <OwnerDisputeLink bookingId={booking.id} navigate={navigate} />
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

function OwnerDisputeLink({ bookingId, navigate }) {
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
        onClick={() => navigate(`/owner/dispute/${disputeId}`)}
        style={{
          width: "100%", padding: "14px 0", borderRadius: 14, border: "2px solid #dc2626",
          background: "#fff", color: "#dc2626", fontWeight: 700, fontSize: 15, cursor: "pointer",
        }}
      >
        ⚠️ Xem khiếu nại & Phản hồi
      </button>
    </div>
  );
}
