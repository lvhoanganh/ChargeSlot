import { useEffect, useRef, useState } from "react";
import { useLocation, useNavigate } from "react-router-dom";
import { chargingApi } from "@/services/api";

const toLocal = (dt) => {
  if (!dt) return "";
  const s = String(dt);
  return new Date(s.endsWith("Z") ? s : s + "Z");
};

function formatDuration(s) {
  const h = String(Math.floor(s / 3600)).padStart(2, "0");
  const m = String(Math.floor((s % 3600) / 60)).padStart(2, "0");
  const sec = String(s % 60).padStart(2, "0");
  return `${h}:${m}:${sec}`;
}

export default function ChargingActive() {
  const navigate = useNavigate();
  const location = useLocation();
  const session = location.state?.session;

  const [sessionData, setSessionData] = useState(session || null);
  const [elapsed, setElapsed] = useState(0);
  const [loading, setLoading] = useState(!session);
  const [confirming, setConfirming] = useState(false);
  const [error, setError] = useState("");
  const startRef = useRef(Date.now());

  // If no session from state, try to load from bookingId in URL
  useEffect(() => {
    if (session) return;
    // Fallback — could navigate here with bookingId
    const bookingId = location.state?.bookingId;
    if (!bookingId) {
      navigate("/driver/my-bookings");
      return;
    }
    chargingApi.getByBookingId(bookingId)
      .then(data => { setSessionData(data); setLoading(false); })
      .catch(() => { navigate("/driver/my-bookings"); });
  }, []);

  // Timer
  useEffect(() => {
    if (!sessionData) return;
    const interval = setInterval(() => {
      const sec = Math.floor((Date.now() - startRef.current) / 1000);
      setElapsed(sec);
    }, 1000);
    return () => clearInterval(interval);
  }, [sessionData]);

  // Poll session status every 10s — check if Owner stopped
  useEffect(() => {
    if (!sessionData) return;
    const interval = setInterval(async () => {
      try {
        const updated = await chargingApi.getByBookingId(sessionData.bookingId);
        if (updated) {
          setSessionData(updated);
          // If session was stopped by owner or completed
          if (updated.actualEndTime || updated.bookingStatus === "Completed") {
            navigate("/driver/charging-complete", { state: { session: updated } });
          }
        }
      } catch { /* ignore poll errors */ }
    }, 10000);
    return () => clearInterval(interval);
  }, [sessionData?.bookingId]);

  async function handleConfirm() {
    if (!sessionData) return;
    setConfirming(true);
    setError("");
    try {
      const result = await chargingApi.confirmCompletion(sessionData.id);
      navigate("/driver/charging-complete", { state: { session: result || sessionData } });
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

  const endTime = toLocal(sessionData.bookingEndTime).getTime();
  const totalDuration = Math.max(0, Math.floor((endTime - startRef.current) / 1000));
  const remaining = Math.max(0, totalDuration - elapsed);
  const progress = totalDuration > 0 ? Math.min(100, (elapsed / totalDuration) * 100) : 0;
  const isTimeUp = remaining <= 0;

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
                {isTimeUp ? "⏰ Hết thời gian sạc!" : "⚡ Đang sạc..."}
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
          <StatCard icon="🏷️" label="Cổng sạc" value={sessionData.slotName || `Slot ${sessionData.slotId}`} />
          <StatCard icon="🏢" label="Trạm" value={sessionData.stationName || "—"} />
        </div>

        {error && (
          <div className="mb-4 bg-red-50 border border-red-200 rounded-xl px-4 py-3">
            <p className="text-sm text-red-600">{error}</p>
          </div>
        )}

        <button
          onClick={handleConfirm}
          disabled={confirming}
          className="w-full h-14 bg-green-500 hover:bg-green-600 text-white font-bold text-lg rounded-xl shadow-lg shadow-green-200 transition-all hover:shadow-xl cursor-pointer flex items-center justify-center gap-2 disabled:opacity-50"
        >
          {confirming ? (
            <div className="w-5 h-5 border-2 border-white/50 border-t-white rounded-full animate-spin" />
          ) : (
            <svg className="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z" />
            </svg>
          )}
          {confirming ? "Đang xử lý..." : "Hoàn thành phiên sạc"}
        </button>
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
