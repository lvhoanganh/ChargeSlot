import { useState, useEffect, useRef } from "react";
import { showToast } from "@/components/Toast";
import { useNavigate } from "react-router-dom";
import { useAuthStore } from "@/stores/authStore";
import { Html5Qrcode } from "html5-qrcode";

export default function ScanQR() {
  const navigate = useNavigate();
  const { token } = useAuthStore();
  const [cameraActive, setCameraActive] = useState(false);
  const [validating, setValidating] = useState(false);
  const [cameraError, setCameraError] = useState(null);
  const [apiError, setApiError] = useState("");
  const scannerRef = useRef(null);
  const scannedRef = useRef(false);


  const [steps, setSteps] = useState([
    { label: "Giải mã QR code", status: "pending" },
    { label: "Xác thực booking (Paid)", status: "pending" },
    { label: "Kiểm tra khung giờ hợp lệ", status: "pending" },
    { label: "Bắt đầu phiên sạc", status: "pending" },
  ]);

  useEffect(() => {
    return () => {
      if (scannerRef.current) {
        scannerRef.current.stop().catch(() => { });
      }
    };
  }, []);

  if (!token) {
    return (
      <div className="min-h-[calc(100vh-64px)] px-6 py-10 pt-24" style={{ background: "linear-gradient(135deg, #f8fafc 0%, #f1f5f9 50%, #e8ecf1 100%)" }}>
        <div className="max-w-md mx-auto rounded-2xl bg-white shadow-xl overflow-hidden">
          <div className="px-8 py-10 flex flex-col items-center text-center" style={{ background: "linear-gradient(135deg, #ff7e29 0%, #f97316 100%)" }}>
            <div className="w-20 h-20 rounded-full bg-white/20 flex items-center justify-center mb-4">
              <svg className="w-10 h-10 text-white" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 15v2m-6 4h12a2 2 0 002-2v-6a2 2 0 00-2-2H6a2 2 0 00-2 2v6a2 2 0 002 2zm10-10V7a4 4 0 00-8 0v4h8z" />
              </svg>
            </div>
            <h1 className="text-2xl font-bold text-white mb-2">Yêu cầu đăng nhập</h1>
            <p className="text-white/80 text-sm">Bạn phải đăng nhập vào hệ thống mới làm thủ tục check-in</p>
          </div>
          <div className="px-8 py-6 flex flex-col gap-3">
            <button onClick={() => navigate("/login")} className="w-full h-12 bg-orange-500 hover:bg-orange-600 text-white font-semibold rounded-xl cursor-pointer transition-all">Đăng nhập ngay</button>
            <button onClick={() => navigate("/register")} className="w-full h-12 border-2 border-orange-300 text-orange-600 font-semibold rounded-xl hover:bg-orange-50 cursor-pointer transition-all">Chưa có tài khoản? Đăng ký</button>
          </div>
        </div>
      </div>
    );
  }

  async function startScanner() {
    setCameraError(null);
    scannedRef.current = false;

    try {
      const scanner = new Html5Qrcode("qr-reader");
      scannerRef.current = scanner;

      await scanner.start(
        { facingMode: "environment" },
        { fps: 15, disableFlip: false },
        (decodedText) => {
          if (scannedRef.current) return;
          scannedRef.current = true;
          scanner.stop().catch(() => { });
          setCameraActive(false);
          runCheckIn(decodedText);
        },
        () => { }
      );
      setCameraActive(true);
    } catch (err) {
      console.error("Camera error:", err);
      setCameraError(typeof err === "string" ? err : "Không thể mở camera. Hãy cho phép truy cập camera và thử lại.");
    }
  }

  async function stopScanner() {
    if (scannerRef.current) {
      await scannerRef.current.stop().catch(() => { });
      scannerRef.current = null;
    }
    setCameraActive(false);
  }

  async function runCheckIn(qrText) {
    setValidating(true);
    setApiError("");

    // Step 1: Decode QR
    setSteps(prev => prev.map((s, i) => i === 0 ? { ...s, status: "loading" } : s));
    await delay(400);

    let qrToken = qrText.trim();
    // QR có thể là JSON hoặc plain token
    try {
      const parsed = JSON.parse(qrText);
      if (parsed.qrCodeToken) qrToken = parsed.qrCodeToken;
      else if (parsed.token) qrToken = parsed.token;
      else if (parsed.slotId) qrToken = qrText; // dùng raw
    } catch {
      // plain text token — OK
    }

    if (!qrToken) {
      setSteps(prev => prev.map((s, i) => i === 0 ? { ...s, status: "fail" } : s));
      setApiError("Mã QR không hợp lệ");
      return;
    }

    setSteps(prev => prev.map((s, i) => i === 0 ? { ...s, status: "done" } : s));

    // Step 2-4: Call backend API (validate Paid + time window + start session)
    setSteps(prev => prev.map((s, i) => i === 1 ? { ...s, status: "loading" } : s));
    await delay(300);

    const accessToken = localStorage.getItem("accessToken");
    const controller = new AbortController();
    const timeout = setTimeout(() => controller.abort(), 15000);

    try {
      const API_URL = import.meta.env.VITE_BASE_URL || "https://chargeslot-api-f8b5brexe2b0ekhp.japaneast-01.azurewebsites.net/api";
      const res = await fetch(`${API_URL}/charging/check-in`, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          Authorization: `Bearer ${accessToken}`,
        },
        body: JSON.stringify({ qrCodeToken: qrToken }),
        signal: controller.signal,
      });

      clearTimeout(timeout);

      if (res.status === 401) {
        localStorage.removeItem("accessToken");
        navigate("/login");
        return;
      }

      const data = await res.json().catch(() => null);

      if (!res.ok) {
        // Backend trả lỗi — hiện message
        let msg = data?.message || `Lỗi check-in (${res.status})`;

        if (res.status === 400) {
          showToast.error(msg, 5000);
          setSteps(prev => prev.map((s, i) =>
            i === 1 ? { ...s, status: "fail" } : s
          ));
          setApiError(msg);
          return;
        }

        // Determine which step failed based on error message
        if (msg.includes("thanh toán") || msg.includes("Paid") || msg.includes("status")) {
          setSteps(prev => prev.map((s, i) =>
            i === 1 ? { ...s, status: "fail" } : i === 0 ? { ...s, status: "done" } : s
          ));
        } else if (msg.includes("giờ") || msg.includes("time") || msg.includes("window") || msg.includes("sớm") || msg.includes("muộn")) {
          setSteps(prev => prev.map((s, i) =>
            i <= 1 ? { ...s, status: "done" } : i === 2 ? { ...s, status: "fail" } : s
          ));
        } else {
          setSteps(prev => prev.map((s, i) =>
            i === 1 ? { ...s, status: "fail" } : s
          ));
        }

        setApiError(msg);
        return;
      }

      // Success! Steps 2, 3, 4 all passed
      setSteps(prev => prev.map((s, i) =>
        i === 1 ? { ...s, status: "done" } : s
      ));
      await delay(300);
      setSteps(prev => prev.map((s, i) =>
        i === 2 ? { ...s, status: "done" } : s
      ));
      await delay(300);
      setSteps(prev => prev.map((s, i) =>
        i === 3 ? { ...s, status: "done" } : s
      ));
      await delay(500);

      // Save bookingId to localStorage for persistence
      if (data?.bookingId) {
        const uId = localStorage.getItem("userId") || "guest";
        localStorage.setItem(`activeChargingBooking_${uId}`, String(data.bookingId));
      }

      // Navigate to charging page with session data
      navigate("/driver/charging", { state: { session: data } });

    } catch (err) {
      clearTimeout(timeout);
      if (err.name === "AbortError") {
        setApiError("⏱️ Yêu cầu quá lâu, vui lòng thử lại!");
      } else {
        setApiError("Lỗi kết nối đến server!");
      }
      setSteps(prev => prev.map((s, i) =>
        i === 1 ? { ...s, status: "fail" } : s
      ));
    }
  }

  // Validation / error screen
  if (validating) {
    return (
      <div className="min-h-[calc(100vh-64px)] px-4 py-10 pt-24 flex items-center justify-center" style={{ background: "linear-gradient(135deg, #f8fafc 0%, #f1f5f9 50%, #e8ecf1 100%)" }}>
        <div className="max-w-md w-full">
          <div className="rounded-2xl bg-white shadow-xl p-10 flex flex-col items-center text-center">
            {!apiError ? (
              <>
                <div className="w-20 h-20 rounded-full bg-orange-100 flex items-center justify-center mb-6">
                  <div className="w-10 h-10 border-4 border-orange-200 border-t-orange-500 rounded-full animate-spin" />
                </div>
                <h1 className="text-xl font-bold text-gray-800 mb-2">Đang xử lý check-in...</h1>
                <p className="text-sm text-gray-500 mb-8">Hệ thống đang xác thực booking và cổng sạc</p>
              </>
            ) : (
              <>
                <div className="w-20 h-20 rounded-full bg-red-100 flex items-center justify-center mb-6">
                  <svg className="w-10 h-10 text-red-500" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
                  </svg>
                </div>
                <h1 className="text-xl font-bold text-gray-800 mb-2">Check-in thất bại</h1>
                <p className="text-sm text-red-500 mb-6 font-medium">{apiError}</p>
              </>
            )}

            <div className="w-full space-y-3 mb-6">
              {steps.map((step, i) => (
                <StepRow key={i} label={step.label} status={step.status} />
              ))}
            </div>

            {apiError && (
              <div className="w-full space-y-2">
                <button
                  onClick={() => { setValidating(false); setApiError(""); scannedRef.current = false; setSteps(s => s.map(x => ({ ...x, status: "pending" }))); }}
                  className="w-full h-12 bg-orange-500 hover:bg-orange-600 text-white font-semibold rounded-xl cursor-pointer transition-all"
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
      </div>
    );
  }

  return (
    <div className="min-h-[calc(100vh-64px)] px-4 py-10 pt-24" style={{ background: "linear-gradient(135deg, #f8fafc 0%, #f1f5f9 50%, #e8ecf1 100%)" }}>
      <div className="max-w-lg mx-auto">
        <div className="text-center mb-6">
          <h1 className="text-2xl font-bold text-gray-800">Check-in tại trạm sạc</h1>
          <p className="text-sm text-gray-500 mt-1">Quét mã QR tại cổng sạc để bắt đầu</p>
        </div>

        <div className="rounded-2xl bg-white shadow-xl overflow-hidden mb-6">
          <div className="relative w-full overflow-hidden" style={{ minHeight: cameraActive ? "360px" : "280px", background: cameraActive ? "#000" : "linear-gradient(135deg, #f1f5f9 0%, #e2e8f0 100%)" }}>
            <div id="qr-reader" className="w-full" style={{ display: cameraActive ? "block" : "none" }} />

            {cameraActive && (
              <div className="absolute inset-0 pointer-events-none flex items-center justify-center">
                <div className="absolute bottom-4 left-0 right-0 text-center">
                  <p className="text-white/80 text-sm bg-black/40 inline-block px-4 py-1.5 rounded-full">📷 Hướng camera vào mã QR</p>
                </div>
              </div>
            )}

            {!cameraActive && (
              <div className="flex flex-col items-center justify-center h-full py-12 gap-4">
                <div className="w-24 h-24 rounded-2xl bg-white shadow-lg flex items-center justify-center">
                  <svg className="w-14 h-14 text-orange-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.5} d="M12 4v1m6 11h2m-6 0h-2v4m0-11v3m0 0h.01M12 12h4.01M16 20h4M4 12h4m12 0h.01M5 8h2a1 1 0 001-1V5a1 1 0 00-1-1H5a1 1 0 00-1 1v2a1 1 0 001 1zm12 0h2a1 1 0 001-1V5a1 1 0 00-1-1h-2a1 1 0 00-1 1v2a1 1 0 001 1zM5 20h2a1 1 0 001-1v-2a1 1 0 00-1-1H5a1 1 0 00-1 1v2a1 1 0 001 1z" />
                  </svg>
                </div>
                <p className="text-gray-500 text-sm text-center">Nhấn nút bên dưới để mở camera quét QR</p>
                {cameraError && (
                  <div className="mx-6 p-3 bg-red-50 border border-red-200 rounded-xl">
                    <p className="text-xs text-red-600 text-center">{cameraError}</p>
                  </div>
                )}
              </div>
            )}
          </div>

          <div className="p-5 space-y-3">
            {!cameraActive ? (
              <>
                <button onClick={startScanner} className="w-full h-14 bg-orange-500 hover:bg-orange-600 text-white font-bold text-lg rounded-xl shadow-lg shadow-orange-200 hover:shadow-xl transition-all flex items-center justify-center gap-3 cursor-pointer">
                  <svg className="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M3 9a2 2 0 012-2h.93a2 2 0 001.664-.89l.812-1.22A2 2 0 0110.07 4h3.86a2 2 0 011.664.89l.812 1.22A2 2 0 0018.07 7H19a2 2 0 012 2v9a2 2 0 01-2 2H5a2 2 0 01-2-2V9z" />
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 13a3 3 0 11-6 0 3 3 0 016 0z" />
                  </svg>
                  Mở camera quét QR
                </button>
                <label className="w-full h-14 bg-blue-500 hover:bg-blue-600 text-white font-bold text-lg rounded-xl shadow-lg shadow-blue-200 hover:shadow-xl transition-all flex items-center justify-center gap-3 cursor-pointer">
                  <svg className="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 16l4.586-4.586a2 2 0 012.828 0L16 16m-2-2l1.586-1.586a2 2 0 012.828 0L20 14m-6-6h.01M6 20h12a2 2 0 002-2V6a2 2 0 00-2-2H6a2 2 0 00-2 2v12a2 2 0 002 2z" />
                  </svg>
                  Upload ảnh mã QR
                  <input
                    type="file"
                    accept="image/*"
                    className="hidden"
                    onChange={async (e) => {
                      const file = e.target.files?.[0];
                      if (!file) return;
                      try {
                        const scanner = new Html5Qrcode("qr-reader-upload");
                        const result = await scanner.scanFile(file, true);
                        scanner.clear();
                        runCheckIn(result);
                      } catch (err) {
                        setCameraError("Không đọc được mã QR từ ảnh. Hãy thử ảnh rõ hơn!");
                      }
                      e.target.value = "";
                    }}
                  />
                </label>
                <div id="qr-reader-upload" style={{ display: "none" }} />
              </>
            ) : (
              <button onClick={stopScanner} className="w-full h-14 bg-gray-500 hover:bg-gray-600 text-white font-bold text-lg rounded-xl shadow-lg transition-all flex items-center justify-center gap-3 cursor-pointer">
                <svg className="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
                </svg>
                Tắt camera
              </button>
            )}
          </div>
        </div>
        <div className="rounded-2xl bg-white shadow-lg overflow-hidden mb-6">
          <div className="px-5 py-3 border-b border-gray-100 flex items-center gap-2">
            <span className="text-sm">💡</span>
            <h3 className="text-sm font-bold text-gray-700">Hướng dẫn check-in</h3>
          </div>
          <div className="p-5 space-y-2 text-sm text-gray-600">
            <p>1. Đảm bảo booking đã được <strong>thanh toán (Paid)</strong></p>
            <p>2. Đến trạm sạc <strong>đúng khung giờ</strong> đã đặt</p>
            <p>3. Quét mã <strong>QR trên cổng sạc</strong> hoặc nhập token thủ công</p>
            <p>4. Hệ thống tự xác thực và bắt đầu sạc</p>
          </div>
        </div>

        <button onClick={() => { stopScanner(); navigate(-1); }} className="text-sm text-gray-500 hover:text-orange-600 transition-all flex items-center gap-1 cursor-pointer">
          <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 19l-7-7 7-7" />
          </svg>
          Quay lại
        </button>
      </div>

      <style>{`
        #qr-reader { border: none !important; width: 100% !important; background: #000 !important; }
        #qr-reader video { width: 100% !important; height: auto !important; object-fit: cover !important; border-radius: 0 !important; }
        #qr-reader__scan_region { min-height: 300px !important; }
        #qr-reader__scan_region video { width: 100% !important; border-radius: 0 !important; }
        #qr-reader__scan_region img { display: none !important; }
        #qr-reader__dashboard_section, #qr-reader__dashboard_section_swaplink, #qr-reader__dashboard_section_csr,
        #qr-reader__status_span, #qr-reader__header_message, #qr-reader__camera_selection, #qr-reader__filescan_input,
        #qr-reader__dashboard { display: none !important; }
        #qr-reader > div:last-child { display: none !important; }
        #qr-reader__scan_region > br { display: none !important; }
      `}</style>
    </div>
  );
}

function delay(ms) { return new Promise(r => setTimeout(r, ms)); }

function StepRow({ label, status }) {
  return (
    <div className="flex items-center gap-3">
      <div className="w-7 h-7 flex items-center justify-center flex-shrink-0">
        {status === "done" && (
          <div className="w-7 h-7 rounded-full bg-green-100 flex items-center justify-center">
            <svg className="w-4 h-4 text-green-600" fill="currentColor" viewBox="0 0 20 20">
              <path fillRule="evenodd" d="M16.707 5.293a1 1 0 010 1.414l-8 8a1 1 0 01-1.414 0l-4-4a1 1 0 011.414-1.414L8 12.586l7.293-7.293a1 1 0 011.414 0z" clipRule="evenodd" />
            </svg>
          </div>
        )}
        {status === "fail" && (
          <div className="w-7 h-7 rounded-full bg-red-100 flex items-center justify-center">
            <svg className="w-4 h-4 text-red-600" fill="currentColor" viewBox="0 0 20 20">
              <path fillRule="evenodd" d="M4.293 4.293a1 1 0 011.414 0L10 8.586l4.293-4.293a1 1 0 111.414 1.414L11.414 10l4.293 4.293a1 1 0 01-1.414 1.414L10 11.414l-4.293 4.293a1 1 0 01-1.414-1.414L8.586 10 4.293 5.707a1 1 0 010-1.414z" clipRule="evenodd" />
            </svg>
          </div>
        )}
        {status === "loading" && (
          <div className="w-5 h-5 border-2 border-orange-200 border-t-orange-500 rounded-full animate-spin" />
        )}
        {status === "pending" && (
          <div className="w-7 h-7 rounded-full bg-gray-100 flex items-center justify-center">
            <div className="w-2 h-2 rounded-full bg-gray-300" />
          </div>
        )}
      </div>
      <span className={`text-sm ${status === "done" ? "text-gray-700" : status === "fail" ? "text-red-600 font-medium" : status === "loading" ? "text-orange-600 font-medium" : "text-gray-400"}`}>
        {label}
      </span>
    </div>
  );
}
