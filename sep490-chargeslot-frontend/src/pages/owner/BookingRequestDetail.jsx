import { useState, useEffect } from "react";
import { useParams, useNavigate } from "react-router-dom";
import { bookingApi, disputeApi, chargingApi } from "@/services/api";
import { showToast } from "@/components/Toast";

const statusStyles = {
  WaitingOwner: { label: "Chờ duyệt", color: "#f59e0b", bg: "#fffbeb", icon: "" },
  PendingPayment: { label: "Chờ thanh toán", color: "#3b82f6", bg: "#eff6ff", icon: "" },
  Paid: { label: "Giữ chỗ", color: "#22c55e", bg: "#f0fdf4", icon: "" },
  Expired: { label: "Hết hạn", color: "#9ca3af", bg: "#f3f4f6", icon: "" },
  Rejected: { label: "Đã từ chối", color: "#ef4444", bg: "#fef2f2", icon: "" },
  Cancelled: { label: "Đã hủy", color: "#6b7280", bg: "#f3f4f6", icon: "" },
  CheckedIn: { label: "Đã check-in", color: "#06b6d4", bg: "#ecfeff", icon: "" },
  InProgress: { label: "Đang sạc", color: "#06b6d4", bg: "#ecfeff", icon: "" },
  CompletedPendingInvoice: { label: "Chờ xác nhận", color: "#f97316", bg: "#fff7ed", icon: "" },
  Completed: { label: "Hoàn thành", color: "#8b5cf6", bg: "#f5f3ff", icon: "" },
  NoShow: { label: "Không đến", color: "#9ca3af", bg: "#f3f4f6", icon: "" },
  Disputed: { label: "Tranh chấp", color: "#dc2626", bg: "#fef2f2", icon: "" },
};

const toLocal = (dt) => {
  if (!dt) return "";
  const s = String(dt);
  return new Date(String(s).replace("Z", "")).toLocaleString("vi-VN");
};

