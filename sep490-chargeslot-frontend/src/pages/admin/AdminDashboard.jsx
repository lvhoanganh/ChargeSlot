import { useState, useEffect } from "react";
import {
  BarChart, Bar, XAxis, YAxis, CartesianGrid, Tooltip as RechartsTooltip,
  ResponsiveContainer, Cell, AreaChart, Area, PieChart, Pie
} from "recharts";
import { Banknote, Lock, CalendarDays, XCircle, Ghost, Scale, AlertTriangle, Landmark, AlertOctagon, Users } from "lucide-react";
import { instance } from "@/lib/httpRequest";

const fmt = (n) => (typeof n === "number" ? n.toLocaleString("vi-VN") + "đ" : "—");
const fmtNum = (n) => (typeof n === "number" ? n.toLocaleString("vi-VN") : "0");
const fmtPct = (n) => (typeof n === "number" ? (n * 100).toFixed(1) + "%" : "—");

// Colors
const COLORS = ['#0088FE', '#00C49F', '#FFBB28', '#FF8042'];

export default function AdminDashboard() {
  const [metrics, setMetrics] = useState(null);
  const [revenueMonthly, setRevenueMonthly] = useState([]);
  const [topStations, setTopStations] = useState([]);
  const [accountStats, setAccountStats] = useState(null);

  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);

    Promise.all([
      instance.get("/admin/analytics/metrics").catch(e => null),
      instance.get("/admin/revenue/monthly?period=year").catch(e => null),
      instance.get("/admin/revenue/top-stations?limit=5").catch(e => null),
      instance.get("/AdminAccounts/statistics").catch(e => null)
    ])
      .then(([resMetrics, resRev, resTop, resAcc]) => {
        if (cancelled) return;
        if (resMetrics?.data) setMetrics(resMetrics.data);
        if (resRev?.data) {
          const sorted = [...(resRev.data)].sort((a, b) => {
            const ka = a.month || a.yearMonth || "";
            const kb = b.month || b.yearMonth || "";
            return ka.localeCompare(kb);
          });
          setRevenueMonthly(sorted);
        }
        if (resTop?.data) setTopStations(Array.isArray(resTop.data) ? resTop.data : (resTop.data?.items ?? []));
        if (resAcc?.data) setAccountStats(resAcc.data);
      })
      .catch((err) => {
        if (!cancelled) setError("Lỗi tải dữ liệu Dashboard");
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    return () => { cancelled = true; };
  }, []);

  const cancelRate = metrics?.cancelRateLast30Days ?? 0;
  const isHighCancel = cancelRate > 0.3;

  const riskDrivers = (metrics?.highRiskDrivers || []).map((d) => ({
    name: d.driverName || `ID ${d.driverUserId}`,
    value: d.cancelledBookings || 0,
    totalBookings: d.totalBookings || 0,
  }));

  const pieData = accountStats ? [
    { name: 'Tài xế', value: accountStats.totalDrivers || 0 },
    { name: 'Chủ trạm', value: accountStats.totalOwners || 0 },
    { name: 'Admin', value: accountStats.totalAdmins || 0 }
  ] : [];

  return (
    <div style={{ minHeight: "100vh", background: "#f1f5f9", paddingTop: 88, paddingBottom: 48 }}>
      <div style={{ maxWidth: 1200, margin: "0 auto", padding: "0 20px" }}>

        {/* Page Header */}
        <div style={{ marginBottom: 28 }}>
          <p style={{ fontSize: 12, fontWeight: 700, textTransform: "uppercase", letterSpacing: "0.15em", color: "#f97316", margin: 0 }}>
            Bảng điều khiển quản trị
          </p>
          <h1 style={{ fontSize: 28, fontWeight: 800, color: "#0f172a", margin: "6px 0 4px", letterSpacing: "-0.5px" }}>
            Tổng quan hệ thống
          </h1>
          <p style={{ fontSize: 14, color: "#64748b", margin: 0 }}>
            Phân tích hiệu suất và cảnh báo rủi ro nền tảng ChargeSlot
          </p>
        </div>

        {loading ? (
          <div style={{ display: "grid", gridTemplateColumns: "repeat(3, 1fr)", gap: 16, marginBottom: 24 }}>
            {[1, 2, 3, 4, 5, 6].map((i) => (
              <div key={i} style={{ background: "white", borderRadius: 16, padding: 24, height: 100, animation: "dash-pulse 1.5s ease-in-out infinite" }} />
            ))}
          </div>
        ) : error ? (
          <div style={{ background: "#fef2f2", border: "1px solid #fecaca", borderRadius: 14, padding: 20, marginBottom: 24, color: "#dc2626", fontSize: 14 }}>
            <AlertTriangle size={18} className="inline mr-2" /> {error}
          </div>
        ) : (
          <>
            {/* Row 1: 6 Stat Cards */}
            <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(280px, 1fr))", gap: 16, marginBottom: 24 }}>
              <MetricCard
                icon={<Banknote size={24} color="#16a34a" />}
                label="Doanh thu Sàn"
                value={fmt(metrics?.totalPlatformRevenue)}
                color="#16a34a"
                bg="linear-gradient(135deg, #f0fdf4, #dcfce7)"
              />
              <MetricCard
                icon={<Lock size={24} color="#7c3aed" />}
                label="Tiền giữ hộ (Escrow)"
                value={fmt(metrics?.totalEscrowBalance)}
                color="#7c3aed"
                bg="linear-gradient(135deg, #f5f3ff, #ede9fe)"
              />
              <MetricCard
                icon={<CalendarDays size={24} color="#2563eb" />}
                label="Tổng đơn (30 ngày)"
                value={fmtNum(metrics?.bookingsLast30Days)}
                color="#2563eb"
                bg="linear-gradient(135deg, #eff6ff, #dbeafe)"
              />
              <MetricCard
                icon={<XCircle size={24} color={isHighCancel ? "#dc2626" : "#16a34a"} />}
                label="Tỷ lệ hủy (30 ngày)"
                value={fmtPct(cancelRate)}
                color={isHighCancel ? "#dc2626" : "#16a34a"}
                bg={isHighCancel ? "linear-gradient(135deg, #fef2f2, #fee2e2)" : "linear-gradient(135deg, #f0fdf4, #dcfce7)"}
                highlight={isHighCancel}
              />
              <MetricCard
                icon={<Ghost size={24} color="#dc2626" />}
                label="NoShow (30 ngày)"
                value={fmtNum(metrics?.noShowLast30Days)}
                color="#dc2626"
                bg="linear-gradient(135deg, #fef2f2, #fee2e2)"
              />
              <MetricCard
                icon={<Scale size={24} color="#9333ea" />}
                label="Khiếu nại (30 ngày)"
                value={fmtNum(metrics?.disputesLast30Days)}
                color="#9333ea"
                bg="linear-gradient(135deg, #faf5ff, #f3e8ff)"
              />
            </div>

            {/* Row 2: Monthly Rev / Top Stations */}
            <div style={{
              display: "grid", gridTemplateColumns: "1fr 1fr", gap: 20, marginBottom: 24, alignItems: "start",
              "@media (max-width: 768px)": { gridTemplateColumns: "1fr" }
            }} className="dashboard-grid">
              <ChartWrapper title="Doanh thu theo tháng" subtitle="Toàn nền tảng trong năm nay">
                <ResponsiveContainer width="100%" height={260}>
                  <AreaChart data={revenueMonthly} margin={{ top: 10, right: 10, left: 0, bottom: 0 }}>
                    <defs>
                      <linearGradient id="colorRev" x1="0" y1="0" x2="0" y2="1">
                        <stop offset="5%" stopColor="#16a34a" stopOpacity={0.8} />
                        <stop offset="95%" stopColor="#16a34a" stopOpacity={0} />
                      </linearGradient>
                    </defs>
                    <CartesianGrid strokeDasharray="3 3" vertical={false} stroke="#e2e8f0" />
                    <XAxis dataKey="month" tick={{ fontSize: 12, fill: "#64748b" }} />
                    <YAxis tick={{ fontSize: 12, fill: "#64748b" }} width={60} tickFormatter={(val) => {
                      if (val >= 1000000) return (val / 1000000) + "tr";
                      if (val >= 1000) return (val / 1000) + "k";
                      return val;
                    }} />
                    <RechartsTooltip formatter={(val) => [val.toLocaleString('vi-VN') + "đ", "Doanh thu"]} />
                    <Area type="monotone" dataKey="revenue" stroke="#16a34a" fillOpacity={1} fill="url(#colorRev)" />
                  </AreaChart>
                </ResponsiveContainer>
              </ChartWrapper>

              <ChartWrapper title="Top 5 Trạm (Doanh thu)" subtitle="Các trạm đóng góp nhiều doanh thu nhất">
                {topStations.length > 0 ? (
                  <ResponsiveContainer width="100%" height={260}>
                    <BarChart data={topStations} layout="vertical" margin={{ top: 5, right: 20, left: 10, bottom: 5 }}>
                      <CartesianGrid strokeDasharray="3 3" horizontal={false} stroke="#e2e8f0" />
                      <XAxis type="number" tickFormatter={(val) => {
                        if (val >= 1000000) return (val / 1000000) + "tr";
                        if (val >= 1000) return (val / 1000) + "k";
                        return val;
                      }} tick={{ fontSize: 11 }} />
                      <YAxis dataKey="stationName" type="category" width={80} tick={{ fontSize: 11, fill: "#475569" }} />
                      <RechartsTooltip formatter={(val) => [val.toLocaleString('vi-VN') + "đ", "Doanh thu"]} />
                      <Bar dataKey={topStations[0]?.revenue !== undefined ? "revenue" : topStations[0]?.totalRevenue !== undefined ? "totalRevenue" : "revenue"} fill="#3b82f6" radius={[0, 4, 4, 0]} barSize={24} />
                    </BarChart>
                  </ResponsiveContainer>
                ) : (
                  <div style={{ height: 260, display: "flex", alignItems: "center", justifyContent: "center", color: "#94a3b8" }}>Chưa có dữ liệu</div>
                )}
              </ChartWrapper>
            </div>

            {/* Row 3: Pending Withdraws / High Risk Drivers */}
            <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 20, marginBottom: 24, alignItems: "start" }} className="dashboard-grid">
              <div style={{ background: "white", borderRadius: 20, padding: "24px 28px", boxShadow: "0 2px 12px rgba(0,0,0,0.05)", border: "1px solid rgba(0,0,0,0.06)", height: "100%" }}>
                <h3 style={{ fontSize: 17, fontWeight: 700, color: "#0f172a", margin: "0 0 16px", display: "flex", alignItems: "center", gap: 8 }}><Landmark size={20} color="#16a34a" /> Rút tiền đang chờ</h3>
                <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
                  <div style={{ padding: 16, background: "#fffbeb", borderRadius: 12, border: "1px solid #fef3c7" }}>
                    <p style={{ margin: 0, fontSize: 14, color: "#b45309" }}>Số lượng yêu cầu</p>
                    <p style={{ margin: 0, fontSize: 24, fontWeight: 700, color: "#92400e" }}>{fmtNum(metrics?.pendingWithdrawCount)}</p>
                  </div>
                  <div style={{ padding: 16, background: "#fef2f2", borderRadius: 12, border: "1px solid #fee2e2" }}>
                    <p style={{ margin: 0, fontSize: 14, color: "#b91c1c" }}>Tổng tiền chờ duyệt</p>
                    <p style={{ margin: 0, fontSize: 24, fontWeight: 700, color: "#991b1b" }}>{fmt(metrics?.pendingWithdrawAmount)}</p>
                  </div>
                  <div style={{ padding: 16, background: "#f0fdf4", borderRadius: 12, border: "1px solid #dcfce7" }}>
                    <p style={{ margin: 0, fontSize: 14, color: "#15803d" }}>Đã giải ngân</p>
                    <p style={{ margin: 0, fontSize: 24, fontWeight: 700, color: "#166534" }}>{fmt(metrics?.completedWithdrawAmount)}</p>
                  </div>
                </div>
              </div>

              <div style={{ background: "white", borderRadius: 20, padding: "24px 28px", boxShadow: "0 2px 12px rgba(0,0,0,0.05)", border: "1px solid rgba(0,0,0,0.06)", height: "100%" }}>
                <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", marginBottom: 20 }}>
                  <div>
                    <h3 style={{ fontSize: 17, fontWeight: 700, color: "#0f172a", margin: 0, display: "flex", alignItems: "center", gap: 8 }}>
                      <AlertOctagon size={20} color="#dc2626" /> Tài xế rủi ro cao
                    </h3>
                    <p style={{ fontSize: 13, color: "#64748b", margin: "4px 0 0" }}>
                      Tài xế hủy đơn nhiều {'>'} 50%
                    </p>
                  </div>
                  <span style={{ fontSize: 12, background: "#fef2f2", color: "#dc2626", border: "1px solid #fecaca", padding: "4px 10px", borderRadius: 20, fontWeight: 600 }}>
                    {riskDrivers.length} tài xế
                  </span>
                </div>
                {riskDrivers.length === 0 ? (
                  <div style={{ textAlign: "center", padding: "40px 20px", display: "flex", alignItems: "center", justifyContent: "center", height: 200 }}>
                    <p style={{ color: "#94a3b8", fontSize: 14, margin: 0 }}>Không có tài xế rủi ro cao</p>
                  </div>
                ) : (
                  <ResponsiveContainer width="100%" height={240}>
                    <BarChart data={riskDrivers} margin={{ top: 5, right: 10, bottom: 20, left: 0 }}>
                      <CartesianGrid strokeDasharray="3 3" vertical={false} stroke="#e2e8f0" />
                      <XAxis dataKey="name" tick={{ fontSize: 11, fill: "#64748b" }} angle={-20} textAnchor="end" interval={0} />
                      <YAxis tick={{ fontSize: 12, fill: "#64748b" }} width={30} />
                      <RechartsTooltip formatter={(val, name, props) => [val + ` / ${props.payload.totalBookings} đơn`, "Số lần hủy"]} />
                      <Bar dataKey="value" radius={[4, 4, 0, 0]} maxBarSize={40}>
                        {riskDrivers.map((_, idx) => (
                          <Cell key={idx} fill={idx === 0 ? "#dc2626" : idx <= 2 ? "#f97316" : "#f59e0b"} />
                        ))}
                      </Bar>
                    </BarChart>
                  </ResponsiveContainer>
                )}
              </div>
            </div>

            {/* Row 4: User Allocation / Summary Stats */}
            <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 20 }} className="dashboard-grid">
              <div style={{ background: "white", borderRadius: 20, padding: "24px 28px", boxShadow: "0 2px 12px rgba(0,0,0,0.05)", border: "1px solid rgba(0,0,0,0.06)" }}>
                <h3 style={{ fontSize: 17, fontWeight: 700, color: "#0f172a", margin: "0 0 16px", display: "flex", alignItems: "center", gap: 8 }}><Users size={20} color="#3b82f6" /> Phân bổ người dùng</h3>
                <ResponsiveContainer width="100%" height={220}>
                  <PieChart>
                    <Pie data={pieData} cx="50%" cy="50%" innerRadius={60} outerRadius={80} paddingAngle={5} dataKey="value">
                      {pieData.map((entry, index) => <Cell key={`cell-${index}`} fill={COLORS[index % COLORS.length]} />)}
                    </Pie>
                    <RechartsTooltip formatter={(value) => [value, "Số lượng"]} />
                  </PieChart>
                </ResponsiveContainer>
                <div style={{ display: "flex", justifyContent: "center", gap: 16 }}>
                  {pieData.map((entry, index) => (
                    <div key={entry.name} style={{ display: "flex", alignItems: "center", gap: 6 }}>
                      <div style={{ width: 12, height: 12, borderRadius: "50%", background: COLORS[index % COLORS.length] }}></div>
                      <span style={{ fontSize: 13, color: "#475569" }}>{entry.name} ({entry.value})</span>
                    </div>
                  ))}
                </div>
              </div>

              <div style={{ background: "white", borderRadius: 20, padding: "24px 28px", boxShadow: "0 2px 12px rgba(0,0,0,0.05)", border: "1px solid rgba(0,0,0,0.06)" }}>
                <h3 style={{ fontSize: 17, fontWeight: 700, color: "#0f172a", margin: "0 0 16px" }}>📊 Trạng thái tài khoản</h3>
                <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
                  <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", padding: "16px", borderBottom: "1px solid #f1f5f9" }}>
                    <span style={{ fontSize: 15, color: "#475569" }}>Tổng số tài khoản</span>
                    <span style={{ fontSize: 18, fontWeight: 700, color: "#0f172a" }}>{fmtNum(accountStats?.totalAccounts)}</span>
                  </div>
                  <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", padding: "16px", borderBottom: "1px solid #f1f5f9" }}>
                    <span style={{ fontSize: 15, color: "#16a34a" }}>Đang hoạt động (Active)</span>
                    <span style={{ fontSize: 18, fontWeight: 700, color: "#15803d" }}>{fmtNum(accountStats?.activeAccounts)}</span>
                  </div>
                  <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", padding: "16px", borderBottom: "1px solid #f1f5f9" }}>
                    <span style={{ fontSize: 15, color: "#dc2626" }}>Đã bị khóa (Banned)</span>
                    <span style={{ fontSize: 18, fontWeight: 700, color: "#b91c1c" }}>{fmtNum(accountStats?.bannedAccounts)}</span>
                  </div>
                </div>
              </div>
            </div>

          </>
        )}
      </div>

      <style>{`
        @keyframes dash-pulse {
          0%, 100% { opacity: 0.5; }
          50% { opacity: 1; }
        }
        @media (max-width: 768px) {
          .dashboard-grid {
            grid-template-columns: 1fr !important;
          }
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

function ChartWrapper({ title, subtitle, children }) {
  return (
    <div style={{ background: "white", borderRadius: 20, padding: "24px 28px", boxShadow: "0 2px 12px rgba(0,0,0,0.05)", border: "1px solid rgba(0,0,0,0.06)", height: "100%" }}>
      <div style={{ marginBottom: 20 }}>
        <h3 style={{ fontSize: 17, fontWeight: 700, color: "#0f172a", margin: 0 }}>{title}</h3>
        {subtitle && <p style={{ fontSize: 13, color: "#64748b", margin: "4px 0 0" }}>{subtitle}</p>}
      </div>
      {children}
    </div>
  );
}
