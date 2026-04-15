import { useEffect, useRef, useState } from "react";
import { useLocation, useNavigate } from "react-router-dom";
import { chargingApi, bookingApi } from "@/services/api";
import { showConfirm } from "@/components/ConfirmDialog";

// Parse datetime từ BE:
// - Nếu có "Z" (UTC) → giữ nguyên, JS Date tự convert sang local
// - Nếu không có "Z" và không có offset → BE trả giờ VN Unspecified → thêm +07:00
const toLocal = (dt) => {
  if (!dt) return new Date(NaN);
  const s = String(dt).trim();
  if (s.endsWith("Z") || /[+-]\d{2}:\d{2}$/.test(s)) return new Date(s);
  return new Date(s + "+07:00");
};

function formatDuration(s) {
  const h = String(Math.floor(s / 3600)).padStart(2, "0");
  const m = String(Math.floor((s % 3600) / 60)).padStart(2, "0");
  const sec = String(s % 60).padStart(2, "0");
  return `${h}:${m}:${sec}`;
}

function formatTime(dt) {
  if (typeof dt === "number") {
    const d = new Date(dt);
    if (isNaN(d)) return "—";
    return d.toLocaleTimeString("vi-VN", { hour: "2-digit", minute: "2-digit" });
  }
  const d = toLocal(dt);
  if (isNaN(d)) return "—";
  return d.toLocaleTimeString("vi-VN", { hour: "2-digit", minute: "2-digit" });
}

