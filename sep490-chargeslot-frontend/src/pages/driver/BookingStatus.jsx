import { useState, useEffect, useRef } from "react";
import { useParams, useNavigate } from "react-router-dom";
import { bookingApi, paymentApi, walletApi, disputeApi, chargingApi } from "@/services/api";
import { showToast } from "@/components/Toast";
import QRCodeModal from "@/components/QRCodeModal";

const statusStyles = {
  WaitingOwner: { label: "Chờ chủ trạm duyệt", color: "#f59e0b", bg: "#fffbeb", bgGrad: "linear-gradient(135deg, #fef3c7, #fde68a)", icon: "" },
  PendingPayment: { label: "Chờ thanh toán", color: "#3b82f6", bg: "#eff6ff", bgGrad: "linear-gradient(135deg, #dbeafe, #bfdbfe)", icon: "" },
  Paid: { label: "Giữ chỗ", color: "#22c55e", bg: "#f0fdf4", bgGrad: "linear-gradient(135deg, #dcfce7, #bbf7d0)", icon: "" },
  Expired: { label: "Hết hạn", color: "#9ca3af", bg: "#f3f4f6", bgGrad: "linear-gradient(135deg, #f3f4f6, #e5e7eb)", icon: "" },
  Rejected: { label: "Bị từ chối", color: "#ef4444", bg: "#fef2f2", bgGrad: "linear-gradient(135deg, #fecaca, #fca5a5)", icon: "" },
  Cancelled: { label: "Đã hủy", color: "#6b7280", bg: "#f3f4f6", bgGrad: "linear-gradient(135deg, #f3f4f6, #e5e7eb)", icon: "" },
  CheckedIn: { label: "Đã check-in", color: "#06b6d4", bg: "#ecfeff", bgGrad: "linear-gradient(135deg, #cffafe, #a5f3fc)", icon: "" },
  InProgress: { label: "Đang sạc", color: "#06b6d4", bg: "#ecfeff", bgGrad: "linear-gradient(135deg, #cffafe, #a5f3fc)", icon: "" },
  Completed: { label: "Hoàn thành", color: "#8b5cf6", bg: "#f5f3ff", bgGrad: "linear-gradient(135deg, #ede9fe, #ddd6fe)", icon: "" },
  NoShow: { label: "Không đến", color: "#9ca3af", bg: "#f3f4f6", bgGrad: "linear-gradient(135deg, #f3f4f6, #e5e7eb)", icon: "" },
  Disputed: { label: "Tranh chấp", color: "#dc2626", bg: "#fef2f2", bgGrad: "linear-gradient(135deg, #fecaca, #fca5a5)", icon: "️" },
  CompletedPendingInvoice: { label: "Chờ xác nhận hóa đơn", color: "#f97316", bg: "#fff7ed", bgGrad: "linear-gradient(135deg, #fed7aa, #fdba74)", icon: "" },
};

// BE trả về giờ VN (không phải UTC).
// Strip "Z" nếu có, thêm "+07:00" để JS parse đúng timezone VN dù browser ở bất kỳ timezone nào.
const toLocal = (dt) => {
  if (!dt) return "";
  const s = String(dt).trim().replace("Z", "");
  const d = new Date(s.includes("+") || s.includes("-", 10) ? s : s + "+07:00");
  if (isNaN(d)) return "";
  return d.toLocaleString("vi-VN", {
    hour: "2-digit", minute: "2-digit",
    day: "2-digit", month: "2-digit", year: "numeric",
    hour12: false,
  });
};

