import { useEffect, useState } from "react";
import { useLocation, useNavigate } from "react-router-dom";

const MOCK_BOOKING = {
  id: 1001,
  slotId: 5,
  slotName: "Cổng sạc A5",
  stationName: "Trạm sạc Vinhomes Grand Park",
  address: "Thủ Đức, TP.HCM",
  connectorType: "Type 2",
  powerKw: 22,
  startTime: "2026-03-19T20:30:00",
  endTime: "2026-03-19T22:00:00",
  totalAmount: 120000,
};

function formatTime(d) { return new Date(d).toLocaleTimeString("vi-VN", { hour: "2-digit", minute: "2-digit" }); }
function formatDate(d) { return new Date(d).toLocaleDateString("vi-VN", { day: "2-digit", month: "2-digit", year: "numeric" }); }
function formatCurrency(a) { return new Intl.NumberFormat("vi-VN", { style: "currency", currency: "VND" }).format(a); }

export default function CheckInResult() {
  const navigate = useNavigate();
  const location = useLocation();
  if (!location.state) {
    return (
      <div className="min-h-[calc(100vh-64px)] px-4 py-10 pt-24 flex items-center justify-center" style={{ background: "linear-gradient(135deg, #f8fafc 0%, #f1f5f9 50%, #e8ecf1 100%)" }}>
        <div className="max-w-md w-full rounded-2xl bg-white shadow-xl p-8 text-center">
          <div className="w-16 h-16 rounded-full bg-red-100 flex items-center justify-center mx-auto mb-4">
            <svg className="w-8 h-8 text-red-500" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-2.5L13.732 4c-.77-.833-1.964-.833-2.732 0L3.732 16.5c-.77.833.192 2.5 1.732 2.5z" />
            </svg>
          </div>
          <h2 className="text-lg font-bold text-gray-800 mb-2">Truy cập không hợp lệ</h2>
          <p className="text-sm text-gray-500 mb-6">Bạn phải quét mã QR tại cổng sạc trước khi check-in</p>
          <button onClick={() => navigate("/driver/scan-qr")} className="w-full h-12 bg-orange-500 hover:bg-orange-600 text-white font-semibold rounded-xl cursor-pointer transition-all">
            Về trang Check-in
          </button>
        </div>
      </div>
    );
  }

  const success = location.state.success;
  const reason = location.state.reason || "";
  const booking = MOCK_BOOKING;

  const [verifying, setVerifying] = useState(true);

  useEffect(() => {
    const timer = setTimeout(() => setVerifying(false), 2500);
    return () => clearTimeout(timer);
  }, []);

  if (verifying) {
    return (
      <div className="min-h-[calc(100vh-64px)] px-4 py-10 pt-24 flex items-center justify-center" style={{ background: "linear-gradient(135deg, #f8fafc 0%, #f1f5f9 50%, #e8ecf1 100%)" }}>
        <div className="max-w-md w-full">
          <div className="rounded-2xl bg-white shadow-xl p-10 flex flex-col items-center text-center">
            <div className="w-20 h-20 rounded-full bg-orange-100 flex items-center justify-center mb-6">
              <div className="w-10 h-10 border-4 border-orange-200 border-t-orange-500 rounded-full animate-spin" />
            </div>
            <h1 className="text-xl font-bold text-gray-800 mb-2">Đang xử lý check-in...</h1>
            <p className="text-sm text-gray-500">Hệ thống đang kiểm tra booking và xác thực vị trí</p>

            <div className="w-full mt-8 space-y-3">
              <VerifyStep label="Kiểm tra slot" status="done" />
              <VerifyStep label="Kiểm tra booking đã thanh toán" status="loading" />
              <VerifyStep label="Xác thực khung giờ" status="pending" />
              <VerifyStep label="Kiểm tra cổng sạc" status="pending" />
            </div>
          </div>
        </div>
      </div>
    );
  }

  return (
    <div className="min-h-[calc(100vh-64px)] px-4 py-10 pt-24" style={{ background: "linear-gradient(135deg, #f8fafc 0%, #f1f5f9 50%, #e8ecf1 100%)" }}>
      <div className="max-w-md mx-auto">
        <div
          className="rounded-2xl overflow-hidden shadow-xl mb-6"
          style={{ background: success ? "linear-gradient(135deg, #22c55e 0%, #16a34a 100%)" : "linear-gradient(135deg, #ef4444 0%, #dc2626 100%)" }}
        >
          <div className="px-8 py-10 flex flex-col items-center text-center">
            <div className="w-20 h-20 rounded-full bg-white/20 flex items-center justify-center mb-4">
              {success ? (
                <svg className="w-10 h-10 text-white" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 13l4 4L19 7" />
                </svg>
              ) : (
                <svg className="w-10 h-10 text-white" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
                </svg>
              )}
            </div>
            <h1 className="text-2xl font-bold text-white mb-1">
              {success ? "Check-in thành công!" : "Check-in thất bại"}
            </h1>
            <p className="text-white/80 text-sm">
              {success ? "Xác thực hoàn tất — phiên sạc sẵn sàng bắt đầu" : reason}
            </p>
          </div>
        </div>

        {success && (
          <>
            <div className="rounded-2xl bg-white shadow-lg overflow-hidden mb-6">
              <div className="px-6 py-4 border-b border-gray-100">
                <h2 className="text-sm font-bold text-gray-700">⚡ Thông tin phiên sạc</h2>
              </div>
              <div className="px-6 py-5 space-y-3">
                <InfoRow label="Mã booking" value={`#${booking.id}`} />
                <InfoRow label="Trạm sạc" value={booking.stationName} />
                <InfoRow label="Cổng sạc" value={booking.slotName} />
                <InfoRow label="Loại cổng" value={`${booking.connectorType} — ${booking.powerKw}kW`} />
                <InfoRow label="Khung giờ" value={`${formatTime(booking.startTime)} — ${formatTime(booking.endTime)}`} />
                <InfoRow label="Ngày" value={formatDate(booking.startTime)} />
                <InfoRow label="Đã thanh toán" value={formatCurrency(booking.totalAmount)} highlight />
              </div>
            </div>

            <button
              onClick={() => navigate("/driver/charging", { state: { booking } })}
              className="w-full h-14 bg-orange-500 hover:bg-orange-600 text-white font-bold text-lg rounded-xl shadow-lg shadow-orange-200 transition-all hover:shadow-xl cursor-pointer flex items-center justify-center gap-2"
            >
              <svg className="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M13 10V3L4 14h7v7l9-11h-7z" />
              </svg>
              Bắt đầu sạc
            </button>
          </>
        )}

        {!success && (
          <div className="space-y-3">
            <div className="p-4 bg-red-50 border border-red-200 rounded-xl">
              <p className="text-sm text-red-700 font-medium mb-1">💡 Bạn nên kiểm tra:</p>
              <ul className="text-xs text-red-600 space-y-1 ml-4 list-disc">
                <li>Đã đến đúng trạm sạc chưa?</li>
                <li>Booking đã được Owner chấp nhận và thanh toán chưa?</li>
                <li>Đã đến đúng khung giờ đã đặt chưa?</li>
              </ul>
            </div>
            <button
              onClick={() => navigate("/")}
              className="w-full h-14 bg-orange-500 hover:bg-orange-600 text-white font-bold text-lg rounded-xl shadow-lg shadow-orange-200 transition-all cursor-pointer"
            >
              Về trang chủ
            </button>
          </div>
        )}
      </div>
    </div>
  );
}

