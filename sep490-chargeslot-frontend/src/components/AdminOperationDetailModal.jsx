import { useState, useEffect } from "react";
import { bookingApi } from "@/services/api";

function toLocal(dt) {
  if (!dt) return "—";
  const s = String(dt).trim().replace("Z", "");
  const d = new Date(s.includes("+") || s.includes("-", 10) ? s : s + "+07:00");
  if (isNaN(d)) return "—";
  return d.toLocaleString("vi-VN", {
    hour: "2-digit", minute: "2-digit",
    day: "2-digit", month: "2-digit", year: "numeric",
    hour12: false,
  });
}

function getStatusLabel(status) {
  switch (status) {
    case "PendingPayment": return "Chờ thanh toán";
    case "Paid": return "Đã thanh toán";
    case "CheckedIn": return "Đang sạc";
    case "CompletedPendingInvoice": return "Chờ xuất HĐ";
    case "Completed": return "Hoàn tất";
    case "Cancelled": return "Đã hủy";
    case "Rejected": return "Từ chối";
    case "Expired": return "Quá hạn";
    default: return status;
  }
}

export default function AdminOperationDetailModal({ bookingId, onClose }) {
  const [data, setData] = useState(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    setLoading(true);
    bookingApi.getById(Number(bookingId))
      .then(setData)
      .catch((err) => console.error("Lỗi lấy chi tiết booking", err))
      .finally(() => setLoading(false));
  }, [bookingId]);

  return (
    <div style={{
      position: "fixed", inset: 0, zIndex: 9999,
      background: "rgba(0,0,0,0.55)", backdropFilter: "blur(4px)",
      display: "flex", alignItems: "center", justifyContent: "center", padding: 24
    }} onClick={onClose}>
      <div 
        style={{
          background: "#fff", borderRadius: 24, padding: 32,
          maxWidth: 500, width: "100%", maxHeight: "90vh", overflowY: "auto",
          boxShadow: "0 24px 64px rgba(0,0,0,0.2)"
        }}
        onClick={(e) => e.stopPropagation()}
      >
        <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 24 }}>
          <h2 style={{ fontSize: 22, fontWeight: 800, margin: 0, color: "#1e293b" }}>Chi tiết Đặt chỗ #{bookingId}</h2>
          <button onClick={onClose} style={{
            width: 32, height: 32, borderRadius: "50%", border: "none", background: "#f1f5f9",
            color: "#64748b", fontWeight: "bold", fontSize: 18, cursor: "pointer",
            display: "flex", alignItems: "center", justifyContent: "center"
          }}>
            &times;
          </button>
        </div>

        {loading ? (
          <div style={{ textAlign: "center", padding: "40px 0" }}>
            <div style={{ width: 32, height: 32, border: "3px solid #f1f5f9", borderTopColor: "#f97316", borderRadius: "50%", animation: "spin 1s linear infinite", margin: "0 auto 16px" }}></div>
            <style>{`@keyframes spin { to { transform: rotate(360deg); } }`}</style>
            <p style={{ color: "#64748b", margin: 0, fontSize: 14 }}>Đang tải dữ liệu...</p>
          </div>
        ) : !data ? (
          <p style={{ color: "#ef4444", textAlign: "center", margin: 0 }}>Không tìm thấy thông tin</p>
        ) : (
          <div style={{ display: "flex", flexDirection: "column", gap: 12 }}>
            <div style={{ background: "#f8fafc", padding: 16, borderRadius: 16, display: "flex", flexDirection: "column", gap: 12 }}>
              <div style={{ display: "flex", justifyContent: "space-between" }}>
                <span style={{ color: "#64748b", fontSize: 14 }}>Khách hàng:</span>
                <span style={{ fontWeight: 600, color: "#1e293b", fontSize: 14 }}>{data.driverName || "N/A"}</span>
              </div>
              <div style={{ display: "flex", justifyContent: "space-between" }}>
                <span style={{ color: "#64748b", fontSize: 14 }}>Trạm sạc:</span>
                <span style={{ fontWeight: 600, color: "#1e293b", fontSize: 14 }}>{data.stationName || "N/A"}</span>
              </div>
              <div style={{ display: "flex", justifyContent: "space-between" }}>
                <span style={{ color: "#64748b", fontSize: 14 }}>Slot:</span>
                <span style={{ fontWeight: 600, color: "#1e293b", fontSize: 14 }}>{data.slotName || "N/A"}</span>
              </div>
            </div>

            <div style={{ background: "#f8fafc", padding: 16, borderRadius: 16, display: "flex", flexDirection: "column", gap: 12 }}>
              <div style={{ display: "flex", justifyContent: "space-between" }}>
                <span style={{ color: "#64748b", fontSize: 14 }}>Khung giờ:</span>
                <span style={{ fontWeight: 600, color: "#1e293b", fontSize: 14 }}>
                  {toLocal(data.startTime)} - {toLocal(data.endTime)}
                </span>
              </div>
              <div style={{ display: "flex", justifyContent: "space-between" }}>
                <span style={{ color: "#64748b", fontSize: 14 }}>Thời lượng:</span>
                <span style={{ fontWeight: 600, color: "#1e293b", fontSize: 14 }}>
                  {data.durationHours ? Math.round(data.durationHours * 60) : 0} Phút
                </span>
              </div>
              <div style={{ display: "flex", justifyContent: "space-between" }}>
                <span style={{ color: "#64748b", fontSize: 14 }}>Giá trị:</span>
                <span style={{ fontWeight: 800, color: "#f97316", fontSize: 16 }}>
                  {data.totalAmount?.toLocaleString("vi-VN") || "0"} ₫
                </span>
              </div>
              <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
                <span style={{ color: "#64748b", fontSize: 14 }}>Trạng thái:</span>
                <span style={{ 
                  fontWeight: 700, padding: "4px 10px", borderRadius: 8, fontSize: 12,
                  background: data.status === "PendingPayment" ? "#fffbeb" : 
                              data.status === "Paid" ? "#f0fdf4" : "#f1f5f9",
                  color: data.status === "PendingPayment" ? "#d97706" : 
                         data.status === "Paid" ? "#16a34a" : "#475569" 
                }}>
                  {getStatusLabel(data.status)}
                </span>
              </div>
            </div>

            {data.note && (
              <div style={{ background: "#f8fafc", padding: 16, borderRadius: 16 }}>
                <p style={{ color: "#64748b", fontSize: 14, margin: "0 0 4px" }}>Ghi chú:</p>
                <p style={{ fontWeight: 500, color: "#333", fontSize: 14, margin: 0 }}>{data.note}</p>
              </div>
            )}
          </div>
        )}
      </div>
    </div>
  );
}