export default function BookingStatus({ bookingIdParam, onClose }) {
  const { id: routeId } = useParams();
  const id = bookingIdParam || routeId;
  const isEmbedded = !!bookingIdParam;
  const navigate = useNavigate();
  const [booking, setBooking] = useState(null);
  const [loading, setLoading] = useState(true);
  const [payLoading, setPayLoading] = useState(false);
  const [cancelLoading, setCancelLoading] = useState(false);
  const [showCancelForm, setShowCancelForm] = useState(false);
  const [cancelReason, setCancelReason] = useState("");
  const [sessionDetail, setSessionDetail] = useState(null);
  const [sepayOpen, setSepayOpen] = useState(false);
  const [sepayUrl, setSepayUrl] = useState("");
  const [walletReceivedAlert, setWalletReceivedAlert] = useState(false);
  const [showPaymentSuccess, setShowPaymentSuccess] = useState(false);
  const [confirmingInvoice, setConfirmingInvoice] = useState(false); // Xác nhận hóa đơn
  // const [manualCheckinLoading, setManualCheckinLoading] = useState(false); // Manual check-in — TẠM ẨN
  const [showWalletConfirm, setShowWalletConfirm] = useState(false);
  const initialWalletBalanceRef = useRef(0);

  // Các status có phiên sạc thực tế
  const SESSION_STATUSES = ["CheckedIn", "InProgress", "Charging", "CompletedPendingInvoice", "Completed"];

  function fetchBooking() {
    setSessionDetail(null); // Reset để tránh hiện dữ liệu cũ khi switch booking
    bookingApi.getById(Number(id))
      .then(async (data) => {
        setBooking(data);
        if (data && SESSION_STATUSES.includes(data.status)) {
          // Chỉ fetch/set sessionDetail khi booking có phiên sạc thực tế
          if (data.chargingSessionDetail) {
            setSessionDetail(data.chargingSessionDetail);
          } else {
            try {
              const session = await chargingApi.getByBookingId(Number(id));
              setSessionDetail(session);
            } catch { /* ignore */ }
          }
        }
        // Booking Rejected/Cancelled/WaitingOwner... → không có sessionDetail
      })
      .catch(() => setBooking(null))
      .finally(() => setLoading(false));
  }

  useEffect(() => {
    fetchBooking();
    const interval = setInterval(fetchBooking, 10000);
    return () => clearInterval(interval);
  }, [id]);

  useEffect(() => {
    if (!sepayOpen) return;
    // Ghi nhớ số dư ví ban đầu để so sánh
    walletApi.getWallet()
      .then((w) => { initialWalletBalanceRef.current = w?.availableBalance || 0; })
      .catch(() => { });

    const timer = setInterval(() => {
      // Kiểm tra trạng thái booking
      bookingApi.getById(Number(id))
        .then((data) => {
          setBooking(data);
          if (data && data.status === "Paid") {
            setSepayOpen(false);
            setShowPaymentSuccess(true); // Hiện overlay thay vì toast nhỏ
          }
        })
        .catch(() => { });

      // Kiểm tra số dư ví — nếu tăng nhưng booking vẫn PendingPayment có nghĩa tiền vào ví
      walletApi.getWallet()
        .then((w) => {
          const currentBalance = w?.availableBalance || 0;
          if (currentBalance > initialWalletBalanceRef.current) {
            setSepayOpen(false);
            setWalletReceivedAlert(true);
            showToast.error("️ Tiền đã vào ví! Kiểm tra lưu ý bên dưới.");
          }
        })
        .catch(() => { });
    }, 3000);
    return () => clearInterval(timer);
  }, [sepayOpen, id]);

  async function handlePaySepay() {
    setPayLoading(true);
    setWalletReceivedAlert(false); // Reset alert khi thử lại
    try {
      const res = await paymentApi.createSepayUrl(Number(id));
      if (res.qrUrl) {
        setSepayUrl(res.qrUrl);
        setSepayOpen(true);
      } else if (typeof res === 'string') {
        setSepayUrl(res);
        setSepayOpen(true);
      }
    } catch (err) {
      showToast.error(err.message || "Lỗi tạo link thanh toán VietQR");
    } finally { setPayLoading(false); }
  }

  function handlePayWalletClick() {
    if (payLoading) return;
    setShowWalletConfirm(true);
  }

  async function executePayWallet() {
    setPayLoading(true);
    try {
      await walletApi.payBooking(Number(id));
      await fetchBooking();
      setShowWalletConfirm(false);
      setShowPaymentSuccess(true); // Hiện overlay thành công
    } catch (err) {
      const msg = err?.message || "";
      if (msg.includes("500") || msg.includes("Internal")) {
        showToast.error("️ Hệ thống đang bảo trì thanh toán bằng ví. Vui lòng dùng VietQR hoặc thử lại sau.");
      } else {
        showToast.error(msg || "Lỗi thanh toán bằng ví");
      }
    } finally { setPayLoading(false); }
  }

  const [cancelPreview, setCancelPreview] = useState(null);  // { penaltyAmount, refundAmount }
  const [previewLoading, setPreviewLoading] = useState(false);

  async function handleStartCancel() {
    setPreviewLoading(true);
    try {
      const preview = await bookingApi.cancelPreview(Number(id));
      setCancelPreview(preview); // hiện modal cảnh báo phạt
      setShowCancelForm(true);
    } catch {
      // Nếu backend không hỗ trợ preview → mở form cancel thông thường
      setCancelPreview(null);
      setShowCancelForm(true);
    } finally {
      setPreviewLoading(false);
    }
  }

  async function submitCancel() {
    setCancelLoading(true);
    try {
      await bookingApi.driverCancel(Number(id), cancelReason || "");
      setShowCancelForm(false);
      setCancelReason("");
      setCancelPreview(null);
      fetchBooking();
    } catch (err) {
      showToast.error(err.message || "Lỗi hủy booking");
    } finally { setCancelLoading(false); }
  }

  async function handleConfirmInvoice() {
    setConfirmingInvoice(true);
    try {
      // Ưu tiên dùng sessionDetail đã có trong state (tránh gọi API thừa)
      // Nếu chưa có thì mới fallback sang API
      let sessionId = sessionDetail?.id;
      if (!sessionId) {
        const session = await chargingApi.getByBookingId(Number(id));
        if (!session?.id) throw new Error("Không tìm thấy phiên sạc.");
        sessionId = session.id;
      }
      await chargingApi.confirmCompletion(sessionId);
      showToast.success(" Xác nhận hóa đơn thành công!");
      await fetchBooking();
    } catch (err) {
      showToast.error(err?.message || "Lỗi xác nhận hóa đơn");
    } finally {
      setConfirmingInvoice(false);
    }
  }

  // ── Manual check-in (Driver) — TẠM ẨN ──
  // async function handleRequestManualCheckin() {
  //   if (!window.confirm(
  //     "Bạn có chắc muốn gửi yêu cầu xác nhận thủ công?\n" +
  //     "Yêu cầu sẽ gửi tới chủ trạm để họ xác nhận check-in cho bạn."
  //   )) return;
  //   setManualCheckinLoading(true);
  //   try {
  //     await chargingApi.requestManualCheckin(Number(id));
  //     showToast.success("Đã gửi yêu cầu! Chủ trạm sẽ xác nhận check-in cho bạn.");
  //     await fetchBooking();
  //   } catch (err) {
  //     showToast.error(err.message || "Lỗi gửi yêu cầu manual check-in");
  //   } finally {
  //     setManualCheckinLoading(false);
  //   }
  // }

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
        <div style={{ fontSize: 56, marginBottom: 12 }}></div>
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
    <div style={{ minHeight: isEmbedded ? "auto" : "100vh", background: isEmbedded ? "transparent" : "#f8fafc", paddingTop: isEmbedded ? 0 : 84 }}>

      {/* ======= PAYMENT SUCCESS OVERLAY ======= */}
      {showPaymentSuccess && (
        <div style={{
          position: "fixed", inset: 0, zIndex: 9999,
          background: "rgba(0,0,0,0.55)",
          backdropFilter: "blur(6px)",
          display: "flex", alignItems: "center", justifyContent: "center",
          padding: 24,
        }}>
          <div style={{
            background: "#fff", borderRadius: 28,
            padding: "48px 36px", maxWidth: 420, width: "100%",
            textAlign: "center",
            boxShadow: "0 24px 64px rgba(0,0,0,0.25)",
            animation: "popIn 0.35s cubic-bezier(0.34,1.56,0.64,1)",
          }}>
            {/* Icon vòng tròn xanh + check */}
            <div style={{
              width: 96, height: 96, borderRadius: "50%",
              background: "linear-gradient(135deg, #22c55e, #16a34a)",
              display: "flex", alignItems: "center", justifyContent: "center",
              margin: "0 auto 24px",
              boxShadow: "0 8px 32px rgba(34,197,94,0.4)",
              animation: "scaleIn 0.4s cubic-bezier(0.34,1.56,0.64,1) 0.1s both",
            }}>
              <svg width="48" height="48" viewBox="0 0 24 24" fill="none" stroke="#fff" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                <path d="M20 6L9 17l-5-5" />
              </svg>
            </div>

            <h2 style={{ fontSize: 26, fontWeight: 900, color: "#15803d", margin: "0 0 8px" }}>
              Thanh toán thành công!
            </h2>
            <p style={{ fontSize: 14, color: "#64748b", margin: "0 0 8px" }}>
              Hệ thống đã nhận tiền và xác nhận booking.
            </p>

            {/* Số tiền */}
            {booking?.totalAmount && (
              <div style={{
                background: "linear-gradient(135deg, #f0fdf4, #dcfce7)",
                borderRadius: 16, padding: "16px 24px", margin: "20px 0",
                border: "1.5px solid #86efac",
              }}>
                <div style={{ fontSize: 13, color: "#166534", fontWeight: 600, marginBottom: 4 }}>Số tiền đã thanh toán</div>
                <div style={{ fontSize: 32, fontWeight: 900, color: "#15803d" }}>
                  {booking.totalAmount.toLocaleString("vi-VN")}đ
                </div>
              </div>
            )}

            {/* Thông tin booking */}
            {booking?.stationName && (
              <div style={{ fontSize: 13, color: "#475569", marginBottom: 24, lineHeight: 1.6 }}>
                <div> <strong>{booking.stationName}</strong></div>
                {booking.slotName && <div> Slot: {booking.slotName}</div>}
              </div>
            )}

            {/* Actions */}
            <div style={{ display: "flex", flexDirection: "column", gap: 10 }}>
              <button
                onClick={() => {
                  setShowPaymentSuccess(false);
                  if (isEmbedded && onClose) onClose();
                  else navigate("/driver/my-bookings");
                }}
                style={{
                  width: "100%", padding: "14px 0", borderRadius: 14, border: "none",
                  background: "linear-gradient(135deg, #22c55e, #16a34a)",
                  color: "#fff", fontWeight: 700, fontSize: 15, cursor: "pointer",
                  boxShadow: "0 4px 14px rgba(34,197,94,0.35)",
                }}
              >
                Xem danh sách booking
              </button>
              <button
                onClick={() => setShowPaymentSuccess(false)}
                style={{
                  width: "100%", padding: "12px 0", borderRadius: 14,
                  border: "1.5px solid #e2e8f0", background: "#f8fafc",
                  color: "#64748b", fontWeight: 600, fontSize: 14, cursor: "pointer",
                }}
              >
                Đóng
              </button>
            </div>
          </div>
          <style>{`
            @keyframes popIn {
              from { opacity: 0; transform: scale(0.85) translateY(20px); }
              to   { opacity: 1; transform: scale(1) translateY(0); }
            }
            @keyframes scaleIn {
              from { transform: scale(0); }
              to   { transform: scale(1); }
            }
          `}</style>
        </div>
      )}
      {/* ======================================= */}
      <div style={{ maxWidth: 600, margin: "0 auto", padding: "0 16px 40px" }}>

        {/* Back button */}
        <button
          onClick={() => {
            if (isEmbedded && onClose) onClose();
            else navigate("/driver/my-bookings");
          }}
          style={{
            background: "none", border: "none", cursor: "pointer",
            color: "#64748b", fontSize: 13, marginBottom: 16,
            display: "flex", alignItems: "center", gap: 6,
            fontWeight: 500,
          }}
        >
          <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><path d="M19 12H5M12 19l-7-7 7-7" /></svg>
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
            Chi tiết booking
          </h3>

          <div style={{ display: "flex", flexDirection: "column", gap: 0 }}>
            <InfoRow icon="" label="Slot" value={booking.slotName} />
            <InfoRow icon="" label="Bắt đầu" value={toLocal(booking.startTime)} />
            <InfoRow icon="" label="Kết thúc" value={toLocal(booking.endTime)} />
            <InfoRow icon="" label="Thời lượng" value={`${Math.round(booking.durationHours * 60)} phút`} />

            {/* Phí sạc & dịch vụ tách riêng khi có extras */}
            {booking.extraServices && booking.extraServices.length > 0 ? (
              <>
                <InfoRow icon="" label="Phí sạc" value={`${((booking.totalAmount || 0) - (booking.serviceAmount || 0)).toLocaleString("vi-VN")}đ`} />
                <InfoRow icon="" label="Phí dịch vụ" value={`${(booking.serviceAmount || 0).toLocaleString("vi-VN")}đ`} purple />
                <InfoRow icon="" label="Tổng cộng" value={`${(booking.totalAmount || 0).toLocaleString("vi-VN")}đ`} highlight />
              </>
            ) : (
              <InfoRow icon="" label="Tổng tiền" value={`${(booking.totalAmount || 0).toLocaleString("vi-VN")}đ`} highlight />
            )}

            {booking.note && <InfoRow icon="" label="Ghi chú" value={booking.note} />}
            {booking.rejectionReason && <InfoRow icon="" label="Lý do từ chối" value={booking.rejectionReason} error />}
            {booking.cancelReason && <InfoRow icon="" label="Lý do hủy" value={booking.cancelReason} error />}
            {booking.paymentExpiresAt && booking.status === "PendingPayment" && (
              <InfoRow icon="" label="Hạn thanh toán" value={toLocal(booking.paymentExpiresAt)} warning />
            )}

            {/* DEEP DETAILS */}
            {booking.paymentDetail && (
              <div style={{ marginTop: 12, borderTop: "1px dashed #e2e8f0", paddingTop: 12 }}>
                <h4 style={{ fontSize: 13, color: "#475569", marginBottom: 8, fontWeight: 700 }}>Chi tiết thanh toán</h4>
                <InfoRow icon="" label="Phương thức" value={booking.paymentDetail.method === "Wallet" ? "Ví hệ thống" : booking.paymentDetail.method === "BankTransfer" ? "Chuyển khoản" : booking.paymentDetail.method} />
                {booking.paymentDetail.gatewayTxnRef && <InfoRow icon="" label="Mã giao dịch" value={booking.paymentDetail.gatewayTxnRef} />}
                {booking.paymentDetail.paidAt && <InfoRow icon="" label="Ngày thanh toán" value={toLocal(booking.paymentDetail.paidAt)} />}
                {booking.paymentDetail.refundedAt && <InfoRow icon="" label="Ngày hoàn tiền" value={toLocal(booking.paymentDetail.refundedAt)} error />}
              </div>
            )}

            {booking.invoiceDetail && (
              <div style={{ marginTop: 12, borderTop: "1px dashed #e2e8f0", paddingTop: 12 }}>
                <h4 style={{ fontSize: 13, color: "#475569", marginBottom: 8, fontWeight: 700 }}>Chi tiết hóa đơn</h4>
                <InfoRow icon="" label="Tiền sạc" value={`${(booking.invoiceDetail.chargingAmount || 0).toLocaleString("vi-VN")}đ`} />
                {booking.invoiceDetail.vatAmount > 0 && <InfoRow icon="" label="Thuế VAT" value={`${booking.invoiceDetail.vatAmount.toLocaleString("vi-VN")}đ`} />}
                <InfoRow icon="" label="Tổng thanh toán" value={`${(booking.invoiceDetail.totalAmount || 0).toLocaleString("vi-VN")}đ`} highlight />
              </div>
            )}

            {sessionDetail && SESSION_STATUSES.includes(booking.status) && (
              <div style={{ marginTop: 12, borderTop: "1px dashed #e2e8f0", paddingTop: 12 }}>
                <h4 style={{ fontSize: 13, color: "#475569", marginBottom: 8, fontWeight: 700 }}>Chi tiết phiên sạc thực tế</h4>
                {(() => {
                  // BE trả về giờ VN (không phải UTC).
                  // Strip "Z" + thêm "+07:00" để JS parse đúng bất kể browser ở timezone nào.
                  const parseVN = (dt) => {
                    if (!dt) return 0;
                    const s = String(dt).trim().replace("Z", "");
                    const d = new Date(s.includes("+") || s.includes("-", 10) ? s : s + "+07:00");
                    return isNaN(d) ? 0 : d.getTime();
                  };

                  const renderDate = (ms) => new Date(ms).toLocaleString("vi-VN", {
                    hour: "2-digit", minute: "2-digit",
                    day: "2-digit", month: "2-digit", year: "numeric",
                    hour12: false,
                  });

                  // actualStartTime = lúc driver CHECK-IN (có thể trước giờ sạc)
                  // bookingStartTime = giờ phiên sạc bắt đầu theo lịch
                  // "Bắt đầu sạc" = max(checkIn, bookingStart) — sạc chỉ bắt đầu khi đến giờ lịch
                  const checkInMs = parseVN(sessionDetail.actualStartTime);
                  const bookingStartMs = parseVN(sessionDetail.bookingStartTime) || parseVN(booking.startTime);
                  const chargingStartMs = (checkInMs > 0 && bookingStartMs > 0)
                    ? Math.max(checkInMs, bookingStartMs)
                    : (bookingStartMs > 0 ? bookingStartMs : checkInMs);

                  const endMs = parseVN(sessionDetail.actualEndTime);

                  // Thời lượng: LUÔN tính từ chargingStartMs đến endMs để nhất quán với giờ hiển thị.
                  // KHÔNG dùng actualDurationHours từ BE vì BE tính từ checkIn time (khác chargingStart).
                  const durationMs = (chargingStartMs > 0 && endMs > 0 && endMs > chargingStartMs)
                    ? endMs - chargingStartMs
                    : 0;

                  // Không hiển thị nếu không có dữ liệu có ý nghĩa
                  if (chargingStartMs === 0 && endMs === 0) return null;

                  return (
                    <>
                      {chargingStartMs > 0 && <InfoRow icon="" label="Bắt đầu sạc" value={renderDate(chargingStartMs)} />}
                      {endMs > 0 && <InfoRow icon="" label="Kết thúc sạc" value={renderDate(endMs)} />}
                      {durationMs > 0 && (() => {
                        const hours = Math.floor(durationMs / 3600000);
                        const minutes = Math.floor((durationMs % 3600000) / 60000);
                        return <InfoRow icon="" label="Thời lượng sạc" value={hours > 0 ? `${hours} giờ ${minutes} phút` : `${minutes} phút`} />;
                      })()}
                    </>
                  );
                })()}
              </div>
            )}

            {booking.disputeDetail && (
              <div style={{ marginTop: 12, borderTop: "1px dashed #e2e8f0", paddingTop: 12 }}>
                <h4 style={{ fontSize: 13, color: "#475569", marginBottom: 8, fontWeight: 700 }}>Tranh chấp ({booking.disputeDetail.status})</h4>
                <InfoRow icon="️" label="Lý do" value={booking.disputeDetail.reason} error />
                {booking.disputeDetail.resultNote && <InfoRow icon="️" label="Kết quả xử lý" value={booking.disputeDetail.resultNote} highlight />}
              </div>
            )}
          </div>
        </div>

        {/* Extra Services breakdown */}
        {booking.extraServices && booking.extraServices.length > 0 && (
          <div style={{
            background: "#fff", borderRadius: 20, padding: 24,
            boxShadow: "0 2px 12px rgba(0,0,0,0.06)", marginBottom: 16,
          }}>
            <h3 style={{ fontSize: 14, fontWeight: 700, color: "#374151", marginBottom: 12, display: "flex", alignItems: "center", gap: 6 }}>
              Dịch vụ bổ sung
            </h3>
            <div style={{ display: "flex", flexDirection: "column", gap: 0 }}>
              {booking.extraServices.map((es, idx) => (
                <div key={idx} style={{
                  display: "flex", justifyContent: "space-between", alignItems: "center",
                  padding: "10px 0", borderBottom: "1px solid #f8fafc",
                }}>
                  <div style={{ flex: 1 }}>
                    <div style={{ fontSize: 14, fontWeight: 600, color: "#1e293b" }}>{es.serviceName}</div>
                    <div style={{ fontSize: 12, color: "#94a3b8" }}>
                      {es.unitPrice?.toLocaleString("vi-VN")}đ × {es.quantity}
                    </div>
                  </div>
                  <span style={{ fontWeight: 700, color: "#7c3aed", fontSize: 14 }}>
                    {es.totalPrice?.toLocaleString("vi-VN")}đ
                  </span>
                </div>
              ))}
              <div style={{
                display: "flex", justifyContent: "space-between", alignItems: "center",
                padding: "10px 0", borderTop: "1px dashed #e5e7eb", marginTop: 4,
              }}>
                <span style={{ fontSize: 13, fontWeight: 700, color: "#374151" }}>Tổng dịch vụ</span>
                <span style={{ fontWeight: 800, color: "#7c3aed", fontSize: 15 }}>
                  {(booking.serviceAmount || booking.extraServices.reduce((s, e) => s + (e.totalPrice || 0), 0)).toLocaleString("vi-VN")}đ
                </span>
              </div>
            </div>
          </div>
        )}

        {/* Wallet Received Alert — tiền vào ví thay vì xác nhận booking */}
        {walletReceivedAlert && booking.status === "PendingPayment" && (
          <div style={{
            background: "linear-gradient(135deg, #fffbeb, #fef3c7)",
            border: "2px solid #f59e0b",
            borderRadius: 16,
            padding: "16px 20px",
            marginBottom: 16,
          }}>
            <div style={{ display: "flex", alignItems: "flex-start", gap: 10 }}>
              <span style={{ fontSize: 24, flexShrink: 0 }}></span>
              <div>
                <div style={{ fontWeight: 700, fontSize: 14, color: "#92400e", marginBottom: 6 }}>
                  Tiền đã vào ví ChargeSlot của bạn!
                </div>
                <div style={{ fontSize: 12, color: "#92400e", lineHeight: 1.6 }}>
                  Hệ thống nhận được chuyển khoản nhưng <strong>không khớp với booking này</strong>
                  (sai số tiền hoặc sai nội dung). Tiền đã được bảo toàn trong ví của bạn.
                </div>
                <div style={{ fontSize: 12, color: "#92400e", marginTop: 6 }}>
                  Ồ <strong>Thanh toán bằng ví:</strong> Bấm nút "Thanh toán bằng ví" bên dưới để dùng số tiền trong ví đã nạp xác nhận booking ngay.
                </div>
              </div>
            </div>
          </div>
        )}

        {/* Chat button — ẩn khi booking đã kết thúc */}
        {!["Cancelled", "Rejected", "Expired", "NoShow", "Completed", "CompletedPendingInvoice"].includes(booking.status) && (
          <button
            onClick={() => navigate(`/driver/chat/${booking.id}`)}
            style={{
              width: "100%", padding: "14px 0", borderRadius: 14,
              border: "2px solid #3b82f6", background: "#eff6ff",
              color: "#3b82f6", fontWeight: 700, fontSize: 14,
              cursor: "pointer", marginBottom: 16, transition: "all 0.2s",
              display: "flex", alignItems: "center", justifyContent: "center", gap: 8,
            }}
            onMouseEnter={e => { e.currentTarget.style.background = "#dbeafe"; }}
            onMouseLeave={e => { e.currentTarget.style.background = "#eff6ff"; }}
          >
            Chat với chủ trạm
          </button>
        )}

        {/* Action cards */}
        {booking.status === "PendingPayment" && (
          <div style={{
            background: "#fff", borderRadius: 20, padding: 24,
            boxShadow: "0 2px 12px rgba(0,0,0,0.06)", marginBottom: 16,
          }}>
            <h3 style={{ fontSize: 14, fontWeight: 700, color: "#374151", marginBottom: 12, display: "flex", alignItems: "center", gap: 6 }}>
              Thanh toán
            </h3>
            <div style={{ display: "flex", flexDirection: "column", gap: 10 }}>
              <ActionButton
                onClick={handlePaySepay}
                disabled={payLoading}
                bg="linear-gradient(135deg, #0284c7, #0369a1)"
                shadow="rgba(2,132,199,0.25)"
              >
                {payLoading ? "Đang xử lý..." : " Thanh toán VietQR"}
              </ActionButton>
              
              {!showWalletConfirm ? (
                <ActionButton
                  onClick={handlePayWalletClick}
                  disabled={payLoading}
                  bg="linear-gradient(135deg, #f97316, #ea580c)"
                  shadow="rgba(249,115,22,0.25)"
                >
                  {payLoading ? "Đang xử lý..." : " Thanh toán bằng ví"}
                </ActionButton>
              ) : (
                <div style={{ background: "#fff", borderRadius: 20, padding: 20, boxShadow: "0 4px 20px rgba(0,0,0,0.08)", border: "1.5px solid #f97316" }}>
                  <h4 style={{ margin: "0 0 8px", fontSize: 16, fontWeight: 800, color: "#1e293b", textAlign: "center" }}>Xác nhận thanh toán</h4>
                  <p style={{ fontSize: 13, color: "#475569", marginBottom: 16, textAlign: "center", lineHeight: 1.5 }}>
                    Bạn sẽ trích <strong>{(booking?.totalAmount || 0).toLocaleString("vi-VN")}đ</strong> từ Ví ChargeSlot để thanh toán cho phiên sạc này.
                  </p>
                  <div style={{ display: "flex", gap: 10 }}>
                    <button
                      onClick={() => setShowWalletConfirm(false)}
                      disabled={payLoading}
                      style={{ flex: 1, padding: "12px 0", borderRadius: 12, border: "1px solid #cbd5e1", background: "#f8fafc", color: "#475569", fontWeight: 700, cursor: payLoading ? "not-allowed" : "pointer" }}
                    >
                      Hủy bỏ
                    </button>
                    <button
                      onClick={executePayWallet}
                      disabled={payLoading}
                      style={{ flex: 1, padding: "12px 0", borderRadius: 12, border: "none", background: "linear-gradient(135deg, #f97316, #ea580c)", color: "#fff", fontWeight: 700, cursor: payLoading ? "not-allowed" : "pointer" }}
                    >
                      {payLoading ? "Đang xử lý..." : "Xác nhận trả"}
                    </button>
                  </div>
                </div>
              )}
            </div>
          </div>
        )}
        {/* ── Manual check-in card (Driver) — TẠM ẨN ──
        {booking.status === "Paid" && (() => {
          const start = booking.startTime
            ? new Date(String(booking.startTime).replace("Z", "") + "+07:00").getTime()
            : 0;
          const now = Date.now();
          const checkInWindowMs = 15 * 60 * 1000;
          const canCheckIn = start > 0 && now >= start - checkInWindowMs;
          if (!canCheckIn) return null;
          return (
            <div style={{
              background: "#fffbeb",
              border: "2px solid #f59e0b",
              borderRadius: 16, padding: "16px 20px", marginBottom: 16,
            }}>
              <div style={{ fontWeight: 700, fontSize: 14, color: "#92400e", marginBottom: 8 }}>
                Quét QR bị lỗi? Yêu cầu xác nhận thủ công
              </div>
              <div style={{ fontSize: 13, color: "#92400e", marginBottom: 12, lineHeight: 1.6 }}>
                Nếu không quét được QR code tại sốt, hãy yêu cầu chủ trạm xác nhận check-in thủ công cho bạn.
              </div>
              <button
                onClick={handleRequestManualCheckin}
                disabled={manualCheckinLoading}
                style={{
                  width: "100%", padding: "12px 0", borderRadius: 12, border: "none",
                  background: manualCheckinLoading ? "#d1d5db" : "linear-gradient(135deg, #f59e0b, #d97706)",
                  color: "#fff", fontWeight: 700, fontSize: 14,
                  cursor: manualCheckinLoading ? "not-allowed" : "pointer",
                }}
              >
                {manualCheckinLoading ? "Đang gửi yêu cầu..." : " Yêu cầu xác nhận thủ công"}
              </button>
            </div>
          );
        })()} */}

        {/* Charging session link */}
        {(booking.status === "CheckedIn" || booking.status === "InProgress") && (
          <ActionButton
            onClick={() => {
              const uId = localStorage.getItem("userId") || "guest";
              localStorage.setItem(`activeChargingBooking_${uId}`, String(booking.id));
              navigate("/driver/charging");
            }}
            bg="linear-gradient(135deg, #3b82f6, #2563eb)"
            shadow="rgba(59,130,246,0.25)"
            style={{ marginBottom: 16 }}
          >
            Xem phiên sạc
          </ActionButton>
        )}

        {/* Cancel section */}
        {(booking.status === "Paid" || booking.status === "WaitingOwner" || booking.status === "PendingPayment") && (
          <div style={{ marginBottom: 16 }}>
            {!showCancelForm ? (
              <button
                onClick={handleStartCancel}
                disabled={previewLoading}
                style={{
                  width: "100%", padding: "14px 0", borderRadius: 14,
                  border: "2px solid #fca5a5", background: "#fff",
                  color: "#ef4444", fontWeight: 700, fontSize: 14,
                  cursor: previewLoading ? "not-allowed" : "pointer", transition: "all 0.2s",
                  opacity: previewLoading ? 0.6 : 1,
                }}
                onMouseEnter={(e) => { if (!previewLoading) e.currentTarget.style.background = "#fef2f2"; }}
                onMouseLeave={(e) => { e.currentTarget.style.background = "#fff"; }}
              >
                {previewLoading ? "Đang kiểm tra phí hủy..." : " Hủy booking"}
              </button>
            ) : (
              <div style={{ background: "#fff", borderRadius: 24, padding: "28px 24px", boxShadow: "0 10px 40px rgba(0,0,0,0.1)", border: "1px solid #e2e8f0" }}>
                <div style={{ width: 64, height: 64, borderRadius: "50%", background: "#fef2f2", color: "#ef4444", fontSize: 32, display: "flex", alignItems: "center", justifyContent: "center", margin: "0 auto 16px" }}>
                  ️
                </div>
                <h3 style={{ margin: "0 0 8px", fontSize: 20, fontWeight: 800, color: "#1e293b", textAlign: "center" }}>Xác nhận hủy</h3>
                <p style={{ fontSize: 13, color: "#64748b", marginBottom: 24, textAlign: "center", lineHeight: 1.5 }}>
                  Logic hoàn tiền được xử lý minh bạch. Bạn vui lòng xem kỹ chi phí trước khi xác nhận hủy.
                </p>

                {cancelPreview && booking && (
                  <div style={{ background: "#f8fafc", border: "1.5px solid #e2e8f0", borderRadius: 16, padding: "16px", marginBottom: 20 }}>
                    <div style={{ display: "flex", justifyContent: "space-between", marginBottom: 12, fontSize: 14, color: "#475569" }}>
                      <span>Tiền đã thanh toán:</span>
                      <span style={{ fontWeight: 600 }}>{booking.totalAmount?.toLocaleString("vi-VN")}đ</span>
                    </div>
                    <div style={{ display: "flex", justifyContent: "space-between", marginBottom: 16, fontSize: 14, color: "#dc2626" }}>
                      <span>Phí phạt hủy:</span>
                      <span style={{ fontWeight: 700 }}>- {cancelPreview.penaltyAmount?.toLocaleString("vi-VN")}đ</span>
                    </div>
                    <div style={{ borderTop: "1px dashed #cbd5e1", paddingTop: 16, display: "flex", justifyContent: "space-between", alignItems: "center" }}>
                      <span style={{ fontSize: 14, fontWeight: 700, color: "#0f172a" }}>Thực nhận lại:</span>
                      <span style={{ fontSize: 18, fontWeight: 800, color: "#16a34a" }}>{cancelPreview.refundAmount?.toLocaleString("vi-VN")}đ</span>
                    </div>
                    {cancelPreview.penaltyAmount === 0 && (
                      <div style={{ fontSize: 12, color: "#166534", marginTop: 12, textAlign: "center", fontWeight: 600, background: "#dcfce7", padding: "6px 10px", borderRadius: 8 }}>
                        Không có phí phạt. Hoàn tiền 100%.
                      </div>
                    )}
                  </div>
                )}

                <div style={{ marginBottom: 20 }}>
                  <p style={{ fontSize: 13, fontWeight: 700, color: "#374151", marginBottom: 8 }}>Lý do hủy (không bắt buộc)</p>
                  <textarea
                    value={cancelReason}
                    onChange={(e) => setCancelReason(e.target.value)}
                    placeholder="Chia sẻ lý do bạn hủy..."
                    rows={2}
                    style={{
                      width: "100%", padding: "12px", borderRadius: 12,
                      border: "1.5px solid #cbd5e1", fontSize: 14, background: "#f8fafc",
                      resize: "vertical", outline: "none", boxSizing: "border-box", transition: "all 0.2s"
                    }}
                    onFocus={e => { e.target.style.borderColor = "#94a3b8"; e.target.style.background = "#fff" }}
                    onBlur={e => { e.target.style.borderColor = "#cbd5e1"; e.target.style.background = "#f8fafc" }}
                  />
                </div>

                <div style={{ display: "flex", gap: 12 }}>
                  <button
                    onClick={() => { setShowCancelForm(false); setCancelReason(""); setCancelPreview(null); }}
                    disabled={cancelLoading}
                    style={{ flex: 1, padding: "12px 0", borderRadius: 12, border: "1px solid #cbd5e1", background: "#f8fafc", color: "#475569", fontWeight: 700, cursor: cancelLoading ? "not-allowed" : "pointer" }}
                  >
                    Đóng
                  </button>
                  <button
                    onClick={submitCancel}
                    disabled={cancelLoading}
                    style={{ flex: 1, padding: "12px 0", borderRadius: 12, border: "none", background: "#ef4444", color: "#fff", fontWeight: 700, cursor: cancelLoading ? "not-allowed" : "pointer" }}
                  >
                    {cancelLoading ? "Đang hủy..." : "Xác nhận hủy"}
                  </button>
                </div>
              </div>
            )}
          </div>
        )}

        {/* Xác nhận hóa đơn — hiện ngay khi Owner đã dừng phiên sạc */}
        {booking.status === "CompletedPendingInvoice" && (
          <div style={{
            background: "linear-gradient(135deg, #fff7ed, #fed7aa)",
            border: "2px solid #f97316",
            borderRadius: 20, padding: "20px 24px",
            marginBottom: 16,
          }}>
            <div style={{ display: "flex", alignItems: "flex-start", gap: 10, marginBottom: 16 }}>
              <span style={{ fontSize: 28, flexShrink: 0 }}></span>
              <div>
                <div style={{ fontWeight: 700, fontSize: 15, color: "#9a3412", marginBottom: 4 }}>
                  Phiên sạc đã kết thúc — Chờ xác nhận hóa đơn
                </div>
                <div style={{ fontSize: 13, color: "#c2410c", lineHeight: 1.6 }}>
                  Vui lòng xác nhận hóa đơn để hoàn tất.
                  <br /><strong>Nếu có vấn đề → dùng nút Khiếu nại bên dưới.</strong>
                </div>
              </div>
            </div>
            <ActionButton
              onClick={handleConfirmInvoice}
              disabled={confirmingInvoice}
              bg="linear-gradient(135deg, #f97316, #ea580c)"
              shadow="rgba(249,115,22,0.3)"
            >
              {confirmingInvoice ? (
                <span style={{ display: "flex", alignItems: "center", justifyContent: "center", gap: 8 }}>
                  <span style={{ width: 16, height: 16, border: "2px solid rgba(255,255,255,0.4)", borderTopColor: "#fff", borderRadius: "50%", display: "inline-block", animation: "spin 1s linear infinite" }} />
                  Đang xác nhận...
                </span>
              ) : " Xác nhận hóa đơn"}
            </ActionButton>
          </div>
        )}

        {/* Dispute — chỉ trong vòng 24h sau khi kết thúc */}
        {(booking.status === "CompletedPendingInvoice" || booking.status === "Completed") && (() => {
          const endMs = booking.endTime ? new Date(String(booking.endTime).replace("Z", "")).getTime() : 0;
          const within24h = endMs && (Date.now() - endMs) < 24 * 60 * 60 * 1000;
          if (!within24h) return null;
          return (
            <ActionButton
              onClick={() => navigate(`/driver/dispute/submit/${booking.id}`)}
              bg="linear-gradient(135deg, #dc2626, #b91c1c)"
              shadow="rgba(220,38,38,0.25)"
              style={{ marginBottom: 16 }}
            >
              ️ Khiếu nại
            </ActionButton>
          );
        })()}

        {booking.status === "Disputed" && (
          <DisputeLink bookingId={booking.id} navigate={navigate} />
        )}
      </div>

      <QRCodeModal
        isOpen={sepayOpen}
        onClose={() => setSepayOpen(false)}
        qrUrl={sepayUrl}
        title="Thanh toán VietQR"
        amount={booking.totalAmount}
        isBookingPayment={true}
      />
    </div>
  );
}

function InfoRow({ icon, label, value, highlight, error, warning, purple }) {
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
        color: error ? "#ef4444" : warning ? "#f59e0b" : purple ? "#7c3aed" : highlight ? "#f97316" : "#1e293b",
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
      .catch(() => { });
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
      ️ Xem khiếu nại
    </button>
  );
}
