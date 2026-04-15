import { useState, useEffect } from "react";
import {
  BarChart, Bar, XAxis, YAxis, CartesianGrid, Tooltip,
  ResponsiveContainer, Cell
} from "recharts";
import { instance } from "@/lib/httpRequest";
import { AiAdvisorPanel } from "@/components/AiAdvisorPanel";

const fmt = (n) => (typeof n === "number" ? n.toLocaleString("vi-VN") + "đ" : "—");
const fmtPct = (n) => (typeof n === "number" ? (n * 100).toFixed(1) + "%" : "—");

export default function AdminDashboard() {
  const [metrics, setMetrics] = useState(null);
  const [metricsLoading, setMetricsLoading] = useState(true);
  const [metricsError, setMetricsError] = useState(null);

  useEffect(() => {
    let cancelled = false;
    setMetricsLoading(true);
    instance
      .get("/admin/analytics/metrics")
      .then((res) => { if (!cancelled) setMetrics(res.data); })
      .catch((err) => {
        if (!cancelled)
          setMetricsError(err?.response?.data?.message || err?.message || "Lỗi tải dữ liệu");
      })
      .finally(() => { if (!cancelled) setMetricsLoading(false); });
    return () => { cancelled = true; };
  }, []);

  const cancelRate = metrics?.cancelRateLast30Days ?? 0;
  const isHighCancel = cancelRate > 0.3;

  const riskDrivers = (metrics?.highRiskDrivers || []).map((d) => ({
    name: d.driverName || `ID ${d.driverUserId}`,
    value: d.cancelledBookings || 0,
    totalBookings: d.totalBookings || 0,
  }));

  return (
    <div style={{ minHeight: "100vh", background: "#f1f5f9", paddingTop: 88, paddingBottom: 48 }}>
      <div style={{ maxWidth: 1200, margin: "0 auto", padding: "0 20px" }}>

        {/* Page Header */}
        <div style={{ marginBottom: 28 }}>
          <p style={{ fontSize: 12, fontWeight: 700, textTransform: "uppercase", letterSpacing: "0.15em", color: "#f97316", margin: 0 }}>
            Admin Dashboard
          </p>
          <h1 style={{ fontSize: 28, fontWeight: 800, color: "#0f172a", margin: "6px 0 4px", letterSpacing: "-0.5px" }}>
            Tổng quan hệ thống
          </h1>
          <p style={{ fontSize: 14, color: "#64748b", margin: 0 }}>
            Phân tích hiệu suất và cảnh báo rủi ro nền tảng ChargeSlot
          </p>
        </div>

        {/* Metrics Cards */}
        {metricsLoading ? (
          <div style={{ display: "grid", gridTemplateColumns: "repeat(3, 1fr)", gap: 16, marginBottom: 24 }}>
            {[1, 2, 3].map((i) => (
              <div key={i} style={{ background: "white", borderRadius: 16, padding: 24, height: 100, animation: "dash-pulse 1.5s ease-in-out infinite" }} />
            ))}
          </div>
        ) : metricsError ? (
          <div style={{ background: "#fef2f2", border: "1px solid #fecaca", borderRadius: 14, padding: 20, marginBottom: 24, color: "#dc2626", fontSize: 14 }}>
            ️ {metricsError}
          </div>
        ) : (
          <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(280px, 1fr))", gap: 16, marginBottom: 24 }}>
            <MetricCard
              icon=""
              label="Số dư Escrow (Ký quỹ)"
              value={fmt(metrics?.totalEscrowBalance)}
              sub={
                metrics?.totalEscrowBalance < 0
                  ? "️ Số dư âm — kiểm tra hoàn tiền/refund"
                  : "Tổng tiền đang giữ hộ"
              }
              color={metrics?.totalEscrowBalance < 0 ? "#dc2626" : "#7c3aed"}
              bg={
                metrics?.totalEscrowBalance < 0
                  ? "linear-gradient(135deg, #fef2f2, #fee2e2)"
                  : "linear-gradient(135deg, #f5f3ff, #ede9fe)"
              }
              highlight={metrics?.totalEscrowBalance < 0}
            />
            <MetricCard
              icon=""
              label="Doanh thu nền tảng"
              value={fmt(metrics?.totalPlatformRevenue)}
              sub="Phí & hoa hồng thu được"
              color="#16a34a"
              bg="linear-gradient(135deg, #f0fdf4, #dcfce7)"
            />
            <MetricCard
              icon=""
              label="Tỷ lệ hủy (30 ngày)"
              value={fmtPct(cancelRate)}
              sub={isHighCancel ? "️ Vượt ngưỡng 30% — cần chú ý!" : " Trong ngưỡng kiểm soát"}
              color={isHighCancel ? "#dc2626" : "#16a34a"}
              bg={isHighCancel ? "linear-gradient(135deg, #fef2f2, #fee2e2)" : "linear-gradient(135deg, #f0fdf4, #dcfce7)"}
              highlight={isHighCancel}
            />
          </div>
        )}

        {/* Charts Row */}
        <div style={{ display: "grid", gridTemplateColumns: "1fr", gap: 20, marginBottom: 24 }}>
          {/* High Risk Drivers */}
          <div style={{ background: "white", borderRadius: 20, padding: "24px 28px", boxShadow: "0 2px 12px rgba(0,0,0,0.05)", border: "1px solid rgba(0,0,0,0.06)" }}>
            <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", marginBottom: 20 }}>
              <div>
                <h3 style={{ fontSize: 17, fontWeight: 700, color: "#0f172a", margin: 0 }}>
                   Danh sách Tài xế rủi ro cao
                </h3>
                <p style={{ fontSize: 13, color: "#64748b", margin: "4px 0 0" }}>
                  Tài xế có lịch sử lạm dụng hủy chỗ (tỉ lệ hủy &gt; 50%)
                </p>
              </div>
              <span style={{ fontSize: 12, background: "#fef2f2", color: "#dc2626", border: "1px solid #fecaca", padding: "4px 10px", borderRadius: 20, fontWeight: 600 }}>
                {riskDrivers.length} tài xế
              </span>
            </div>
            {riskDrivers.length === 0 ? (
              <div style={{ textAlign: "center", padding: "40px 20px" }}>
                <div style={{ fontSize: 40, marginBottom: 10, opacity: 0.3 }}></div>
                <p style={{ color: "#94a3b8", fontSize: 14 }}>
                  {metricsLoading ? "Đang tải..." : "Không có tài xế rủi ro cao"}
                </p>
              </div>
            ) : (
              <ResponsiveContainer width="100%" height={260}>
                <BarChart data={riskDrivers} margin={{ top: 5, right: 20, bottom: 40, left: 10 }}>
                  <CartesianGrid strokeDasharray="3 3" stroke="#f1f5f9" />
                  <XAxis
                    dataKey="name"
                    tick={{ fontSize: 11, fill: "#64748b" }}
                    angle={-30}
                    textAnchor="end"
                    interval={0}
                  />
                  <YAxis tick={{ fontSize: 12, fill: "#64748b" }} />
                  <Tooltip
                    contentStyle={{ borderRadius: 10, border: "none", boxShadow: "0 8px 24px rgba(0,0,0,0.12)", fontSize: 13 }}
                    formatter={(val, name, props) => {
                      if (name === "value") return [val + ` / ${props.payload.totalBookings} đơn`, "Số lần hủy"];
                      return [val, name];
                    }}
                  />
                  <Bar dataKey="value" radius={[6, 6, 0, 0]} maxBarSize={50}>
                    {riskDrivers.map((_, idx) => (
                      <Cell
                        key={idx}
                        fill={idx === 0 ? "#dc2626" : idx <= 2 ? "#f97316" : "#f59e0b"}
                      />
                    ))}
                  </Bar>
                </BarChart>
              </ResponsiveContainer>
            )}
          </div>
        </div>

        {/* AI Advisor Panel */}
        <AiAdvisorPanel role="admin" />

      </div>

      <style>{`
        @keyframes dash-pulse {
          0%, 100% { opacity: 0.5; }
          50% { opacity: 1; }
        }
      `}</style>
    </div>
  );
}