export default function ChargingActive() {
  const navigate = useNavigate();
  const location = useLocation();
  const session = location.state?.session;

  const lsKey = `activeChargingBooking_${localStorage.getItem("userId") || "guest"}`;
  const [sessionData, setSessionData] = useState(session || null);
  const [bookingStartMs, setBookingStartMs] = useState(null); // giờ BẮT ĐẦU đặt lịch (ms)

  // Khởi tạo elapsed ngay từ session để tránh hiện 00:00:00 lần đầu render
  const [elapsed, setElapsed] = useState(() => {
    if (!session?.actualStartTime) return 0;
    const ms = toLocal(session.actualStartTime).getTime();
    return isNaN(ms) ? 0 : Math.max(0, Math.floor((Date.now() - ms) / 1000));
  });
  const [waitRemaining, setWaitRemaining] = useState(0); // giây còn lại để chờ
  const [loading, setLoading] = useState(!session);
  const [confirming, setConfirming] = useState(false);
  const [autoCompleting, setAutoCompleting] = useState(false); // đang tự động hoàn thành
  const [requestingEarlyEnd, setRequestingEarlyEnd] = useState(false);
  const [earlyEndRequested, setEarlyEndRequested] = useState(false);
  const [error, setError] = useState("");
  const autoConfirmTriggeredRef = useRef(false); // tránh gọi confirmCompletion 2 lần

  // If no session from state, try to load from bookingId in URL or localStorage
  useEffect(() => {
    if (session) {
      if (session.bookingId) localStorage.setItem(lsKey, String(session.bookingId));
      if (session.earlyEndRequestedAt) setEarlyEndRequested(true);
      // Lấy bookingStartTime từ session nếu có, hoặc fetch booking API
      if (session.bookingStartTime) {
        setBookingStartMs(toLocal(session.bookingStartTime).getTime());
      } else if (session.bookingId) {
        bookingApi.getById(session.bookingId)
          .then(b => { if (b?.startTime) setBookingStartMs(toLocal(b.startTime).getTime()); })
          .catch(() => {});
      }
      return;
    }
    const bookingId = location.state?.bookingId || localStorage.getItem(lsKey);
    if (!bookingId) { navigate("/driver/my-bookings"); return; }
    chargingApi.getByBookingId(Number(bookingId))
      .then(data => {
        if (!data || data.actualEndTime) {
          localStorage.removeItem(lsKey);
          navigate("/driver/my-bookings");
          return;
        }
        setSessionData(data);
        if (data?.earlyEndRequestedAt) setEarlyEndRequested(true);
        // Fetch booking để lấy startTime
        if (data?.bookingId) {
          bookingApi.getById(data.bookingId)
            .then(b => { if (b?.startTime) setBookingStartMs(toLocal(b.startTime).getTime()); })
            .catch(() => {});
        }
        setLoading(false);
      })
      .catch(() => {
        localStorage.removeItem(lsKey);
        navigate("/driver/my-bookings");
      });
  }, []);

  // Timer đếm ngược chờ (nếu chưa đến giờ)
  useEffect(() => {
    if (!bookingStartMs) return;
    const update = () => {
      const diff = Math.floor((bookingStartMs - Date.now()) / 1000);
      setWaitRemaining(diff > 0 ? diff : 0);
    };
    update();
    const iv = setInterval(update, 1000);
    return () => clearInterval(iv);
  }, [bookingStartMs]);

  // Timer đếm elapsed (phiên đang sạc)
  useEffect(() => {
    if (!sessionData) return;
    const calcElapsed = () => {
      const actMs = sessionData.actualStartTime ? toLocal(sessionData.actualStartTime).getTime() : Date.now();
      const schedMs = sessionData.bookingStartTime ? toLocal(sessionData.bookingStartTime).getTime() : actMs;
      const effectiveStartMs = Math.max(isNaN(actMs) ? Date.now() : actMs, isNaN(schedMs) ? 0 : schedMs);
      
      setElapsed(Math.max(0, Math.floor((Date.now() - effectiveStartMs) / 1000)));
    };
    calcElapsed();
    const interval = setInterval(calcElapsed, 1000);
    return () => clearInterval(interval);
  }, [sessionData]);

  // Poll session status — nhanh hơn (3s) khi hết giờ, bình thường 10s
  // Khi phát hiện CompletedPendingInvoice → tự động confirmCompletion luôn
  useEffect(() => {
    if (!sessionData) return;

    // Tính isTimeUp ngay trong effect để dùng cho interval speed
    const getIsTimeUp = () => {
      const actMs = sessionData.actualStartTime ? toLocal(sessionData.actualStartTime).getTime() : Date.now();
      const schedMs = sessionData.bookingStartTime ? toLocal(sessionData.bookingStartTime).getTime() : actMs;
      const startMs = Math.max(isNaN(actMs) ? Date.now() : actMs, isNaN(schedMs) ? 0 : schedMs);
      const endMs = toLocal(sessionData.bookingEndTime).getTime();
      return Date.now() >= endMs;
    };

    async function pollOnce() {
      try {
        const updated = await chargingApi.getByBookingId(sessionData.bookingId);
        if (!updated) return;
        setSessionData(updated);
        if (updated.earlyEndRequestedAt) setEarlyEndRequested(true);

        // Nếu đã Completed/actualEndTime → navigate
        if (updated.actualEndTime && updated.bookingStatus === "Completed") {
          localStorage.removeItem(lsKey);
          navigate("/driver/charging-complete", { state: { session: updated } });
          return;
        }

        // Khi hết giờ / kết thúc sớm: nếu BE đã chuyển sang CompletedPendingInvoice → chuyển qua ChargingComplete để hiện Hóa đơn xác nhận
        if (updated.bookingStatus === "CompletedPendingInvoice" && !autoConfirmTriggeredRef.current) {
          autoConfirmTriggeredRef.current = true;
          setAutoCompleting(true);
          setTimeout(() => {
             localStorage.removeItem(lsKey);
             navigate(`/driver/charging-complete`, { state: { session: updated } });
          }, 1500);
        }
      } catch { /* ignore poll errors */ }
    }

    // Quyết định interval: 3s khi hết giờ (cần phản hồi nhanh), 10s bình thường
    const speed = getIsTimeUp() ? 3000 : 10000;
    const interval = setInterval(pollOnce, speed);
    return () => clearInterval(interval);
  }, [sessionData?.bookingId, earlyEndRequested]);

  async function handleRequestEarlyEnd() {
    if (!sessionData || earlyEndRequested) return;
    if (!(await showConfirm("Bạn muốn kết thúc sạc sớm? Hệ thống sẽ tự động xử lý.", "Kết thúc sớm"))) return;
    setRequestingEarlyEnd(true);
    setError("");
    try {
      const result = await chargingApi.requestEarlyEnd(sessionData.id);
      setEarlyEndRequested(true);
      autoConfirmTriggeredRef.current = true; // tránh auto-stop effect trigger lại
      if (result) setSessionData(result);
    } catch (err) {
      setError(err?.message || "Lỗi khi gửi yêu cầu kết thúc sớm.");
    } finally {
      setRequestingEarlyEnd(false);
    }
  }

  async function handleConfirm() {
    if (!sessionData) return;
    setConfirming(true);
    setError("");
    try {
      await chargingApi.confirmCompletion(sessionData.id);
      localStorage.removeItem(lsKey);
      navigate("/driver/charging-complete", { state: { session: { ...sessionData, bookingStatus: "Completed" } } });
    } catch (err) {
      setError(err?.message || "Lỗi xác nhận, vui lòng thử lại!");
      setConfirming(false);
    }
  }

  if (loading || !sessionData) {
    return (
      <div className="min-h-[calc(100vh-64px)] px-4 py-10 pt-24 flex items-center justify-center" style={{ background: "linear-gradient(135deg, #f8fafc 0%, #e8ecf1 100%)" }}>
        <div className="text-center">
          <div className="w-12 h-12 border-4 border-orange-200 border-t-orange-500 rounded-full animate-spin mx-auto mb-4" />
          <p className="text-gray-500">Đang tải phiên sạc...</p>
        </div>
      </div>
    );
  }

  const actMs = sessionData.actualStartTime ? toLocal(sessionData.actualStartTime).getTime() : Date.now();
  const schedMs = sessionData.bookingStartTime ? toLocal(sessionData.bookingStartTime).getTime() : actMs;
  const startTimeMs = Math.max(isNaN(actMs) ? Date.now() : actMs, isNaN(schedMs) ? 0 : schedMs);
  const endTimeMs = toLocal(sessionData.bookingEndTime).getTime();
  const totalDuration = Math.max(0, Math.floor((endTimeMs - startTimeMs) / 1000));
  const remaining = Math.max(0, totalDuration - elapsed);
  const progress = totalDuration > 0 ? Math.min(100, (elapsed / totalDuration) * 100) : 0;
  const isTimeUp = remaining <= 0;

  //  Đang chờ đến giờ bắt đầu 
  const isWaiting = bookingStartMs != null && waitRemaining > 0;

  if (isWaiting) {
    const scheduledTime = formatTime(bookingStartMs);
    const wH = String(Math.floor(waitRemaining / 3600)).padStart(2, "0");
    const wM = String(Math.floor((waitRemaining % 3600) / 60)).padStart(2, "0");
    const wS = String(waitRemaining % 60).padStart(2, "0");

    return (
      <div
        className="min-h-[calc(100vh-64px)] px-4 py-10 pt-24"
        style={{ background: "linear-gradient(135deg, #f8fafc 0%, #f1f5f9 50%, #e8ecf1 100%)" }}
      >
        <div className="max-w-md mx-auto">
          {/* Waiting card */}
          <div
            className="rounded-2xl overflow-hidden shadow-xl mb-6"
            style={{ background: "linear-gradient(135deg, #8b5cf6 0%, #7c3aed 100%)" }}
          >
            <div className="relative px-8 py-10 flex flex-col items-center text-center">
              <div className="absolute inset-0 opacity-10">
                <div className="absolute top-0 right-0 w-48 h-48 bg-white rounded-full -translate-y-24 translate-x-24" />
              </div>
              <div className="relative">
                {/* Animated waiting icon */}
                <div className="w-20 h-20 rounded-full bg-white/20 flex items-center justify-center mb-4 mx-auto ring-4 ring-white/30 ring-offset-0">
                  <svg className="w-10 h-10 text-white" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.5}
                      d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z" />
                  </svg>
                </div>
                <p className="text-white/80 text-sm mb-1"> Đang chờ đến giờ sạc...</p>
                <p className="text-5xl font-bold text-white font-mono tracking-wider mb-2">
                  {wH}:{wM}:{wS}
                </p>
                <p className="text-white/70 text-sm">
                  Phiên sạc sẽ bắt đầu lúc{" "}
                  <strong className="text-white">{scheduledTime}</strong>
                </p>
              </div>
            </div>
          </div>

          {/* Info card */}
          <div className="rounded-2xl bg-white shadow-lg overflow-hidden mb-6">
            <div className="px-6 py-4 border-b border-gray-100">
              <h2 className="text-sm font-bold text-gray-700">ℹ️ Thông tin phiên sạc</h2>
            </div>
            <div className="px-6 py-5 space-y-3">
              <InfoRow label="Cổng sạc" value={sessionData.slotName || `Slot ${sessionData.slotId}`} />
              <InfoRow label="Trạm" value={sessionData.stationName || "—"} />
              <InfoRow label="Giờ bắt đầu" value={scheduledTime} highlight />
              <InfoRow label="Giờ kết thúc" value={formatTime(sessionData.bookingEndTime)} />
            </div>
          </div>

          {/* Notice box */}
          <div className="rounded-2xl border border-purple-200 bg-purple-50 px-5 py-4 mb-6">
            <p className="text-sm font-semibold text-purple-800 mb-1">Bạn đã check-in sớm hơn giờ đặt</p>
            <p className="text-xs text-purple-600 leading-relaxed">
              Cổng sạc đã được xác nhận. Hệ thống sẽ tự động kích hoạt phiên sạc khi đến{" "}
              <strong>{scheduledTime}</strong>. Vui lòng ở lại cổng sạc và chờ timer này về 0.
            </p>
          </div>

          {/* Back button */}
          <button
            onClick={() => navigate("/driver/my-bookings")}
            className="w-full h-12 border border-gray-200 text-gray-600 font-semibold rounded-xl hover:bg-gray-50 cursor-pointer transition-all text-sm"
          >
            ← Quay lại danh sách booking
          </button>
        </div>
      </div>
    );
  }

  //  Đang sạc bình thường 
  return (
    <div className="min-h-[calc(100vh-64px)] px-4 py-10 pt-24" style={{ background: "linear-gradient(135deg, #f8fafc 0%, #f1f5f9 50%, #e8ecf1 100%)" }}>
      <div className="max-w-md mx-auto">
        {/* Timer header */}
        <div
          className="rounded-2xl overflow-hidden shadow-xl mb-6"
          style={{ background: isTimeUp ? "linear-gradient(135deg, #f97316 0%, #ea580c 100%)" : "linear-gradient(135deg, #3b82f6 0%, #2563eb 100%)" }}
        >
          <div className="relative px-8 py-10 flex flex-col items-center text-center">
            <div className="absolute inset-0 opacity-10">
              <div className="absolute top-0 right-0 w-48 h-48 bg-white rounded-full -translate-y-24 translate-x-24" />
            </div>
            <div className="relative">
              {!isTimeUp && (
                <div className="w-16 h-16 rounded-full bg-white/20 flex items-center justify-center mb-4 mx-auto">
                  <div className="w-10 h-10 border-4 border-white/30 border-t-white rounded-full animate-spin" />
                </div>
              )}
              <p className="text-white/80 text-sm mb-1">
                {isTimeUp ? " Hết thời gian sạc!" : " Đang sạc..."}
              </p>
              <p className="text-5xl font-bold text-white font-mono tracking-wider mb-2">
                {formatDuration(remaining)}
              </p>
              <p className="text-white/60 text-xs">Đã sạc: {formatDuration(elapsed)}</p>
            </div>
          </div>
        </div>

        {/* Progress bar */}
        <div className="rounded-2xl bg-white shadow-lg overflow-hidden mb-6">
          <div className="px-6 py-4">
            <div className="flex justify-between text-xs text-gray-500 mb-2">
              <span>Tiến trình</span>
              <span>{Math.round(progress)}%</span>
            </div>
            <div className="w-full h-3 bg-gray-100 rounded-full overflow-hidden">
              <div
                className="h-full rounded-full transition-all duration-1000"
                style={{
                  width: `${progress}%`,
                  background: isTimeUp ? "linear-gradient(90deg, #f97316, #ea580c)" : "linear-gradient(90deg, #3b82f6, #2563eb)",
                }}
              />
            </div>
          </div>
        </div>

        {/* Stats */}
        <div className="grid grid-cols-2 gap-4 mb-6">
          <StatCard icon="️" label="Cổng sạc" value={sessionData.slotName || `Slot ${sessionData.slotId}`} />
          <StatCard icon="" label="Trạm" value={sessionData.stationName || "—"} />
        </div>

        {error && (
          <div className="mb-4 bg-red-50 border border-red-200 rounded-xl px-4 py-3">
            <p className="text-sm text-red-600">{error}</p>
          </div>
        )}

        {/* Trạng thái khi hết giờ / early end */}
        {earlyEndRequested ? (
          <div className="mb-4 bg-amber-50 border border-amber-200 rounded-xl px-4 py-3 text-center">
            <p className="text-sm font-semibold text-amber-700">
              {autoCompleting ? " Đang hoàn tất phiên sạc..." : " Hết thời gian sạc"}
            </p>
            <p className="text-xs text-amber-600 mt-1">
              {autoCompleting
                ? "Hệ thống đang tự động xử lý hóa đơn, vui lòng chờ..."
                : "Hệ thống đang tự động kết thúc phiên sạc..."}
            </p>
          </div>
        ) : !isTimeUp && (
          <button
            onClick={handleRequestEarlyEnd}
            disabled={requestingEarlyEnd}
            className="w-full h-12 mb-4 bg-amber-50 hover:bg-amber-100 text-amber-700 font-semibold text-sm rounded-xl border border-amber-200 transition-all cursor-pointer flex items-center justify-center gap-2 disabled:opacity-50"
          >
            {requestingEarlyEnd ? (
              <div className="w-4 h-4 border-2 border-amber-400 border-t-amber-700 rounded-full animate-spin" />
            ) : "️"}
            {requestingEarlyEnd ? "Đang gửi..." : "Kết thúc sớm"}
          </button>
        )}

        {sessionData.bookingStatus === "CompletedPendingInvoice" && !autoCompleting ? (
          <div>
            <div className="mb-3 bg-amber-50 border border-amber-200 rounded-xl px-4 py-3 text-center">
              <p className="text-sm font-semibold text-amber-700"> Phiên sạc đã kết thúc!</p>
              <p className="text-xs text-amber-600 mt-0.5">Vui lòng kiểm tra và xác nhận hóa đơn...</p>
            </div>
            <div className="w-full h-14 bg-amber-100 rounded-xl flex items-center justify-center gap-2">
              <div className="w-5 h-5 border-2 border-amber-300 border-t-amber-600 rounded-full animate-spin" />
              <span className="text-sm text-amber-700 font-semibold">Đang chuyển sang trang thanh toán...</span>
            </div>
          </div>
        ) : autoCompleting ? (
          <div className="w-full h-14 bg-amber-100 rounded-xl flex items-center justify-center gap-2">
            <div className="w-5 h-5 border-2 border-amber-300 border-t-amber-600 rounded-full animate-spin" />
            <span className="text-sm text-amber-700 font-semibold">Đang chuyển sang trang xác nhận...</span>
          </div>
        ) : !earlyEndRequested ? (
          <div className="w-full h-14 bg-gray-100 rounded-xl flex items-center justify-center gap-2">
            <div className="w-4 h-4 border-2 border-gray-300 border-t-gray-500 rounded-full animate-spin" />
            <span className="text-sm text-gray-500 font-medium">Đang sạc — tự động kết thúc khi hết giờ</span>
          </div>
        ) : (
          <div className="w-full h-14 bg-amber-50 rounded-xl flex items-center justify-center gap-2 border border-amber-200">
            <div className="w-4 h-4 border-2 border-amber-300 border-t-amber-600 rounded-full animate-spin" />
            <span className="text-sm text-amber-700 font-medium">Đang xử lý kết thúc phiên sạc...</span>
          </div>
        )}
      </div>
    </div>
  );
}

function StatCard({ icon, label, value }) {
  return (
    <div className="rounded-xl bg-white shadow-lg p-4 text-center">
      <span className="text-2xl">{icon}</span>
      <p className="text-xs text-gray-500 mt-1">{label}</p>
      <p className="text-sm font-bold text-gray-800 mt-0.5">{value}</p>
    </div>
  );
}

function InfoRow({ label, value, highlight }) {
  return (
    <div className="flex items-center justify-between">
      <span className="text-sm text-gray-500">{label}</span>
      <span className={`text-sm font-semibold ${highlight ? "text-purple-700" : "text-gray-800"}`}>{value}</span>
    </div>
  );
}
