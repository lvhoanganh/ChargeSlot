import { useState, useEffect } from "react";
import {
  BarChart, Bar, XAxis, YAxis, CartesianGrid, Tooltip,
  ResponsiveContainer, Cell
} from "recharts";
import { Banknote, Zap, CheckCircle, XCircle, Star, BarChart3 } from "lucide-react";
import { instance } from "@/lib/httpRequest";
import { ownerAnalyticsApi } from "@/services/api";

//  Helpers 
const fmt = (n) => (typeof n === "number" ? n.toLocaleString("vi-VN") + "đ" : "—");
const fmtNum = (n) => (typeof n === "number" ? n.toLocaleString("vi-VN") : "—");

const STATION_COLORS = ["#f97316", "#fb923c", "#3b82f6", "#22c55e", "#a855f7", "#ef4444"];

const PERIODS = [
  { label: "7 ngày", days: 7 },
  { label: "30 ngày", days: 30 },
  { label: "3 tháng", days: 90 },
  { label: "6 tháng", days: 180 },
  { label: "Năm nay", days: 365 },
];

function getDateRange(days) {
  const to = new Date();
  const from = new Date();
  from.setDate(from.getDate() - days);
  return {
    fromDate: from.toISOString().split("T")[0],
    toDate: to.toISOString().split("T")[0],
  };
}

