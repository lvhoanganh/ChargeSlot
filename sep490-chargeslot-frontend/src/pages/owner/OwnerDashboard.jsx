import { useState, useEffect } from "react";
import {
  BarChart, Bar, XAxis, YAxis, CartesianGrid, Tooltip,
  ResponsiveContainer, Cell
} from "recharts";
import { instance } from "@/lib/httpRequest";
import { AiAdvisorPanel } from "@/components/AiAdvisorPanel";

const fmt = (n) => (typeof n === "number" ? n.toLocaleString("vi-VN") + "đ" : "—");
const fmtNum = (n) => (typeof n === "number" ? n.toLocaleString("vi-VN") : "—");

const STATION_COLORS = ["#f97316", "#fb923c", "#fdba74", "#fed7aa", "#fde68a"];

export default function OwnerDashboard() {
  const [metrics, setMetrics] = useState(null);
  const [metricsLoading, setMetricsLoading] = useState(true);
  const [metricsError, setMetricsError] = useState(null);

  useEffect(() => {
    let cancelled = false;
    setMetricsLoading(true);
    instance
      .get("/owner/analytics/metrics")
      .then((res) => { if (!cancelled) setMetrics(res.data); })
      .catch((err) => {
        if (!cancelled)
          setMetricsError(err?.response?.data?.message || err?.message || "Lỗi tải dữ liệu");
      })
      .finally(() => { if (!cancelled) setMetricsLoading(false); });
    return () => { cancelled = true; };
  }, []);

  // Hỗ trợ cả 2 tên key có thể từ BE
  const stationPerformances = (
    metrics?.stationPerformances ||
    metrics?.stationPerformance ||
    []
  ).map((s) => ({
    name: s.stationName || s.name || "Chưa rõ",
    revenue: s.revenue || s.totalRevenue || 0,
    sessions: s.totalSessions || s.sessionCount || 0,
    rating: s.averageRating ?? s.rating ?? 0,
  }));

  // Tính average rating từ các trạm có rating thực tế
  const ratedStations = stationPerformances.filter((s) => s.rating > 0);
  const avgRating = ratedStations.length > 0
    ? ratedStations.reduce((sum, s) => sum + s.rating, 0) / ratedStations.length
    : null;
  const starDisplay = avgRating !== null ? `${Number(avgRating).toFixed(1)} ⭐` : "—";

  return (
    <div style={{ minHeight: "100vh", background: "#f8fafc", paddingTop: 88, paddingBottom: 48 }}>
      <div style={{ maxWidth: 1200, margin: "0 auto", padding: "0 20px" }}>

        {/* Page Header */}
        <div style={{ marginBottom: 28 }}>
          <p style={{ fontSize: 12, fontWeight: 700, textTransform: "uppercase", letterSpacing: "0.15em", color: "#f97316", margin: 0 }}>
            Owner Dashboard
          </p>
          <h1 style={{ fontSize: 28, fontWeight: 800, color: "#0f172a", margin: "6px 0 4px", letterSpacing: "-0.5px" }}>
            Tổng quan kinh doanh
          </h1>
          <p style={{ fontSize: 14, color: "#64748b", margin: 0 }}>
            Hiệu suất trạm sạc và phân tích chiến lược của bạn
          </p>
        </div>

        {/* Metrics Cards */}
        {metricsLoading ? (
          <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(250px, 1fr))", gap: 16, marginBottom: 24 }}>
            {[1, 2, 3].map((i) => (
              <div key={i} style={{ background: "white", borderRadius: 16, padding: 24, height: 100, animation: "dash-pulse 1.5s ease-in-out infinite" }} />
            ))}
          </div>
        ) : metricsError ? (
          <div style={{ background: "#fef2f2", border: "1px solid #fecaca", borderRadius: 14, padding: 20, marginBottom: 24, color: "#dc2626", fontSize: 14 }}>
            ⚠️ {metricsError}
          </div>
        ) : (
          <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(250px, 1fr))", gap: 16, marginBottom: 24 }}>
            <MetricCard
              icon="💵"
              label="Doanh thu 30 ngày"
              value={fmt(metrics?.revenueLast30Days)}
              sub="Tổng tiền đã thu từ tất cả trạm"
              color="#16a34a"
              bg="linear-gradient(135deg, #f0fdf4, #dcfce7)"
            />
            <MetricCard
              icon="⭐"
              label="Đánh giá trung bình"
              value={starDisplay}
              sub="Trung bình rating từ tất cả trạm"
              color="#d97706"
              bg="linear-gradient(135deg, #fffbeb, #fef3c7)"
            />
            <MetricCard
              icon="📋"
              label="Đơn đặt 30 ngày"
              value={fmtNum(metrics?.bookingsLast30Days)}
              sub="Tổng booking trong 30 ngày qua"
              color="#2563eb"
              bg="linear-gradient(135deg, #eff6ff, #dbeafe)"
            />
          </div>
        )}

        {/* Station Performance Chart */}
        <div style={{ background: "white", borderRadius: 20, padding: "24px 28px", boxShadow: "0 2px 12px rgba(0,0,0,0.05)", border: "1px solid rgba(0,0,0,0.06)", marginBottom: 24 }}>
          <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", marginBottom: 20 }}>
            <div>
              <h3 style={{ fontSize: 17, fontWeight: 700, color: "#0f172a", margin: 0 }}>
                📊 So sánh doanh thu các trạm
              </h3>
              <p style={{ fontSize: 13, color: "#64748b", margin: "4px 0 0" }}>
                Hiệu suất kinh doanh từng trạm sạc của bạn
              </p>
            </div>
            <span style={{ fontSize: 12, background: "#fff7ed", color: "#f97316", border: "1px solid #fed7aa", padding: "4px 10px", borderRadius: 20, fontWeight: 600 }}>
              {stationPerformances.length} trạm
            </span>
          </div>

          {stationPerformances.length === 0 ? (
            <div style={{ textAlign: "center", padding: "40px 20px" }}>
              <div style={{ fontSize: 40, marginBottom: 10, opacity: 0.3 }}>📊</div>
              <p style={{ color: "#94a3b8", fontSize: 14 }}>
                {metricsLoading ? "Đang tải..." : "Chưa có dữ liệu hiệu suất trạm"}
              </p>
            </div>
          ) : (
            <ResponsiveContainer width="100%" height={280}>
              <BarChart data={stationPerformances} margin={{ top: 5, right: 20, bottom: 50, left: 20 }}>
                <CartesianGrid strokeDasharray="3 3" stroke="#f1f5f9" />
                <XAxis
                  dataKey="name"
                  tick={{ fontSize: 11, fill: "#64748b" }}
                  angle={-25}
                  textAnchor="end"
                  interval={0}
                />
                <YAxis
                  tick={{ fontSize: 11, fill: "#64748b" }}
                  tickFormatter={(v) => `${(v / 1000000).toFixed(1)}M`}
                />
                <Tooltip
                  contentStyle={{ borderRadius: 10, border: "none", boxShadow: "0 8px 24px rgba(0,0,0,0.12)", fontSize: 13 }}
                  formatter={(val, name) => [
                    name === "revenue"
                      ? `${val.toLocaleString("vi-VN")}đ`
                      : val,
                    name === "revenue" ? "Doanh thu" : "Phiên sạc",
                  ]}
                />
                <Bar dataKey="revenue" radius={[8, 8, 0, 0]} maxBarSize={60}>
                  {stationPerformances.map((_, idx) => (
                    <Cell
                      key={idx}
                      fill={STATION_COLORS[idx % STATION_COLORS.length]}
                    />
                  ))}
                </Bar>
              </BarChart>
            </ResponsiveContainer>
          )}
        </div>

        {/* AI Advisor Panel */}
        <AiAdvisorPanel role="owner" />

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

function MetricCard({ icon, label, value, sub, color, bg }) {
  return (
    <div style={{
      background: bg,
      borderRadius: 18,
      padding: "22px 24px",
      boxShadow: "0 2px 12px rgba(0,0,0,0.05)",
      border: "1px solid rgba(0,0,0,0.05)",
      transition: "transform 0.2s, box-shadow 0.2s",
      cursor: "default",
    }}
      onMouseEnter={(e) => { e.currentTarget.style.transform = "translateY(-2px)"; e.currentTarget.style.boxShadow = "0 8px 28px rgba(0,0,0,0.1)"; }}
      onMouseLeave={(e) => { e.currentTarget.style.transform = "translateY(0)"; e.currentTarget.style.boxShadow = "0 2px 12px rgba(0,0,0,0.05)"; }}
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
