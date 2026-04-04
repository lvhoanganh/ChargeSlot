import { useEffect } from "react";

export default function QRCodeModal({ isOpen, onClose, qrUrl, title, description, amount, isBookingPayment = false }) {
  // Hủy cuộc gọi khi press Escape
  useEffect(() => {
    const handleEsc = (e) => {
      if (e.key === "Escape") onClose();
    };
    window.addEventListener("keydown", handleEsc);
    return () => window.removeEventListener("keydown", handleEsc);
  }, [onClose]);

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 z-[60] flex items-center justify-center p-4 bg-black/50 backdrop-blur-sm transition-opacity">
      <div className="bg-white rounded-2xl shadow-xl w-full max-w-sm overflow-hidden animate-in fade-in zoom-in-95 duration-200">
        {/* Header */}
        <div className="px-6 py-4 border-b border-gray-100 flex items-center justify-between bg-gray-50/50">
          <h3 className="text-lg font-bold text-gray-800">{title || "Thanh toán VietQR"}</h3>
          <button
            onClick={onClose}
            className="p-1.5 text-gray-400 hover:text-gray-600 hover:bg-gray-100 rounded-lg transition-colors cursor-pointer"
          >
            <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
            </svg>
          </button>
        </div>

        {/* Body */}
        <div className="p-6 text-center">
          {description && (
            <p className="text-sm text-gray-600 mb-4 bg-orange-50 text-orange-700 px-4 py-2 rounded-lg font-medium">
              {description}
            </p>
          )}

          {/* ⚠️ Critical warning for booking payments */}
          {isBookingPayment && (
            <div style={{
              background: "linear-gradient(135deg, #fef2f2, #fee2e2)",
              border: "1.5px solid #fca5a5",
              borderRadius: 12,
              padding: "10px 14px",
              marginBottom: 16,
              textAlign: "left",
            }}>
              <div style={{ display: "flex", alignItems: "flex-start", gap: 8 }}>
                <span style={{ fontSize: 18, flexShrink: 0 }}>⚠️</span>
                <div style={{ fontSize: 12, color: "#991b1b", lineHeight: 1.5 }}>
                  <strong>Lưu ý quan trọng:</strong>
                  <ul style={{ margin: "4px 0 0 0", paddingLeft: 16 }}>
                    <li>Chuyển <strong>đúng số tiền</strong> hiển thị bên dưới — không thêm/bớt.</li>
                    <li><strong>Không sửa nội dung</strong> chuyển khoản (đã được điền sẵn trong mã QR).</li>
                    <li>Nếu chuyển sai, tiền sẽ vào <strong>ví ChargeSlot</strong> của bạn, lịch đặt chưa được xác nhận.</li>
                  </ul>
                </div>
              </div>
            </div>
          )}
          
          <div className="bg-white p-4 rounded-xl border-2 border-dashed border-gray-200 mx-auto max-w-[260px] aspect-square relative flex items-center justify-center mb-4 shadow-sm group">
            <div className="absolute inset-0 bg-blue-50/80 opacity-0 group-hover:opacity-100 transition-opacity rounded-lg flex items-center justify-center backdrop-blur-sm z-10">
               <a href={qrUrl} download="Ma_QR_ChargeSlot.png" className="bg-white text-blue-600 px-4 py-2 rounded-full font-semibold shadow-lg border border-blue-100 hover:bg-blue-50 transition-colors text-sm flex items-center gap-2 cursor-pointer">
                 <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-4l-4 4m0 0l-4-4m4 4V4" /></svg>
                 Tải mã QR
               </a>
            </div>
            {qrUrl ? (
              <img
                src={qrUrl}
                alt="QR Code"
                className="w-full h-full object-contain mix-blend-multiply relative z-0"
              />
            ) : (
              <div className="w-10 h-10 border-4 border-orange-200 border-t-orange-500 rounded-full animate-spin relative z-0" />
            )}
          </div>
          
          {amount && (
            <div className="mb-4">
              <span className="text-sm text-gray-500">Số tiền thanh toán: </span>
              <span className="text-2xl font-black text-orange-600">{new Intl.NumberFormat("vi-VN", { style: "currency", currency: "VND" }).format(amount)}</span>
            </div>
          )}

          <div className="text-xs text-gray-500 mt-4 leading-relaxed px-2 flex flex-col items-center gap-2">
            <p>Mở ứng dụng Ngân hàng trên điện thoại của bạn, chọn <b>Quét mã QR</b> để thanh toán.</p>
            <div className="flex items-center gap-1.5 text-blue-600 font-medium bg-blue-50 px-3 py-1.5 rounded-full">
              <div className="w-3 h-3 border-2 border-blue-300 border-t-blue-600 rounded-full animate-spin"></div>
              <span>Đang chờ thanh toán...</span>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