//  Sub-components 
function MetricCard({ icon, label, value, sub, color, bg }) {
  return (
    <div
      style={{
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
      {sub && <p style={{ fontSize: 12, color, margin: 0, opacity: 0.8 }}>{sub}</p>}
    </div>
  );
}

function StatCard({ icon, label, value, sub, color = "#f97316", bg = "#fff7ed" }) {
  return (
    <div style={{
      background: "#fff", borderRadius: 20, padding: "20px 22px",
      boxShadow: "0 2px 12px rgba(0,0,0,0.06)", border: "1px solid #f1f5f9",
      display: "flex", alignItems: "center", gap: 16, flex: "1 1 200px",
    }}>
      <div style={{
        width: 52, height: 52, borderRadius: 14, background: bg,
        display: "flex", alignItems: "center", justifyContent: "center",
        fontSize: 24, flexShrink: 0,
      }}>
        {icon}
      </div>
      <div style={{ flex: 1, minWidth: 0 }}>
        <p style={{ fontSize: 12, color: "#94a3b8", fontWeight: 600, margin: 0 }}>{label}</p>
        <p style={{ fontSize: 24, fontWeight: 800, color: "#1e293b", margin: "2px 0 0" }}>{value ?? "—"}</p>
        {sub && <p style={{ fontSize: 11, color: "#64748b", margin: 0 }}>{sub}</p>}
      </div>
    </div>
  );
}

function MiniBar({ label, value, max, color = "#f97316" }) {
  const pct = max > 0 ? Math.min(100, (value / max) * 100) : 0;
  return (
    <div style={{ display: "flex", alignItems: "center", gap: 10, marginBottom: 8 }}>
      <div style={{ width: 110, fontSize: 12, color: "#64748b", fontWeight: 600, flexShrink: 0, textAlign: "right" }}>{label}</div>
      <div style={{ flex: 1, background: "#f1f5f9", borderRadius: 99, height: 10, overflow: "hidden" }}>
        <div style={{ width: `${pct}%`, height: "100%", background: color, borderRadius: 99, transition: "width 0.5s" }} />
      </div>
      <div style={{ width: 80, fontSize: 12, fontWeight: 700, color: "#1e293b", textAlign: "right" }}>
        {typeof value === "number" ? value.toLocaleString("vi-VN") : value}
      </div>
    </div>
  );
}

//  Tab: Tổng quan 
function TabOverview() {
  const [metrics, setMetrics] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    instance
      .get("/owner/analytics/metrics")
      .then((res) => { if (!cancelled) setMetrics(res.data); })
      .catch((err) => {
        if (!cancelled)
          setError(err?.response?.data?.message || err?.message || "Lỗi tải dữ liệu");
      })
      .finally(() => { if (!cancelled) setLoading(false); });
    return () => { cancelled = true; };
  }, []);

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

  const ratedStations = stationPerformances.filter((s) => s.rating > 0);
  const avgRating = ratedStations.length > 0
    ? ratedStations.reduce((sum, s) => sum + s.rating, 0) / ratedStations.length
    : null;
  const starDisplay = avgRating !== null ? `${Number(avgRating).toFixed(1)} ` : "—";

  if (loading) return (
    <div>
      <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(250px, 1fr))", gap: 16, marginBottom: 24 }}>
        {[1, 2, 3].map((i) => (
          <div key={i} style={{ background: "white", borderRadius: 16, padding: 24, height: 100, animation: "dash-pulse 1.5s ease-in-out infinite" }} />
        ))}
      </div>
    </div>
  );

  if (error) return (
    <div style={{ background: "#fef2f2", border: "1px solid #fecaca", borderRadius: 14, padding: 20, color: "#dc2626", fontSize: 14 }}>
      ️ {error}
    </div>
  );

  return (
    <div>
      {/* Metrics Cards */}
      <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(250px, 1fr))", gap: 16, marginBottom: 24 }}>
        <MetricCard
          icon={<Banknote size={24} color="#16a34a" />} label="Doanh thu 30 ngày"
          value={fmt(metrics?.revenueLast30Days)}
          sub="Tổng tiền đã thu từ tất cả trạm"
          color="#16a34a" bg="linear-gradient(135deg, #f0fdf4, #dcfce7)"
        />
        <MetricCard
          icon={<Star size={24} color="#d97706" />} label="Đánh giá trung bình"
          value={starDisplay}
          sub="Trung bình rating từ tất cả trạm"
          color="#d97706" bg="linear-gradient(135deg, #fffbeb, #fef3c7)"
        />
        <MetricCard
          icon={<Zap size={24} color="#2563eb" />} label="Đơn đặt 30 ngày"
          value={fmtNum(metrics?.bookingsLast30Days)}
          sub="Tổng booking trong 30 ngày qua"
          color="#2563eb" bg="linear-gradient(135deg, #eff6ff, #dbeafe)"
        />
      </div>

      {/* Station Performance Chart */}
      <div style={{ background: "white", borderRadius: 20, padding: "24px 28px", boxShadow: "0 2px 12px rgba(0,0,0,0.05)", border: "1px solid rgba(0,0,0,0.06)", marginBottom: 24 }}>
        <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", marginBottom: 20 }}>
          <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
            <BarChart3 size={20} color="#0f172a" />
            <h3 style={{ fontSize: 17, fontWeight: 700, color: "#0f172a", margin: 0 }}>
               So sánh doanh thu các trạm
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
            <div style={{ fontSize: 40, marginBottom: 10, opacity: 0.3 }}></div>
            <p style={{ color: "#94a3b8", fontSize: 14 }}>Chưa có dữ liệu hiệu suất trạm</p>
          </div>
        ) : (
          <ResponsiveContainer width="100%" height={280}>
            <BarChart data={stationPerformances} margin={{ top: 5, right: 20, bottom: 50, left: 20 }}>
              <CartesianGrid strokeDasharray="3 3" stroke="#f1f5f9" />
              <XAxis dataKey="name" tick={{ fontSize: 11, fill: "#64748b" }} angle={-25} textAnchor="end" interval={0} />
              <YAxis tick={{ fontSize: 11, fill: "#64748b" }} tickFormatter={(v) => `${(v / 1000000).toFixed(1)}M`} />
              <Tooltip
                contentStyle={{ borderRadius: 10, border: "none", boxShadow: "0 8px 24px rgba(0,0,0,0.12)", fontSize: 13 }}
                formatter={(val, name) => [
                  name === "revenue" ? `${val.toLocaleString("vi-VN")}đ` : val,
                  name === "revenue" ? "Doanh thu" : "Phiên sạc",
                ]}
              />
              <Bar dataKey="revenue" radius={[8, 8, 0, 0]} maxBarSize={60}>
                {stationPerformances.map((_, idx) => (
                  <Cell key={idx} fill={STATION_COLORS[idx % STATION_COLORS.length]} />
                ))}
              </Bar>
            </BarChart>
          </ResponsiveContainer>
        )}
      </div>
    </div>
  );
}

