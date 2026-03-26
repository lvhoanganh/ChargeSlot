import { useState, useEffect } from "react";
import { useNavigate } from "react-router-dom";
import { loyaltyApi } from "@/services/api";

export default function DriverLoyalty() {
  const navigate = useNavigate();
  const [data, setData] = useState(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    loyaltyApi.getInfo()
      .then(setData)
      .catch(() => setData(null))
      .finally(() => setLoading(false));
  }, []);

  if (loading) {
    return (
      <div style={{ minHeight: "100vh", background: "#f8fafc", paddingTop: 100, textAlign: "center" }}>
        <div style={{ fontSize: 40 }}>🏆</div>
        <p style={{ color: "#6b7280" }}>Đang tải điểm thưởng...</p>
      </div>
    );
  }

  if (!data) {
    return (
      <div style={{ minHeight: "100vh", background: "#f8fafc", paddingTop: 100, textAlign: "center" }}>
        <div style={{ fontSize: 48, marginBottom: 8 }}>🏆</div>
        <h2 style={{ fontSize: 20, fontWeight: 700, color: "#1e293b" }}>Không thể tải thông tin điểm thưởng</h2>
        <button onClick={() => navigate(-1)} style={{ marginTop: 16, padding: "10px 20px", borderRadius: 10, border: "none", background: "#f97316", color: "#fff", fontWeight: 600, cursor: "pointer" }}>
          ← Quay lại
        </button>
      </div>
    );
  }

  const history = data.recentHistory || [];

  return (
    <div style={{ minHeight: "100vh", background: "#f8fafc", paddingTop: 90 }}>
      <div style={{ maxWidth: 700, margin: "0 auto", padding: "0 16px 40px" }}>
        {/* Points card */}
        <div style={{
          background: "linear-gradient(135deg, #7c3aed 0%, #6d28d9 50%, #4c1d95 100%)",
          borderRadius: 24, padding: "32px 28px", color: "#fff", marginBottom: 24,
          boxShadow: "0 8px 32px rgba(124,58,237,0.3)",
        }}>
          <p style={{ fontSize: 13, fontWeight: 600, opacity: 0.85, letterSpacing: 1, textTransform: "uppercase" }}>Điểm tích lũy</p>
          <div style={{ fontSize: 42, fontWeight: 800, marginTop: 4, display: "flex", alignItems: "baseline", gap: 8 }}>
            {(data.currentPoints || 0).toLocaleString("vi-VN")}
            <span style={{ fontSize: 16, fontWeight: 600, opacity: 0.7 }}>điểm</span>
          </div>
          <p style={{ fontSize: 12, opacity: 0.7, marginTop: 4 }}>1 điểm = 1 VND</p>

          <div style={{ display: "flex", gap: 10, marginTop: 20, flexWrap: "wrap" }}>
            <div style={{
              padding: "8px 16px", borderRadius: 12,
              background: "rgba(255,255,255,0.15)", backdropFilter: "blur(4px)",
              border: "1px solid rgba(255,255,255,0.2)",
            }}>
              <div style={{ fontSize: 10, opacity: 0.7, textTransform: "uppercase", letterSpacing: 0.5 }}>Tỷ lệ tích</div>
              <div style={{ fontSize: 18, fontWeight: 800 }}>{((data.earnRate || 0) * 100).toFixed(0)}%</div>
            </div>
            <div style={{
              padding: "8px 16px", borderRadius: 12,
              background: "rgba(255,255,255,0.15)", backdropFilter: "blur(4px)",
              border: "1px solid rgba(255,255,255,0.2)",
            }}>
              <div style={{ fontSize: 10, opacity: 0.7, textTransform: "uppercase", letterSpacing: 0.5 }}>Tối đa dùng</div>
              <div style={{ fontSize: 18, fontWeight: 800 }}>{((data.maxRedeemRate || 0) * 100).toFixed(0)}%</div>
            </div>
          </div>
        </div>

        {/* History */}
        <h2 style={{ fontSize: 20, fontWeight: 800, color: "#1e293b", marginBottom: 16 }}>
          Lịch sử điểm
        </h2>

        {history.length === 0 ? (
          <div style={{
            textAlign: "center", padding: 40, background: "#fff", borderRadius: 16,
            boxShadow: "0 2px 8px rgba(0,0,0,0.04)",
          }}>
            <div style={{ fontSize: 48, marginBottom: 8 }}>📋</div>
            <p style={{ color: "#6b7280" }}>Chưa có lịch sử điểm</p>
          </div>
        ) : (
          <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
            {history.map(h => {
              const isEarn = h.type === "Earn";
              return (
                <div key={h.id} style={{
                  background: "#fff", borderRadius: 16, padding: "16px 20px",
                  boxShadow: "0 2px 8px rgba(0,0,0,0.04)", display: "flex",
                  alignItems: "center", gap: 12,
                }}>
                  <div style={{
                    width: 42, height: 42, borderRadius: 12,
                    background: isEarn ? "#f0fdf415" : "#fef2f215",
                    display: "flex", alignItems: "center", justifyContent: "center",
                    fontSize: 20, flexShrink: 0,
                    backgroundColor: isEarn ? "#f0fdf4" : "#fef2f2",
                  }}>
                    {isEarn ? "📈" : "🎁"}
                  </div>
                  <div style={{ flex: 1, minWidth: 0 }}>
                    <div style={{ fontWeight: 700, fontSize: 14, color: "#1e293b" }}>
                      {isEarn ? "Tích điểm" : "Dùng điểm"}
                    </div>
                    <div style={{ fontSize: 12, color: "#94a3b8", marginTop: 2 }}>
                      {h.description || `Booking #${h.bookingId}`}
                    </div>
                    <div style={{ fontSize: 11, color: "#cbd5e1", marginTop: 2 }}>
                      {new Date(String(h.createdAt).replace("Z", "")).toLocaleString("vi-VN")}
                    </div>
                  </div>
                  <div style={{
                    fontWeight: 800, fontSize: 15,
                    color: isEarn ? "#22c55e" : "#ef4444",
                  }}>
                    {isEarn ? "+" : "−"}{Math.abs(h.points).toLocaleString("vi-VN")}
                  </div>
                </div>
              );
            })}
          </div>
        )}
      </div>
    </div>
  );
}
