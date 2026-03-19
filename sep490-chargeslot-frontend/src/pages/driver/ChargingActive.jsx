import { useEffect, useRef, useState } from "react";
import { useLocation, useNavigate } from "react-router-dom";

const MOCK_BOOKING = {
  id: 1001,
  slotName: "Cổng sạc A5",
  stationName: "Trạm sạc Vinhomes Grand Park",
  connectorType: "Type 2",
  powerKw: 22,
  startTime: "2026-03-19T20:30:00",
  endTime: "2026-03-19T22:00:00",
  totalAmount: 120000,
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
  const booking = location.state?.booking || MOCK_BOOKING;

  const startRef = useRef(Date.now());
  const endTime = new Date(booking.endTime).getTime();
  const totalDuration = Math.max(0, Math.floor((endTime - startRef.current) / 1000));

  const [elapsed, setElapsed] = useState(0);
  const [energyKwh, setEnergyKwh] = useState(0);

  useEffect(() => {
    const interval = setInterval(() => {
      const sec = Math.floor((Date.now() - startRef.current) / 1000);
      setElapsed(sec);
      setEnergyKwh(((sec / 3600) * booking.powerKw * 0.85).toFixed(2));
    }, 1000);
    return () => clearInterval(interval);
  }, [booking.powerKw]);

  const remaining = Math.max(0, totalDuration - elapsed);
  const progress = totalDuration > 0 ? Math.min(100, (elapsed / totalDuration) * 100) : 0;
  const isTimeUp = remaining <= 0;

  function handleComplete() {
    navigate("/driver/charging-complete", {
      state: {
        booking,
        chargingResult: { duration: elapsed, energyKwh: parseFloat(energyKwh) },
      },
    });
  }

  return (
    <div className="min-h-[calc(100vh-64px)] px-4 py-10 pt-24" style={{ background: "linear-gradient(135deg, #f8fafc 0%, #f1f5f9 50%, #e8ecf1 100%)" }}>
      <div className="max-w-md mx-auto">
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

        <div className="grid grid-cols-2 gap-4 mb-6">
          <StatCard icon="⚡" label="Năng lượng" value={`${energyKwh} kWh`} />
          <StatCard icon="🔌" label="Công suất" value={`${booking.powerKw} kW`} />
          <StatCard icon="🏷️" label="Cổng sạc" value={booking.slotName} />
          <StatCard icon="🏢" label="Trạm" value={booking.stationName} />
        </div>

        <button
          onClick={handleComplete}
          className="w-full h-14 bg-green-500 hover:bg-green-600 text-white font-bold text-lg rounded-xl shadow-lg shadow-green-200 transition-all hover:shadow-xl cursor-pointer flex items-center justify-center gap-2"
        >
          <svg className="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z" />
          </svg>
          Hoàn thành phiên sạc
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