//  Tab: Thống kê chi tiết 
function TabAnalytics() {
  const [periodIdx, setPeriodIdx] = useState(1);
  const [metrics, setMetrics] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  useEffect(() => {
    const { fromDate, toDate } = getDateRange(PERIODS[periodIdx].days);
    setLoading(true);
    setError("");
    setMetrics(null);

    ownerAnalyticsApi.getMetrics(fromDate, toDate)
      .then((data) => setMetrics(data))
      .catch((err) => setError(err.message || "Không tải được dữ liệu"))
      .finally(() => setLoading(false));
  }, [periodIdx]);

  const m = metrics;
  const stationPerfs = m?.stationPerformances || [];
  const maxRevenue = Math.max(1, ...stationPerfs.map((s) => Number(s.totalRevenue) || 0));
  const maxBookings = Math.max(1, ...stationPerfs.map((s) => s.totalBookings || 0));
  const topServices = m?.topServicesSold || [];
  const maxSvcRev = Math.max(1, ...topServices.map((s) => Number(s.revenue) || 0));

  return (
    <div>
      {/* Period selector */}
      <div style={{ display: "flex", gap: 8, marginBottom: 24, flexWrap: "wrap" }}>
        {PERIODS.map((p, i) => (
          <button key={i} onClick={() => setPeriodIdx(i)} style={{
            padding: "8px 18px", borderRadius: 12, fontSize: 13, fontWeight: 700, cursor: "pointer", transition: "all 0.15s",
            border: i === periodIdx ? "2px solid #f97316" : "1.5px solid #e2e8f0",
            background: i === periodIdx ? "#f97316" : "#fff",
            color: i === periodIdx ? "#fff" : "#64748b",
            boxShadow: i === periodIdx ? "0 2px 8px rgba(249,115,22,0.3)" : "none",
          }}>
            {p.label}
          </button>
        ))}
      </div>

      {error && (
        <div style={{ background: "#fef2f2", border: "1px solid #fca5a5", borderRadius: 12, padding: "14px 18px", color: "#dc2626", marginBottom: 20, fontSize: 14 }}>
          ️ {error}
        </div>
      )}

      {loading && (
        <div style={{ textAlign: "center", paddingTop: 60, color: "#94a3b8" }}>
          <div style={{ fontSize: 48, marginBottom: 8 }}></div>
          <p>Đang tải dữ liệu...</p>
        </div>
      )}

      {!loading && m && (
        <>
          {/* Stat Cards */}
          <div style={{ display: "flex", flexWrap: "wrap", gap: 16, marginBottom: 12 }}>
            <StatCard icon={<Banknote size={24} />} label="Doanh thu" color="#f97316" bg="#fff7ed"
              value={`${(m.revenueLast30Days ?? 0).toLocaleString("vi-VN")}đ`}
              sub={`Số dư ví: ${(m.walletBalance ?? 0).toLocaleString("vi-VN")}đ`}
            />
            <StatCard icon={<Zap size={24} />} label="Lượt booking" color="#3b82f6" bg="#eff6ff"
              value={m.bookingsLast30Days ?? 0}
              sub={`${m.totalStations ?? 0} trạm đang quản lý`}
            />
            <StatCard icon={<CheckCircle size={24} />} label="Hoàn thành" color="#22c55e" bg="#f0fdf4"
              value={m.completedBookingsLast30Days ?? 0}
              sub="Số đơn sạc thành công"
            />
            <StatCard icon={<XCircle size={24} />} label="Tỷ lệ hủy" color="#ef4444" bg="#fef2f2"
              value={m.cancelRateLast30Days != null
                ? `${(m.cancelRateLast30Days * 100).toFixed(1)}%`
                : "—"}
              sub="Driver + Owner hủy"
            />
          </div>
          <div style={{ marginBottom: 24 }}>
            <span style={{ fontSize: 12, background: "#fef2f2", color: "#dc2626", border: "1px solid #fecaca", padding: "4px 10px", borderRadius: 20, fontWeight: 600 }}>
              NoShow: {m.noShowLast30Days ?? 0} đơn
            </span>
          </div>

          {/* Station mini bars */}
          <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 20, marginBottom: 24 }}>
            <div style={{ background: "#fff", borderRadius: 20, padding: 24, boxShadow: "0 2px 12px rgba(0,0,0,0.06)" }}>
              <h3 style={{ fontSize: 15, fontWeight: 800, color: "#1e293b", margin: "0 0 16px" }}> Doanh thu từng trạm</h3>
              {stationPerfs.length === 0
                ? <p style={{ fontSize: 13, color: "#94a3b8", fontStyle: "italic" }}>Chưa có dữ liệu.</p>
                : stationPerfs
                  .sort((a, b) => (b.totalRevenue ?? 0) - (a.totalRevenue ?? 0))
                  .map((s, i) => (
                    <div key={i} style={{ marginBottom: 12 }}>
                      <MiniBar
                        label={s.stationName?.length > 15 ? s.stationName.substring(0, 15) + "…" : s.stationName}
                        value={Number(s.totalRevenue) || 0}
                        max={maxRevenue}
                        color="#f97316"
                      />
                      <div style={{ fontSize: 11, color: "#94a3b8", textAlign: "right", marginTop: -4 }}>
                        {s.totalBookings} booking ·  {s.averageRating > 0 ? s.averageRating.toFixed(1) : "—"}
                      </div>
                    </div>
                  ))
              }
            </div>

            <div style={{ background: "#fff", borderRadius: 20, padding: 24, boxShadow: "0 2px 12px rgba(0,0,0,0.06)" }}>
              <h3 style={{ fontSize: 15, fontWeight: 800, color: "#1e293b", margin: "0 0 16px" }}> Lượt booking từng trạm</h3>
              {stationPerfs.length === 0
                ? <p style={{ fontSize: 13, color: "#94a3b8", fontStyle: "italic" }}>Chưa có dữ liệu.</p>
                : stationPerfs
                  .sort((a, b) => (b.totalBookings ?? 0) - (a.totalBookings ?? 0))
                  .map((s, i) => (
                    <MiniBar key={i}
                      label={s.stationName?.length > 15 ? s.stationName.substring(0, 15) + "…" : s.stationName}
                      value={s.totalBookings ?? 0}
                      max={maxBookings}
                      color="#3b82f6"
                    />
                  ))
              }
            </div>
          </div>

          {/* Top dịch vụ bổ sung */}
          {topServices.length > 0 && (
            <div style={{ background: "#fff", borderRadius: 20, padding: 24, boxShadow: "0 2px 12px rgba(0,0,0,0.06)", marginBottom: 24 }}>
              <h3 style={{ fontSize: 15, fontWeight: 800, color: "#1e293b", margin: "0 0 14px" }}> Top dịch vụ bổ sung</h3>
              <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fill, minmax(200px, 1fr))", gap: 10 }}>
                {topServices.map((svc, i) => (
                  <div key={i} style={{
                    background: i === 0 ? "#fff7ed" : "#f8fafc",
                    border: `1.5px solid ${i === 0 ? "#fed7aa" : "#e2e8f0"}`,
                    borderRadius: 14, padding: "12px 14px",
                  }}>
                    <div style={{ fontSize: 12, color: "#94a3b8", fontWeight: 600 }}>#{i + 1}</div>
                    <div style={{ fontSize: 15, fontWeight: 800, color: "#1e293b", margin: "4px 0 2px" }}>{svc.serviceName}</div>
                    <div style={{ fontSize: 12, color: "#f97316", fontWeight: 700 }}>
                      {(Number(svc.revenue) || 0).toLocaleString("vi-VN")}đ
                    </div>
                    <div style={{ fontSize: 11, color: "#64748b" }}>Đã bán: {svc.quantitySold} lượt</div>
                  </div>
                ))}
              </div>
            </div>
          )}

          {/* AI Insights block removed */}
        </>
      )}
    </div>
  );
}

