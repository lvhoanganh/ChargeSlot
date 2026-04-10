import { useEffect, useState } from "react";
import { useLocation, useNavigate } from "react-router-dom";
import { chargingApi } from "@/services/api";

const toLocalDate = (dt) => {
  if (!dt) return null;
  const s = String(dt).trim();
  // Nếu có Z (UTC) → giữ nguyên, JS tự convert sang local
  // Nếu không có offset → BE trả giờ VN Unspecified → thêm +07:00
  if (s.endsWith("Z") || /[+-]\d{2}:\d{2}$/.test(s)) return new Date(s);
  return new Date(s + "+07:00");
};

const toLocal = (dt) => {
  const d = toLocalDate(dt);
  if (!d || isNaN(d)) return "—";
  return d.toLocaleString("vi-VN");
};

function formatDuration(s) {
  if (!s || s <= 0) return "—";
  const h = Math.floor(s / 3600);
  const m = Math.floor((s % 3600) / 60);
  return h > 0 ? `${h} giờ ${m} phút` : `${m} phút`;
}

function formatCurrency(a) {
  return new Intl.NumberFormat("vi-VN", { style: "currency", currency: "VND" }).format(a || 0);
}

export default function ChargingComplete() {
  const navigate = useNavigate();
  const location = useLocation();
  const sessionFromState = location.state?.session;

  const [session, setSession] = useState(sessionFromState || null);
  const [invoice, setInvoice] = useState(null);
  const [loading, setLoading] = useState(true);
  const [confirming, setConfirming] = useState(false);
  const [confirmed, setConfirmed] = useState(false);
  const [error, setError] = useState("");

  useEffect(() => {
    async function loadData() {
      try {
        // If we have session, try to load invoice
        if (sessionFromState?.bookingId) {
          const inv = await chargingApi.getInvoice(sessionFromState.bookingId).catch(() => null);
          setInvoice(inv);
        }

        // If session is already completed, mark as confirmed
        if (sessionFromState?.bookingStatus === "Completed") {
          setConfirmed(true);
        }
      } catch { /* ignore */ }
      setLoading(false);
    }
    loadData();
  }, []);

  async function handleConfirm() {
    if (!session) return;
    setConfirming(true);
    setError("");
    try {
      await chargingApi.confirmCompletion(session.id);
      setConfirmed(true);
    } catch (err) {
      setError(err?.message || "Lỗi xác nhận, vui lòng thử lại!");
    } finally {
      setConfirming(false);
    }
  }

  if (loading) {
    return (
      <div className="min-h-[calc(100vh-64px)] px-4 py-10 pt-24 flex items-center justify-center" style={{ background: "linear-gradient(135deg, #f8fafc 0%, #e8ecf1 100%)" }}>
        <div className="text-center">
          <div className="w-12 h-12 border-4 border-green-200 border-t-green-500 rounded-full animate-spin mx-auto mb-4" />
          <p className="text-gray-500">Đang tải hóa đơn...</p>
        </div>
      </div>
    );
  }

  if (!session) {
    return (
      <div className="min-h-[calc(100vh-64px)] px-4 py-10 pt-24 flex items-center justify-center" style={{ background: "linear-gradient(135deg, #f8fafc 0%, #e8ecf1 100%)" }}>
        <div className="max-w-md w-full rounded-2xl bg-white shadow-xl p-8 text-center">
          <div className="w-16 h-16 rounded-full bg-red-100 flex items-center justify-center mx-auto mb-4">
            <svg className="w-8 h-8 text-red-500" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-2.5L13.732 4c-.77-.833-1.964-.833-2.732 0L3.732 16.5c-.77.833.192 2.5 1.732 2.5z" />
            </svg>
          </div>
          <h2 className="text-lg font-bold text-gray-800 mb-2">Không tìm thấy phiên sạc</h2>
          <button onClick={() => navigate("/driver/my-bookings")} className="w-full h-12 bg-orange-500 hover:bg-orange-600 text-white font-semibold rounded-xl cursor-pointer transition-all mt-4">
            Về danh sách booking
          </button>
        </div>
      </div>
    );
  }

  // Tính thời gian sạc đúng:
  // effectiveStart = max(actualStartTime, bookingStartTime)
  // → loại trừ thời gian chờ trước giờ đặt lịch
  const actualStartDate   = toLocalDate(session.actualStartTime);
  const bookingStartDate  = toLocalDate(session.bookingStartTime);
  const actualEndDate     = toLocalDate(session.actualEndTime);

  // Giờ bắt đầu tính cước = max(actualStart, bookingStart)
  const effectiveStartMs = Math.max(
    actualStartDate  && !isNaN(actualStartDate)  ? actualStartDate.getTime()  : 0,
    bookingStartDate && !isNaN(bookingStartDate) ? bookingStartDate.getTime() : 0,
  );
  const effectiveEndMs = actualEndDate && !isNaN(actualEndDate) ? actualEndDate.getTime() : 0;

  // Duration chính xác (giây) — không dùng actualDurationHours của BE vì BE thiếu sót
  const durationSec = effectiveStartMs && effectiveEndMs && effectiveEndMs > effectiveStartMs
    ? Math.floor((effectiveEndMs - effectiveStartMs) / 1000)
    : (session.actualDurationHours ? session.actualDurationHours * 3600 : 0);

  return (
    <div className="min-h-[calc(100vh-64px)] px-4 py-10 pt-24" style={{ background: "linear-gradient(135deg, #f8fafc 0%, #f1f5f9 50%, #e8ecf1 100%)" }}>
      <div className="max-w-md mx-auto">
        {/* Success banner */}
        <div className="rounded-2xl overflow-hidden shadow-xl mb-6" style={{ background: "linear-gradient(135deg, #22c55e 0%, #16a34a 100%)" }}>
          <div className="px-8 py-10 flex flex-col items-center text-center">
            <div className="w-20 h-20 rounded-full bg-white/20 flex items-center justify-center mb-4">
              <svg className="w-10 h-10 text-white" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z" />
              </svg>
            </div>
            <h1 className="text-2xl font-bold text-white mb-1">Sạc hoàn tất!</h1>
            <p className="text-white/80 text-sm">Cảm ơn bạn đã sử dụng dịch vụ ChargeSlot</p>
          </div>
        </div>

        {/* Stats */}
        <div className="mb-6">
          <div className="rounded-xl bg-white shadow-lg p-5 text-center">
            <span className="text-3xl">⏱️</span>
            <p className="text-2xl font-bold text-gray-800 mt-2">{formatDuration(durationSec)}</p>
            <p className="text-xs text-gray-500 mt-1">Thời gian sạc</p>
          </div>
        </div>

        {/* Invoice */}
        <div className="rounded-2xl bg-white shadow-lg overflow-hidden mb-6">
          <div className="px-6 py-4 border-b border-gray-100">
            <h2 className="text-sm font-bold text-gray-700">🧾 Hóa đơn</h2>
          </div>
          <div className="px-6 py-5 space-y-3">
            <InfoRow label="Mã booking" value={`#${session.bookingId}`} />
            <InfoRow label="Trạm sạc" value={session.stationName || "—"} />
            <InfoRow label="Cổng sạc" value={session.slotName || `Slot ${session.slotId}`} />
            <InfoRow
              label="Bắt đầu sạc"
              value={effectiveStartMs ? new Date(effectiveStartMs).toLocaleString("vi-VN") : "—"}
            />
            <InfoRow label="Kết thúc" value={toLocal(session.actualEndTime || session.bookingEndTime)} />
            <div className="border-t border-gray-100 pt-3">
              <div className="flex items-center justify-between">
                <span className="text-sm font-bold text-gray-800">Tổng tiền</span>
                <span className="text-lg font-bold text-orange-600">{formatCurrency(invoice?.totalAmount || session.totalAmount)}</span>
              </div>
              <p className="text-xs text-green-600 mt-1 flex items-center gap-1">
                <svg className="w-3.5 h-3.5" fill="currentColor" viewBox="0 0 20 20">
                  <path fillRule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zm3.707-9.293a1 1 0 00-1.414-1.414L9 10.586 7.707 9.293a1 1 0 00-1.414 1.414l2 2a1 1 0 001.414 0l4-4z" clipRule="evenodd" />
                </svg>
                Đã thanh toán trước khi check-in
              </p>
            </div>
          </div>
        </div>

        {error && (
          <div className="mb-4 bg-red-50 border border-red-200 rounded-xl px-4 py-3">
            <p className="text-sm text-red-600">{error}</p>
          </div>
        )}

        {!confirmed ? (
          <div className="space-y-3">
            <button
              onClick={handleConfirm}
              disabled={confirming}
              className="w-full h-14 bg-orange-500 hover:bg-orange-600 text-white font-bold text-lg rounded-xl shadow-lg shadow-orange-200 transition-all hover:shadow-xl cursor-pointer disabled:opacity-50"
            >
              {confirming ? "Đang xử lý..." : "✅ Xác nhận hoàn thành"}
            </button>
            <button
              onClick={() => navigate(`/driver/dispute/submit/${session.bookingId}`)}
              className="w-full h-14 bg-white hover:bg-red-50 text-red-600 font-bold text-lg rounded-xl border-2 border-red-500 transition-all cursor-pointer"
            >
              ⚠️ Khiếu nại
            </button>
            <p className="text-xs text-center text-gray-400">
              Nếu có vấn đề với phiên sạc, bạn có thể gửi khiếu nại thay vì xác nhận
            </p>
          </div>
        ) : (
          <div className="space-y-3">
            <div className="bg-green-50 border border-green-200 rounded-xl px-4 py-3 text-center">
              <p className="text-sm font-semibold text-green-700">✅ Đã xác nhận hoàn thành!</p>
            </div>
            <button
              onClick={() => navigate("/driver/reviews")}
              className="w-full h-14 bg-orange-500 hover:bg-orange-600 text-white font-bold text-lg rounded-xl shadow-lg shadow-orange-200 transition-all hover:shadow-xl cursor-pointer flex items-center justify-center gap-2"
            >
              <span>⭐</span> Đánh giá trạm sạc
            </button>
            <button
              onClick={() => navigate("/driver/my-bookings")}
              className="w-full h-11 border border-gray-200 text-gray-500 font-medium text-sm rounded-xl hover:bg-gray-50 cursor-pointer transition-all"
            >
              Về danh sách booking
            </button>
          </div>
        )}
      </div>
    </div>
  );
}

function InfoRow({ label, value }) {
  return (
    <div className="flex items-center justify-between">
      <span className="text-sm text-gray-500">{label}</span>
      <span className="text-sm font-medium text-gray-800">{value}</span>
    </div>
  );
}
