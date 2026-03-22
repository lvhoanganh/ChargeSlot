import { useLocation, useNavigate } from "react-router-dom";

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

  const { success, reason, session } = location.state;

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
              {success ? "Xác thực hoàn tất — phiên sạc đã bắt đầu" : reason}
            </p>
          </div>
        </div>

        {success && session && (
          <>
            <div className="rounded-2xl bg-white shadow-lg overflow-hidden mb-6">
              <div className="px-6 py-4 border-b border-gray-100">
                <h2 className="text-sm font-bold text-gray-700">⚡ Thông tin phiên sạc</h2>
              </div>
              <div className="px-6 py-5 space-y-3">
                <InfoRow label="Mã booking" value={`#${session.bookingId}`} />
                <InfoRow label="Trạm sạc" value={session.stationName || "—"} />
                <InfoRow label="Cổng sạc" value={session.slotName || `Slot ${session.slotId}`} />
              </div>
            </div>

            <button
              onClick={() => navigate("/driver/charging", { state: { session } })}
              className="w-full h-14 bg-orange-500 hover:bg-orange-600 text-white font-bold text-lg rounded-xl shadow-lg shadow-orange-200 transition-all hover:shadow-xl cursor-pointer flex items-center justify-center gap-2"
            >
              <svg className="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M13 10V3L4 14h7v7l9-11h-7z" />
              </svg>
              Xem phiên sạc
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
              onClick={() => navigate("/driver/scan-qr")}
              className="w-full h-14 bg-orange-500 hover:bg-orange-600 text-white font-bold text-lg rounded-xl shadow-lg shadow-orange-200 transition-all cursor-pointer"
            >
              Quét lại
            </button>
            <button
              onClick={() => navigate("/driver/my-bookings")}
              className="w-full h-12 border border-gray-200 text-gray-600 font-semibold rounded-xl hover:bg-gray-50 cursor-pointer transition-all"
            >
              Xem danh sách booking
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
      <span className="text-sm font-semibold text-gray-800">{value}</span>
    </div>
  );
}
