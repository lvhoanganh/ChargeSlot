import { useState, useEffect } from "react";
import { adminRevenueApi } from "../../services/api";

const fmt = (n) => (n || 0).toLocaleString("vi-VN") + "đ";
const toLocal = (dt) => new Date(dt).toLocaleString("vi-VN");

export default function AdminRevenue() {
  const [period, setPeriod] = useState("all");

  const [summary, setSummary] = useState(null);
  const [monthly, setMonthly] = useState([]);
  const [topStations, setTopStations] = useState([]);
  const [recentTx, setRecentTx] = useState([]);

  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  // Fetch all data when period changes
  useEffect(() => {
    let cancelled = false;
    async function fetchData() {
      setLoading(true);
      setError(null);
      try {
        const [summaryRes, monthlyRes, topRes, txRes] = await Promise.all([
          adminRevenueApi.getSummary(period),
          adminRevenueApi.getMonthly(period),
          adminRevenueApi.getTopStations(period, 5),
          adminRevenueApi.getRecentTransactions(10),
        ]);
        if (!cancelled) {
          setSummary(summaryRes);
          setMonthly(monthlyRes);
          setTopStations(topRes);
          setRecentTx(txRes);
        }
      } catch (err) {
        if (!cancelled) setError(err.message || "Không thể tải dữ liệu");
      } finally {
        if (!cancelled) setLoading(false);
      }
    }
    fetchData();
    return () => { cancelled = true; };
  }, [period]);

  // Loading state
  if (loading) {
    return (
      <div style={{ minHeight: "100vh", background: "#f1f5f9", paddingTop: 90, display: "flex", justifyContent: "center", alignItems: "center" }}>
        <div style={{ textAlign: "center" }}>
          <div style={{ width: 40, height: 40, border: "4px solid #e2e8f0", borderTopColor: "#3b82f6", borderRadius: "50%", animation: "spin 0.8s linear infinite", margin: "0 auto 16px" }} />
          <p style={{ color: "#64748b", fontSize: 15 }}>Đang tải báo cáo doanh thu...</p>
          <style>{`@keyframes spin { to { transform: rotate(360deg); } }`}</style>
        </div>
      </div>
    );
  }

  // Error state
  if (error) {
    return (
      <div style={{ minHeight: "100vh", background: "#f1f5f9", paddingTop: 90, display: "flex", justifyContent: "center", alignItems: "center" }}>
        <div style={{ textAlign: "center", background: "#fff", padding: 32, borderRadius: 16, boxShadow: "0 2px 8px rgba(0,0,0,0.06)" }}>
          <p style={{ fontSize: 40, marginBottom: 8 }}>⚠️</p>
          <p style={{ color: "#dc2626", fontSize: 15, fontWeight: 600, marginBottom: 8 }}>{error}</p>
          <button
            onClick={() => setPeriod((p) => p)}
            style={{ padding: "8px 20px", borderRadius: 8, border: "none", background: "#3b82f6", color: "#fff", fontWeight: 600, cursor: "pointer" }}
          >
            Thử lại
          </button>
        </div>
      </div>
    );
  }

  const data = summary || {};
  const maxRevenue = monthly.length ? Math.max(...monthly.map((m) => m.revenue)) : 1;

  // Dynamic breakdown percentages
  const total = (data.totalRevenue || 0);
  const ownerNet = total - (data.platformFee || 0) - (data.vatCollected || 0);
  const pctOwner = total > 0 ? Math.round((ownerNet / total) * 100) : 0;
  const pctFee = total > 0 ? Math.round(((data.platformFee || 0) / total) * 100) : 0;
  const pctVat = total > 0 ? Math.round(((data.vatCollected || 0) / total) * 100) : 0;
  const hasRevenue = total > 0;

  // Tính tổng tiền từ giao dịch gần đây
  const txTotal = recentTx.reduce((sum, tx) => sum + (tx.amount > 0 ? tx.amount : 0), 0);

  return (
    <div style={{ minHeight: "100vh", background: "#f1f5f9", paddingTop: 90 }}>
      <div style={{ maxWidth: 1100, margin: "0 auto", padding: "0 16px 40px" }}>

        {/* Header */}
        <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 24 }}>
          <div>
            <h1 style={{ fontSize: 26, fontWeight: 800, color: "#0f172a", margin: 0 }}>📊 Báo cáo doanh thu</h1>
            <p style={{ color: "#64748b", fontSize: 14, marginTop: 4 }}>Tổng quan tài chính hệ thống ChargeSlot</p>
          </div>
          <select
            value={period}
            onChange={(e) => setPeriod(e.target.value)}
            style={{ padding: "8px 16px", borderRadius: 10, border: "1.5px solid #e2e8f0", fontSize: 14, fontWeight: 600, cursor: "pointer", background: "#fff" }}
          >
            <option value="all">Tất cả</option>
            <option value="month">Tháng này</option>
            <option value="quarter">Quý này</option>
            <option value="year">Năm nay</option>
          </select>
        </div>

        {/* Summary Cards */}
        <div style={{ display: "grid", gridTemplateColumns: "repeat(4, 1fr)", gap: 14, marginBottom: 24 }}>
          <SummaryCard icon="💰" label="Tổng doanh thu" value={fmt(data.totalRevenue)} color="#16a34a" bg="linear-gradient(135deg, #f0fdf4 0%, #dcfce7 100%)" />
          <SummaryCard icon="🏦" label="Phí nền tảng (5%)" value={fmt(data.platformFee)} color="#8b5cf6" bg="linear-gradient(135deg, #f5f3ff 0%, #ede9fe 100%)" />
          <SummaryCard icon="📋" label="Tổng booking" value={data.totalBookings} sub={`${data.completedBookings} hoàn thành`} color="#3b82f6" bg="linear-gradient(135deg, #eff6ff 0%, #dbeafe 100%)" />
          <SummaryCard icon="⚠️" label="Tranh chấp" value={data.disputedBookings} sub={`VAT thu: ${fmt(data.vatCollected)}`} color="#f59e0b" bg="linear-gradient(135deg, #fffbeb 0%, #fef3c7 100%)" />
        </div>
        {/* Charts Row */}
        <div style={{ display: "grid", gridTemplateColumns: "2fr 1fr", gap: 16, marginBottom: 24 }}>
          {/* Bar chart */}
          <div style={{ background: "#fff", borderRadius: 16, padding: 24, boxShadow: "0 2px 8px rgba(0,0,0,0.04)" }}>
            <h3 style={{ fontSize: 16, fontWeight: 700, color: "#1e293b", marginBottom: 20 }}>📈 Doanh thu theo tháng</h3>
            {monthly.length === 0 ? (
              <div style={{ textAlign: "center", padding: "30px 20px" }}>
                <div style={{ fontSize: 36, marginBottom: 8, opacity: 0.4 }}>📉</div>
                <p style={{ color: "#94a3b8", fontSize: 14 }}>Chưa có dữ liệu doanh thu theo tháng</p>
              </div>
            ) : (
              <div style={{ display: "flex", alignItems: "flex-end", gap: 10, height: 200 }}>
                {monthly.map((m, idx) => (
                  <div key={m.month} style={{ flex: 1, display: "flex", flexDirection: "column", alignItems: "center" }}>
                    <span style={{ fontSize: 11, fontWeight: 600, color: "#64748b", marginBottom: 4 }}>
                      {(m.revenue / 1000000).toFixed(1)}M
                    </span>
                    <div
                      style={{
                        width: "100%", borderRadius: "8px 8px 0 0",
                        height: `${(m.revenue / maxRevenue) * 160}px`,
                        background: idx === monthly.length - 1 ? "linear-gradient(180deg, #f97316, #ea580c)" : "linear-gradient(180deg, #3b82f6, #2563eb)",
                        transition: "height 0.5s ease",
                      }}
                    />
                    <span style={{ fontSize: 11, color: "#94a3b8", marginTop: 6 }}>{m.month}</span>
                  </div>
                ))}
              </div>
            )}
          </div>

          {/* Pie-like breakdown */}
          <div style={{ background: "#fff", borderRadius: 16, padding: 24, boxShadow: "0 2px 8px rgba(0,0,0,0.04)" }}>
            <h3 style={{ fontSize: 16, fontWeight: 700, color: "#1e293b", marginBottom: 20 }}>💹 Phân bổ doanh thu</h3>
            {hasRevenue ? (
              <div style={{ display: "flex", flexDirection: "column", gap: 12 }}>
                <BreakdownRow label="Owner nhận" value={fmt(ownerNet)} pct={pctOwner} color="#16a34a" />
                <BreakdownRow label="Phí nền tảng" value={fmt(data.platformFee)} pct={pctFee} color="#8b5cf6" />
                <BreakdownRow label="Thuế VAT (8%)" value={fmt(data.vatCollected)} pct={pctVat} color="#f59e0b" />
              </div>
            ) : (
              <div style={{ textAlign: "center", padding: "20px 10px" }}>
                <div style={{ fontSize: 36, marginBottom: 8, opacity: 0.4 }}>💹</div>
                <p style={{ color: "#94a3b8", fontSize: 13 }}>Sẽ hiển thị khi có doanh thu</p>
              </div>
            )}
            <div style={{ marginTop: 20, padding: 12, borderRadius: 10, background: "#f8fafc" }}>
              <div style={{ display: "flex", justifyContent: "space-between", fontSize: 13, marginBottom: 6 }}>
                <span style={{ color: "#64748b" }}>Trạm hoạt động</span>
                <span style={{ fontWeight: 700, color: "#1e293b" }}>{data.totalStations}</span>
              </div>
              <div style={{ display: "flex", justifyContent: "space-between", fontSize: 13 }}>
                <span style={{ color: "#64748b" }}>Tổng tài xế</span>
                <span style={{ fontWeight: 700, color: "#1e293b" }}>{data.totalDrivers}</span>
              </div>
            </div>
          </div>
        </div>

        {/* Bottom Row */}
        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 16 }}>
          {/* Top Stations */}
          <div style={{ background: "#fff", borderRadius: 16, padding: 24, boxShadow: "0 2px 8px rgba(0,0,0,0.04)" }}>
            <h3 style={{ fontSize: 16, fontWeight: 700, color: "#1e293b", marginBottom: 16 }}>🏆 Top trạm doanh thu cao</h3>
            <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
              {topStations.length === 0 ? (
                <div style={{ textAlign: "center", padding: "20px 10px" }}>
                  <div style={{ fontSize: 36, marginBottom: 8, opacity: 0.4 }}>🏆</div>
                  <p style={{ color: "#94a3b8", fontSize: 13 }}>Chưa có trạm nào phát sinh doanh thu</p>
                </div>
              ) : (
                topStations.map((st, idx) => (
                  <div key={st.name} style={{ display: "flex", alignItems: "center", gap: 10, padding: "10px 12px", borderRadius: 10, background: idx === 0 ? "#fffbeb" : "#f8fafc" }}>
                    <span style={{ fontSize: 18, width: 28, textAlign: "center" }}>
                      {idx === 0 ? "🥇" : idx === 1 ? "🥈" : idx === 2 ? "🥉" : `#${idx + 1}`}
                    </span>
                    <div style={{ flex: 1 }}>
                      <p style={{ fontSize: 14, fontWeight: 600, color: "#1e293b", margin: 0 }}>{st.name}</p>
                      <p style={{ fontSize: 12, color: "#94a3b8", margin: 0 }}>{st.owner} — {st.bookings} bookings</p>
                    </div>
                    <span style={{ fontSize: 14, fontWeight: 700, color: "#16a34a" }}>{fmt(st.revenue)}</span>
                  </div>
                ))
              )}
            </div>
          </div>

          {/* Recent Transactions */}
          <div style={{ background: "#fff", borderRadius: 16, padding: 24, boxShadow: "0 2px 8px rgba(0,0,0,0.04)" }}>
            <h3 style={{ fontSize: 16, fontWeight: 700, color: "#1e293b", marginBottom: 16 }}>🔄 Giao dịch gần đây</h3>
            <div style={{ display: "flex", flexDirection: "column", gap: 6 }}>
              {recentTx.length === 0 ? (
                <p style={{ color: "#94a3b8", fontSize: 14, textAlign: "center", padding: 20 }}>Chưa có giao dịch</p>
              ) : (
                recentTx.map((tx) => (
                  <div key={tx.id} style={{ display: "flex", justifyContent: "space-between", alignItems: "center", padding: "8px 10px", borderRadius: 8, background: "#f8fafc" }}>
                    <div style={{ flex: 1 }}>
                      <p style={{ fontSize: 13, color: "#1e293b", margin: 0, fontWeight: 500 }}>{tx.memo}</p>
                      <p style={{ fontSize: 11, color: "#94a3b8", margin: 0 }}>{toLocal(tx.date)}</p>
                    </div>
                    <span style={{ fontSize: 13, fontWeight: 700, color: tx.amount >= 0 ? "#16a34a" : "#dc2626", whiteSpace: "nowrap", marginLeft: 8 }}>
                      {tx.amount >= 0 ? "+" : ""}{fmt(tx.amount)}
                    </span>
                  </div>
                ))
              )}
            </div>
          </div>
        </div>

      </div>
    </div>
  );
}

