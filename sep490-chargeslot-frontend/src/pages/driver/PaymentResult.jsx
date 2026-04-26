import { useEffect } from "react";
import { useNavigate, useSearchParams } from "react-router-dom";

export default function PaymentResult() {
  const [params] = useSearchParams();
  const navigate = useNavigate();
  const success = params.get("success") === "True" || params.get("success") === "true";
  const code = params.get("responseCode");

  return (
    <div style={{ minHeight: "100vh", background: "#f8fafc", display: "flex", alignItems: "center", justifyContent: "center" }}>
      <div style={{ background: "#fff", borderRadius: 24, boxShadow: "0 4px 24px rgba(0,0,0,0.08)", padding: 40, maxWidth: 420, width: "100%", textAlign: "center" }}>
        <div style={{ fontSize: 64, marginBottom: 16 }}>{success ? "" : ""}</div>
        <h1 style={{ fontSize: 22, fontWeight: 800, color: success ? "#22c55e" : "#ef4444", marginBottom: 8 }}>
          {success ? "Thanh toán thành công!" : "Thanh toán thất bại"}
        </h1>
        <p style={{ fontSize: 14, color: "#64748b", marginBottom: 24 }}>
          {success ? "Booking của bạn đã được xác nhận." : `Mã lỗi: ${code || "Unknown"}`}
        </p>
        <div style={{ display: "flex", gap: 8, justifyContent: "center" }}>
          <button onClick={() => navigate("/driver/my-bookings")} style={{ padding: "12px 20px", borderRadius: 12, border: "none", background: "#f97316", color: "#fff", fontWeight: 600, cursor: "pointer" }}>
             Xem booking
          </button>
          <button onClick={() => navigate("/driver/map")} style={{ padding: "12px 20px", borderRadius: 12, border: "1.5px solid #e5e7eb", background: "#fff", color: "#374151", fontWeight: 600, cursor: "pointer" }}>
            ️ Bản đồ
          </button>
        </div>
      </div>
    </div>
  );
}