function MetricCard({ icon, label, value, sub, color, bg, highlight }) {
  return (
    <div style={{
      background: bg,
      borderRadius: 18,
      padding: "22px 24px",
      boxShadow: highlight
        ? "0 4px 20px rgba(220,38,38,0.15)"
        : "0 2px 12px rgba(0,0,0,0.05)",
      border: highlight ? "1px solid rgba(220,38,38,0.3)" : "1px solid rgba(0,0,0,0.05)",
      transition: "transform 0.2s, box-shadow 0.2s",
      cursor: "default",
    }}
      onMouseEnter={(e) => { e.currentTarget.style.transform = "translateY(-2px)"; e.currentTarget.style.boxShadow = "0 8px 28px rgba(0,0,0,0.1)"; }}
      onMouseLeave={(e) => { e.currentTarget.style.transform = "translateY(0)"; e.currentTarget.style.boxShadow = highlight ? "0 4px 20px rgba(220,38,38,0.15)" : "0 2px 12px rgba(0,0,0,0.05)"; }}
    >
      <div style={{ display: "flex", alignItems: "center", gap: 8, marginBottom: 12 }}>
        <span style={{ fontSize: 24 }}>{icon}</span>
        <span style={{ fontSize: 13, fontWeight: 600, color: "#64748b" }}>{label}</span>
      </div>
      <p style={{ fontSize: 26, fontWeight: 800, color, margin: "0 0 4px", letterSpacing: "-0.5px" }}>{value}</p>
      {sub && <p style={{ fontSize: 12, color: color, margin: 0, opacity: 0.8 }}>{sub}</p>}
    </div>
  );
}