export default function BookingRequestDetail() {
  const { id } = useParams();
  const navigate = useNavigate();
  const [booking, setBooking] = useState(null);
  const [sessionDetail, setSessionDetail] = useState(null);
  const [loading, setLoading] = useState(true);
  const [actionLoading, setActionLoading] = useState(false);
  const [rejectReason, setRejectReason] = useState("");
  const [showRejectForm, setShowRejectForm] = useState(false);
  // const [manualCheckinLoading, setManualCheckinLoading] = useState(false); // TẠM ẨN

  useEffect(() => {
    bookingApi.getById(Number(id))
      .then(async (data) => {
        setBooking(data);
        if (data) {
          if (data.chargingSessionDetail) {
            setSessionDetail(data.chargingSessionDetail);
          } else {
            try {
              const session = await chargingApi.getByBookingId(Number(id));
              setSessionDetail(session);
            } catch { /* ignore */ }
          }
        }
      })
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

  // ── Manual check-in (Owner) — TẠM ẨN ──
  // async function handleConfirmManualCheckin() {
  //   if (!window.confirm(
  //     "Xác nhận check-in thủ công cho Driver?\n" +
  //     "Hành động này sẽ bắt đầu phiên sạc ngay lập tức."
  //   )) return;
  //   setManualCheckinLoading(true);
  //   try {
  //     await chargingApi.confirmManualCheckin(Number(id));
  //     showToast.success(" Đã xác nhận check-in thủ công! Phiên sạc bắt đầu.");
  //     const updated = await bookingApi.getById(Number(id));
  //     setBooking(updated);
  //   } catch (err) {
  //     showToast.error(err.message || "Lỗi xác nhận check-in thủ công");
  //   } finally {
  //     setManualCheckinLoading(false);
  //   }
  // }

  if (loading) {
    return (
      <div style={{ minHeight: "100vh", background: "#f8fafc", paddingTop: 100, textAlign: "center" }}>
        <div style={{ fontSize: 40 }}></div>
        <p style={{ color: "#6b7280" }}>Đang tải booking...</p>
      </div>
    );
  }

  if (!booking) {
    return (
      <div style={{ minHeight: "100vh", background: "#f8fafc", paddingTop: 100, textAlign: "center" }}>
        <div style={{ fontSize: 48 }}></div>
        <h2 style={{ fontSize: 20, fontWeight: 700, color: "#1e293b" }}>Booking không tồn tại</h2>
      </div>
    );
  }

  const st = statusStyles[booking.status] || statusStyles.WaitingOwner;

  // Chỉ hiện cột "Chi tiết nâng cao" khi có ít nhất 1 dữ liệu thực tế
  const hasAdvancedDetail = !!(booking.paymentDetail || booking.invoiceDetail || sessionDetail || booking.disputeDetail);

  const renderActions = () => (
    <div style={{ marginTop: hasAdvancedDetail ? "auto" : 20, borderTop: "2px solid #e2e8f0", paddingTop: 20 }}>
      {/* Chat button */}
      {!["Cancelled", "Rejected", "Expired", "NoShow", "Completed", "CompletedPendingInvoice"].includes(booking.status) && (
        <div style={{ marginBottom: 12 }}>
          <button
            onClick={() => navigate(`/owner/chat/${booking.id}`)}
            style={{ width: "100%", padding: "12px 0", borderRadius: 12, border: "2px solid #3b82f6", background: "#eff6ff", color: "#3b82f6", fontWeight: 700, fontSize: 14, cursor: "pointer", transition: "all 0.2s", display: "flex", alignItems: "center", justifyContent: "center", gap: 8 }}
            onMouseEnter={e => { e.currentTarget.style.background = "#dbeafe"; }}
            onMouseLeave={e => { e.currentTarget.style.background = "#eff6ff"; }}
          >
             Chat với Driver
          </button>
        </div>
      )}

      {/* Accept/Reject buttons */}
      {booking.status === "WaitingOwner" && (
        <div style={{ marginBottom: 12 }}>
          {showRejectForm ? (
            <div className="animate-in fade-in zoom-in duration-200">
              <textarea value={rejectReason} onChange={(e) => setRejectReason(e.target.value)} placeholder="Lý do từ chối..." rows={3} style={{ width: "100%", padding: 12, borderRadius: 10, border: "1.5px solid #e5e7eb", fontSize: 14, marginBottom: 12, resize: "vertical", outline: "none", boxSizing: "border-box" }} />
              <div style={{ display: "flex", gap: 8 }}>
                <button onClick={handleReject} disabled={actionLoading} style={{ flex: 1, padding: 12, borderRadius: 10, border: "none", background: "#ef4444", color: "#fff", fontWeight: 700, cursor: "pointer" }}>
                  {actionLoading ? "Đang xử lý..." : "Xác nhận từ chối"}
                </button>
                <button onClick={() => setShowRejectForm(false)} style={{ padding: "12px 20px", borderRadius: 10, border: "1.5px solid #e5e7eb", background: "#fff", color: "#374151", fontWeight: 600, cursor: "pointer" }}>Hủy</button>
              </div>
            </div>
          ) : (
            <div style={{ display: "flex", gap: 8 }}>
              <button onClick={handleAccept} disabled={actionLoading} style={{ flex: 1, padding: 14, borderRadius: 12, border: "none", background: "linear-gradient(135deg, #22c55e, #16a34a)", color: "#fff", fontWeight: 700, fontSize: 15, cursor: "pointer", boxShadow: "0 4px 12px rgba(34,197,94,0.3)" }}>
                {actionLoading ? "Đang xử lý..." : " Chấp nhận"}
              </button>
              <button onClick={() => setShowRejectForm(true)} style={{ flex: 1, padding: 14, borderRadius: 12, border: "none", background: "linear-gradient(135deg, #ef4444, #dc2626)", color: "#fff", fontWeight: 700, fontSize: 15, cursor: "pointer", boxShadow: "0 4px 12px rgba(239,68,68,0.3)" }}>
                 Từ chối
              </button>
            </div>
          )}
        </div>
      )}

      {/* ── Manual check-in card (Owner) — TẠM ẨN ──
      {booking.status === "Paid" && (
        <div style={{ marginBottom: 12 }}>
          <div style={{
            padding: "12px 16px", background: "#fffbeb", border: "1.5px solid #f59e0b",
            borderRadius: 12, marginBottom: 8, fontSize: 13, color: "#92400e", lineHeight: 1.5,
          }}>
             <strong>Nếu Driver báo không quét được QR:</strong> Dùng nút bên dưới để xác nhận check-in thủ công.
          </div>
          <button
            onClick={handleConfirmManualCheckin}
            disabled={manualCheckinLoading}
            style={{
              width: "100%", padding: 14, borderRadius: 12, border: "none",
              background: manualCheckinLoading ? "#d1d5db" : "linear-gradient(135deg, #f59e0b, #d97706)",
              color: "#fff", fontWeight: 700, fontSize: 14,
              cursor: manualCheckinLoading ? "not-allowed" : "pointer",
            }}
          >
            {manualCheckinLoading ? "Đang xác nhận..." : " Xác nhận Check-in Thủ công"}
          </button>
        </div>
      )}
      */}

      {/* Owner cancel — cho booking đã Paid hoặc PendingPayment */}
      {(booking.status === "Paid" || booking.status === "PendingPayment") && (
        <OwnerCancelSection bookingId={booking.id} onDone={(updated) => setBooking(updated)} />
      )}

      {/* Dispute link */}
      {booking.status === "Disputed" && (
        <OwnerDisputeLink bookingId={booking.id} navigate={navigate} />
      )}
    </div>
  );

  return (
    <div style={{ minHeight: "100vh", background: "#f8fafc", paddingTop: 90 }}>
      <div style={{ maxWidth: 1000, margin: "0 auto", padding: "0 16px 40px" }}>
        <button onClick={() => navigate("/owner/booking-requests")} style={{ background: "none", border: "none", cursor: "pointer", color: "#6b7280", fontSize: 14, marginBottom: 12, display: "flex", alignItems: "center", gap: 4 }}>
          <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><path d="M19 12H5M12 19l-7-7 7-7" /></svg>
          Quay lại
        </button>

        <div style={{ background: "#fff", borderRadius: 20, boxShadow: "0 2px 12px rgba(0,0,0,0.06)", padding: 24 }}>
          <div style={{ textAlign: "center", marginBottom: 20 }}>
            <div style={{ fontSize: 48, marginBottom: 8 }}>{st.icon}</div>
            <span style={{ fontSize: 16, fontWeight: 700, color: st.color, background: st.bg, padding: "6px 16px", borderRadius: 20 }}>
              {st.label}
            </span>
          </div>

          <div className={hasAdvancedDetail ? "grid grid-cols-1 md:grid-cols-2 gap-8 md:gap-12" : "grid grid-cols-1 max-w-lg mx-auto w-full"}>
            {/* ========== CỘT TRÁI: Thông tin cơ bản ========== */}
            <div style={{ display: "flex", flexDirection: "column", gap: 12 }}>
              <div style={{ fontSize: 16, fontWeight: 700, color: "#1e293b", marginBottom: 4, borderBottom: "1px solid #e2e8f0", paddingBottom: 8 }}>Thông tin đặt lịch</div>
              <InfoRow label="Booking #" value={booking.id} />
              <InfoRow label="Driver" value={booking.driverName} />
              <InfoRow label="Trạm" value={booking.stationName} />
              <InfoRow label="Slot" value={booking.slotName} />
              <InfoRow label="Bắt đầu" value={toLocal(booking.startTime)} />
              <InfoRow label="Kết thúc" value={toLocal(booking.endTime)} />
              <InfoRow label="Thời lượng" value={`${Math.round(booking.durationHours * 60)} phút`} />

              {/* Split costs when extras exist */}
              {booking.extraServices && booking.extraServices.length > 0 ? (
                <>
                  <InfoRow label="Phí sạc" value={`${((booking.totalAmount || 0) - (booking.serviceAmount || 0)).toLocaleString("vi-VN")}đ`} />
                  <InfoRow label="Phí dịch vụ" value={`${(booking.serviceAmount || 0).toLocaleString("vi-VN")}đ`} purple />
                  <InfoRow label="Tổng tiền" value={`${(booking.totalAmount || 0).toLocaleString("vi-VN")}đ`} highlight />
                </>
              ) : (
                <InfoRow label="Tổng tiền" value={`${(booking.totalAmount || 0).toLocaleString("vi-VN")}đ`} highlight />
              )}

              {booking.note && <InfoRow label="Ghi chú" value={booking.note} />}
              {booking.rejectionReason && <InfoRow label="Lý do từ chối" value={booking.rejectionReason} error />}

              {/* Extra Services breakdown */}
              {booking.extraServices && booking.extraServices.length > 0 && (
                <div style={{ marginTop: 8, paddingTop: 12, borderTop: "1px dashed #e2e8f0" }}>
                  <div style={{ fontSize: 13, fontWeight: 700, color: "#7c3aed", marginBottom: 10, display: "flex", alignItems: "center", gap: 6 }}>
                     Dịch vụ bổ sung
                  </div>
                  {booking.extraServices.map((es, idx) => (
                    <div key={idx} style={{
                      display: "flex", justifyContent: "space-between", alignItems: "center",
                      padding: "8px 0", borderBottom: "1px solid #f8fafc",
                    }}>
                      <div style={{ flex: 1 }}>
                        <div style={{ fontSize: 13, fontWeight: 600, color: "#1e293b" }}>{es.serviceName}</div>
                        <div style={{ fontSize: 11, color: "#94a3b8" }}>
                          {es.unitPrice?.toLocaleString("vi-VN")}đ × {es.quantity}
                        </div>
                      </div>
                      <span style={{ fontWeight: 700, color: "#7c3aed", fontSize: 13 }}>
                        {es.totalPrice?.toLocaleString("vi-VN")}đ
                      </span>
                    </div>
                  ))}
                </div>
              )}

              {/* Nếu không có advanced details, render actions dưới cột thông tin cơ bản */}
              {!hasAdvancedDetail && renderActions()}
            </div>

            {/* ========== CỘT PHẢI: Chi tiết nâng cao (chỉ hiện khi có dữ liệu) ========== */}
            {hasAdvancedDetail && (
            <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
              <div style={{ fontSize: 16, fontWeight: 700, color: "#1e293b", marginBottom: -4, borderBottom: "1px solid #e2e8f0", paddingBottom: 8 }}>Chi tiết nâng cao</div>

              <div style={{ display: "flex", flexDirection: "column", gap: 12 }}>
                {/* DEEP DETAILS cho Owner */}
                {booking.paymentDetail && (
                  <div>
                    <div style={{ fontSize: 13, color: "#475569", marginBottom: 6, fontWeight: 700 }}> Thanh toán</div>
                    <InfoRow label="Phương thức" value={booking.paymentDetail.method === "Wallet" ? "Ví hệ thống" : booking.paymentDetail.method === "BankTransfer" ? "Chuyển khoản" : booking.paymentDetail.method} />
                    {booking.paymentDetail.paidAt && <InfoRow label="Ngày thanh toán" value={toLocal(booking.paymentDetail.paidAt)} />}
                    {booking.paymentDetail.refundedAt && <InfoRow label="Ngày hoàn tiền" value={toLocal(booking.paymentDetail.refundedAt)} error />}
                  </div>
                )}

                {booking.invoiceDetail && (
                  <div style={{ marginTop: 4, borderTop: "1px dashed #e2e8f0", paddingTop: 12 }}>
                    <div style={{ fontSize: 13, color: "#475569", marginBottom: 6, fontWeight: 700 }}> Chi tiết hóa đơn</div>
                    <InfoRow label="Tiền sạc" value={`${(booking.invoiceDetail.chargingAmount || 0).toLocaleString("vi-VN")}đ`} />
                    {booking.invoiceDetail.vatAmount > 0 && <InfoRow label="Thuế VAT" value={`${booking.invoiceDetail.vatAmount.toLocaleString("vi-VN")}đ`} />}
                    <InfoRow label="Tổng thanh toán" value={`${(booking.invoiceDetail.totalAmount || 0).toLocaleString("vi-VN")}đ`} highlight />
                  </div>
                )}

                {sessionDetail && (
                  <div style={{ marginTop: 4, borderTop: "1px dashed #e2e8f0", paddingTop: 12 }}>
                    <div style={{ fontSize: 13, color: "#475569", marginBottom: 6, fontWeight: 700 }}> Chi tiết phiên sạc thực tế</div>
                    {(() => {
                      const actualStart = sessionDetail.actualStartTime ? new Date(String(sessionDetail.actualStartTime).replace("Z", "")).getTime() : 0;
                      const bookingStart = booking.startTime ? new Date(String(booking.startTime).replace("Z", "")).getTime() : (sessionDetail.bookingStartTime ? new Date(String(sessionDetail.bookingStartTime).replace("Z", "")).getTime() : 0);
                      const effectiveStartMs = Math.max(actualStart > 0 ? actualStart : 0, bookingStart > 0 ? bookingStart : 0);
                      const end = sessionDetail.actualEndTime ? new Date(String(sessionDetail.actualEndTime).replace("Z", "")).getTime() : 0;
                      
                      const durationMs = effectiveStartMs > 0 && end > 0 && end > effectiveStartMs ? end - effectiveStartMs : (sessionDetail.actualDurationHours ? sessionDetail.actualDurationHours * 3600000 : 0);

                      const renderDate = (ms) => new Date(ms).toLocaleString("vi-VN", { hour: "2-digit", minute: "2-digit", day: "2-digit", month: "2-digit", year: "numeric", hour12: false });
                      
                      return (
                        <>
                          {effectiveStartMs > 0 && <InfoRow label="Bắt đầu sạc" value={renderDate(effectiveStartMs)} />}
                          {end > 0 && <InfoRow label="Kết thúc sạc" value={renderDate(end)} />}
                          {durationMs > 0 && (() => {
                            const hours = Math.floor(durationMs / 3600000);
                            const minutes = Math.floor((durationMs % 3600000) / 60000);
                            const formattedDuration = hours > 0 ? `${hours} giờ ${minutes} phút` : `${minutes} phút`;
                            return <InfoRow label="Thời lượng sạc" value={formattedDuration} />;
                          })()}
                        </>
                      );
                    })()}
                  </div>
                )}

                {booking.disputeDetail && (
                  <div style={{ marginTop: 4, borderTop: "1px dashed #e2e8f0", paddingTop: 12 }}>
                    <div style={{ fontSize: 13, color: "#475569", marginBottom: 6, fontWeight: 700 }}>️ Tranh chấp ({booking.disputeDetail.status})</div>
                    <InfoRow label="Lý do" value={booking.disputeDetail.reason} error />
                    {booking.disputeDetail.resultNote && <InfoRow label="Kết quả" value={booking.disputeDetail.resultNote} highlight />}
                  </div>
                )}
              </div>

              {/* Actions Section */}
              {renderActions()}
            </div>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}

function InfoRow({ label, value, highlight, error, purple }) {
  return (
    <div style={{ display: "flex", justifyContent: "space-between", fontSize: 14, padding: "8px 0", borderBottom: "1px solid #f1f5f9" }}>
      <span style={{ color: "#64748b" }}>{label}</span>
      <span style={{ fontWeight: 600, color: error ? "#ef4444" : purple ? "#7c3aed" : highlight ? "#f97316" : "#1e293b" }}>{value}</span>
    </div>
  );
}

function OwnerCancelSection({ bookingId, onDone }) {
  const [show, setShow] = useState(false);
  const [reason, setReason] = useState("");
  const [loading, setLoading] = useState(false);

  async function handleCancel() {
    setLoading(true);
    try {
      const updated = await bookingApi.ownerCancel(bookingId, reason || "");
      onDone(updated);
      showToast.success("Đã hủy booking — hoàn 100% cho Driver");
    } catch (err) {
      showToast.error(err.message || "Lỗi hủy booking");
    } finally { setLoading(false); }
  }

  if (!show) {
    return (
      <div style={{ marginTop: 20 }}>
        <button
          onClick={() => setShow(true)}
          style={{
            width: "100%", padding: 14, borderRadius: 14,
            border: "2px solid #fca5a5", background: "#fff",
            color: "#ef4444", fontWeight: 700, fontSize: 15, cursor: "pointer",
          }}
        >
           Hủy booking (hoàn tiền 100%)
        </button>
      </div>
    );
  }

  return (
    <div style={{ marginTop: 20, border: "2px solid #fecaca", borderRadius: 16, padding: 20, background: "#fef2f2" }}>
      <p style={{ fontSize: 14, fontWeight: 700, color: "#dc2626", marginBottom: 10 }}>Lý do hủy booking</p>
      <textarea
        value={reason}
        onChange={(e) => setReason(e.target.value)}
        placeholder="Nhập lý do hủy (không bắt buộc)..."
        rows={3}
        style={{ width: "100%", padding: 12, borderRadius: 10, border: "1px solid #fca5a5", fontSize: 14, resize: "vertical", outline: "none", boxSizing: "border-box" }}
      />
      <div style={{ display: "flex", gap: 8, marginTop: 12 }}>
        <button
          onClick={handleCancel} disabled={loading}
          style={{ flex: 1, padding: 12, borderRadius: 10, border: "none", background: loading ? "#d1d5db" : "#ef4444", color: "#fff", fontWeight: 700, fontSize: 14, cursor: loading ? "not-allowed" : "pointer" }}
        >
          {loading ? "Đang hủy..." : "Xác nhận hủy"}
        </button>
        <button
          onClick={() => { setShow(false); setReason(""); }}
          disabled={loading}
          style={{ flex: 1, padding: 12, borderRadius: 10, border: "1px solid #d1d5db", background: "#fff", color: "#6b7280", fontWeight: 600, fontSize: 14, cursor: "pointer" }}
        >
          Không hủy
        </button>
      </div>
    </div>
  );
}

function OwnerDisputeLink({ bookingId, navigate }) {
  const [disputeId, setDisputeId] = useState(null);
  useEffect(() => {
    disputeApi.getByBookingId(bookingId)
      .then((d) => { if (d?.id) setDisputeId(d.id); })
      .catch(() => { });
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
        ️ Xem khiếu nại & Phản hồi
      </button>
    </div>
  );
}