function VerifyStep({ label, status }) {
  return (
    <div className="flex items-center gap-3">
      <div className="w-6 h-6 flex items-center justify-center flex-shrink-0">
        {status === "done" && (
          <div className="w-6 h-6 rounded-full bg-green-100 flex items-center justify-center">
            <svg className="w-3.5 h-3.5 text-green-600" fill="currentColor" viewBox="0 0 20 20">
              <path fillRule="evenodd" d="M16.707 5.293a1 1 0 010 1.414l-8 8a1 1 0 01-1.414 0l-4-4a1 1 0 011.414-1.414L8 12.586l7.293-7.293a1 1 0 011.414 0z" clipRule="evenodd" />
            </svg>
          </div>
        )}
        {status === "loading" && (
          <div className="w-5 h-5 border-2 border-orange-200 border-t-orange-500 rounded-full animate-spin" />
        )}
        {status === "pending" && (
          <div className="w-6 h-6 rounded-full bg-gray-100 flex items-center justify-center">
            <div className="w-2 h-2 rounded-full bg-gray-300" />
          </div>
        )}
      </div>
      <span className={`text-sm ${status === "done" ? "text-gray-700" : status === "loading" ? "text-orange-600 font-medium" : "text-gray-400"}`}>
        {label}
      </span>
    </div>
  );
}

function CheckRow({ label, pass }) {
  return (
    <div className="flex items-center gap-3">
      <div className={`w-6 h-6 rounded-full flex items-center justify-center flex-shrink-0 ${pass ? "bg-green-100" : "bg-red-100"}`}>
        {pass ? (
          <svg className="w-3.5 h-3.5 text-green-600" fill="currentColor" viewBox="0 0 20 20">
            <path fillRule="evenodd" d="M16.707 5.293a1 1 0 010 1.414l-8 8a1 1 0 01-1.414 0l-4-4a1 1 0 011.414-1.414L8 12.586l7.293-7.293a1 1 0 011.414 0z" clipRule="evenodd" />
          </svg>
        ) : (
          <svg className="w-3.5 h-3.5 text-red-600" fill="currentColor" viewBox="0 0 20 20">
            <path fillRule="evenodd" d="M4.293 4.293a1 1 0 011.414 0L10 8.586l4.293-4.293a1 1 0 111.414 1.414L11.414 10l4.293 4.293a1 1 0 01-1.414 1.414L10 11.414l-4.293 4.293a1 1 0 01-1.414-1.414L8.586 10 4.293 5.707a1 1 0 010-1.414z" clipRule="evenodd" />
          </svg>
        )}
      </div>
      <span className="text-sm text-gray-700">{label}</span>
    </div>
  );
}

function InfoRow({ label, value, highlight }) {
  return (
    <div className="flex items-center justify-between">
      <span className="text-sm text-gray-500">{label}</span>
      <span className={`text-sm font-semibold ${highlight ? "text-green-600" : "text-gray-800"}`}>{value}</span>
    </div>
  );
}
