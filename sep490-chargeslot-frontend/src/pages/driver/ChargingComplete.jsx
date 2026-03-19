import { useLocation, useNavigate } from "react-router-dom";

const MOCK_RESULT = {
  booking: {
    id: 1001,
    slotName: "Cổng sạc A5",
    stationName: "Trạm sạc Vinhomes Grand Park",
    address: "Thủ Đức, TP.HCM",
    connectorType: "Type 2",
    powerKw: 22,
    startTime: "2026-03-19T20:30:00",
    endTime: "2026-03-19T22:00:00",
    totalAmount: 120000,
  },
  chargingResult: { duration: 6847, energyKwh: 35.2 },
};

function formatDuration(s) {
  const h = Math.floor(s / 3600);
  const m = Math.floor((s % 3600) / 60);
  return h > 0 ? `${h} giờ ${m} phút` : `${m} phút`;
}
function formatCurrency(a) { return new Intl.NumberFormat("vi-VN", { style: "currency", currency: "VND" }).format(a); }
function formatDateTime(d) { return new Date(d).toLocaleString("vi-VN", { day: "2-digit", month: "2-digit", year: "numeric", hour: "2-digit", minute: "2-digit" }); }

export default function ChargingComplete() {
  const navigate = useNavigate();
  const location = useLocation();
  const { booking, chargingResult } = location.state || MOCK_RESULT;

  return (
    <div className="min-h-[calc(100vh-64px)] px-4 py-10 pt-24" style={{ background: "linear-gradient(135deg, #f8fafc 0%, #f1f5f9 50%, #e8ecf1 100%)" }}>
      <div className="max-w-md mx-auto">
        <div className="rounded-2xl overflow-hidden shadow-xl mb-6" style={{ background: "linear-gradient(135deg, #22c55e 0%, #16a34a 100%)" }}>
          <div className="px-8 py-10 flex flex-col items-center text-center">
            <div className="w-20 h-20 rounded-full bg-white/20 flex items-center justify-center mb-4">
              <svg className="w-10 h-10 text-white" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z" />
              </svg>
            </div>
            <h1 className="text-2xl font-bold text-white mb-1">Sạc hoàn tất!</h1>
          </div>
        </div>

        <div className="grid grid-cols-2 gap-4 mb-6">
          <div className="rounded-xl bg-white shadow-lg p-5 text-center">
            <span className="text-3xl">⚡</span>
            <p className="text-2xl font-bold text-gray-800 mt-2">{chargingResult.energyKwh} kWh</p>
            <p className="text-xs text-gray-500 mt-1">Năng lượng đã sạc</p>
          </div>
          <div className="rounded-xl bg-white shadow-lg p-5 text-center">
            <span className="text-3xl">⏱️</span>
            <p className="text-2xl font-bold text-gray-800 mt-2">{formatDuration(chargingResult.duration)}</p>
            <p className="text-xs text-gray-500 mt-1">Thời gian sạc</p>
          </div>
        </div>

        <div className="rounded-2xl bg-white shadow-lg overflow-hidden mb-6">
          <div className="px-6 py-4 border-b border-gray-100">
            <h2 className="text-sm font-bold text-gray-700">🧾 Hóa đơn (từ Owner)</h2>
          </div>
          <div className="px-6 py-5 space-y-3">
            <InfoRow label="Trạm sạc" value={booking.stationName} />
            <InfoRow label="Cổng sạc" value={booking.slotName} />
            <InfoRow label="Loại cổng" value={`${booking.connectorType} — ${booking.powerKw}kW`} />
            <InfoRow label="Bắt đầu" value={formatDateTime(booking.startTime)} />
            <InfoRow label="Kết thúc" value={formatDateTime(booking.endTime)} />
            <InfoRow label="Năng lượng" value={`${chargingResult.energyKwh} kWh`} />
            <div className="border-t border-gray-100 pt-3">
              <div className="flex items-center justify-between">
                <span className="text-sm font-bold text-gray-800">Tổng tiền</span>
                <span className="text-lg font-bold text-orange-600">{formatCurrency(booking.totalAmount)}</span>
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

        <button
          onClick={() => navigate("/")}
          className="w-full h-14 bg-orange-500 hover:bg-orange-600 text-white font-bold text-lg rounded-xl shadow-lg shadow-orange-200 transition-all hover:shadow-xl cursor-pointer"
        >
          Xác nhận hoàn thành
        </button>
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