//  Main Component 
const TABS = [
  { key: "overview", label: " Tổng quan", icon: "" },
  { key: "analytics", label: " Thống kê chi tiết", icon: "" },
];

export default function OwnerDashboard() {
  const [activeTab, setActiveTab] = useState("overview");

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

        {/* Tab Bar */}
        <div style={{
          display: "flex", gap: 4, marginBottom: 28,
          background: "#f1f5f9", borderRadius: 16, padding: 4,
          width: "fit-content",
        }}>
          {TABS.map((tab) => (
            <button
              key={tab.key}
              onClick={() => setActiveTab(tab.key)}
              style={{
                padding: "10px 24px", borderRadius: 12, fontSize: 14, fontWeight: 700,
                border: "none", cursor: "pointer", transition: "all 0.2s",
                background: activeTab === tab.key
                  ? "linear-gradient(135deg, #f97316, #ea580c)"
                  : "transparent",
                color: activeTab === tab.key ? "#fff" : "#64748b",
                boxShadow: activeTab === tab.key ? "0 2px 10px rgba(249,115,22,0.35)" : "none",
              }}
            >
              {tab.label}
            </button>
          ))}
        </div>

        {/* Tab Content */}
        {activeTab === "overview" && <TabOverview />}
        {activeTab === "analytics" && <TabAnalytics />}

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