function SummaryCard({ icon, label, value, sub, color, bg }) {
  return (
    <div style={{ background: bg, borderRadius: 14, padding: "18px 16px", boxShadow: "0 2px 8px rgba(0,0,0,0.04)" }}>
      <div style={{ display: "flex", alignItems: "center", gap: 8, marginBottom: 8 }}>
        <span style={{ fontSize: 22 }}>{icon}</span>
        <span style={{ fontSize: 13, color: "#64748b", fontWeight: 500 }}>{label}</span>
      </div>
      <p style={{ fontSize: 22, fontWeight: 800, color, margin: 0 }}>{value}</p>
      {sub && <p style={{ fontSize: 12, color: "#94a3b8", marginTop: 2 }}>{sub}</p>}
    </div>
  );
}

function BreakdownRow({ label, value, pct, color }) {
  return (
    <div>
      <div style={{ display: "flex", justifyContent: "space-between", fontSize: 13, marginBottom: 4 }}>
        <span style={{ color: "#64748b" }}>{label}</span>
        <span style={{ fontWeight: 600, color: "#1e293b" }}>{value}</span>
      </div>
      <div style={{ height: 8, borderRadius: 4, background: "#f1f5f9", overflow: "hidden" }}>
        <div style={{ height: "100%", width: `${pct}%`, borderRadius: 4, background: color, transition: "width 0.6s ease" }} />
      </div>
    </div>
  );
}
