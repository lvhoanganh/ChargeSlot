import { useState } from "react";

// ─── MOCK DATA (thay bằng API thực khi có BE) ───
const MOCK_SUMMARY = {
  totalRevenue: 48560000,
  platformFee: 2428000,
  vatCollected: 3884800,
  totalBookings: 312,
  completedBookings: 278,
  disputedBookings: 8,
  totalStations: 24,
  totalDrivers: 156,
};

const MOCK_MONTHLY = [
  { month: "10/2025", revenue: 3200000, bookings: 18, platformFee: 160000 },
  { month: "11/2025", revenue: 4800000, bookings: 28, platformFee: 240000 },
  { month: "12/2025", revenue: 5600000, bookings: 35, platformFee: 280000 },
  { month: "01/2026", revenue: 7200000, bookings: 42, platformFee: 360000 },
  { month: "02/2026", revenue: 12360000, bookings: 89, platformFee: 618000 },
  { month: "03/2026", revenue: 15400000, bookings: 100, platformFee: 770000 },
];

const MOCK_TOP_STATIONS = [
  { name: "EV Station Quận 1", owner: "Nguyễn Văn A", revenue: 8200000, bookings: 56 },
  { name: "ChargePoint Thủ Đức", owner: "Trần Thị B", revenue: 6800000, bookings: 42 },
  { name: "Test 6", owner: "Owner Demo", revenue: 5400000, bookings: 38 },
  { name: "GreenCharge Quận 7", owner: "Lê Văn C", revenue: 4200000, bookings: 31 },
  { name: "PowerUp Bình Thạnh", owner: "Phạm Thị D", revenue: 3900000, bookings: 28 },
];

const MOCK_RECENT_TRANSACTIONS = [
  { id: 1, type: "Settlement", memo: "Booking #312 → Owner nhận 228,000đ", amount: 228000, date: "2026-03-23T09:30:00Z" },
  { id: 2, type: "PlatformFee", memo: "Phí nền tảng booking #312 — 12,000đ", amount: 12000, date: "2026-03-23T09:30:00Z" },
  { id: 3, type: "Settlement", memo: "Booking #311 → Owner nhận 171,000đ", amount: 171000, date: "2026-03-23T08:15:00Z" },
  { id: 4, type: "PlatformFee", memo: "Phí nền tảng booking #311 — 9,000đ", amount: 9000, date: "2026-03-23T08:15:00Z" },
  { id: 5, type: "Refund", memo: "Hoàn tiền dispute #5 → Driver 120,000đ", amount: -120000, date: "2026-03-22T16:45:00Z" },
  { id: 6, type: "Settlement", memo: "Booking #310 → Owner nhận 342,000đ", amount: 342000, date: "2026-03-22T14:20:00Z" },
];

const fmt = (n) => (n || 0).toLocaleString("vi-VN") + "đ";
const toLocal = (dt) => new Date(dt).toLocaleString("vi-VN");

export default function AdminRevenue() {
  const [period, setPeriod] = useState("all");
  const data = MOCK_SUMMARY;
  const maxRevenue = Math.max(...MOCK_MONTHLY.map((m) => m.revenue));

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
            <div style={{ display: "flex", alignItems: "flex-end", gap: 10, height: 200 }}>
              {MOCK_MONTHLY.map((m) => (
                <div key={m.month} style={{ flex: 1, display: "flex", flexDirection: "column", alignItems: "center" }}>
                  <span style={{ fontSize: 11, fontWeight: 600, color: "#64748b", marginBottom: 4 }}>
                    {(m.revenue / 1000000).toFixed(1)}M
                  </span>
                  <div
                    style={{
                      width: "100%", borderRadius: "8px 8px 0 0",
                      height: `${(m.revenue / maxRevenue) * 160}px`,
                      background: m.month === "03/2026" ? "linear-gradient(180deg, #f97316, #ea580c)" : "linear-gradient(180deg, #3b82f6, #2563eb)",
                      transition: "height 0.5s ease",
                    }}
                  />
                  <span style={{ fontSize: 11, color: "#94a3b8", marginTop: 6 }}>{m.month}</span>
                </div>
              ))}
            </div>
          </div>

          {/* Pie-like breakdown */}
          <div style={{ background: "#fff", borderRadius: 16, padding: 24, boxShadow: "0 2px 8px rgba(0,0,0,0.04)" }}>
            <h3 style={{ fontSize: 16, fontWeight: 700, color: "#1e293b", marginBottom: 20 }}>💹 Phân bổ doanh thu</h3>
            <div style={{ display: "flex", flexDirection: "column", gap: 12 }}>
              <BreakdownRow label="Owner nhận" value={fmt(data.totalRevenue - data.platformFee - data.vatCollected)} pct={87} color="#16a34a" />
              <BreakdownRow label="Phí nền tảng" value={fmt(data.platformFee)} pct={5} color="#8b5cf6" />
              <BreakdownRow label="Thuế VAT (8%)" value={fmt(data.vatCollected)} pct={8} color="#f59e0b" />
            </div>
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
              {MOCK_TOP_STATIONS.map((st, idx) => (
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
              ))}
            </div>
          </div>

          {/* Recent Transactions */}
          <div style={{ background: "#fff", borderRadius: 16, padding: 24, boxShadow: "0 2px 8px rgba(0,0,0,0.04)" }}>
            <h3 style={{ fontSize: 16, fontWeight: 700, color: "#1e293b", marginBottom: 16 }}>🔄 Giao dịch gần đây</h3>
            <div style={{ display: "flex", flexDirection: "column", gap: 6 }}>
              {MOCK_RECENT_TRANSACTIONS.map((tx) => (
                <div key={tx.id} style={{ display: "flex", justifyContent: "space-between", alignItems: "center", padding: "8px 10px", borderRadius: 8, background: "#f8fafc" }}>
                  <div style={{ flex: 1 }}>
                    <p style={{ fontSize: 13, color: "#1e293b", margin: 0, fontWeight: 500 }}>{tx.memo}</p>
                    <p style={{ fontSize: 11, color: "#94a3b8", margin: 0 }}>{toLocal(tx.date)}</p>
                  </div>
                  <span style={{ fontSize: 13, fontWeight: 700, color: tx.amount >= 0 ? "#16a34a" : "#dc2626", whiteSpace: "nowrap", marginLeft: 8 }}>
                    {tx.amount >= 0 ? "+" : ""}{fmt(tx.amount)}
                  </span>
                </div>
              ))}
            </div>
          </div>
        </div>

        {/* Note */}
        <div style={{ marginTop: 20, padding: 14, borderRadius: 12, background: "#fffbeb", border: "1px solid #fde68a", textAlign: "center" }}>
          <p style={{ fontSize: 13, color: "#92400e", margin: 0 }}>
            ⚠️ Đang hiển thị dữ liệu mẫu. Khi có API backend, dữ liệu sẽ được cập nhật tự động.
          </p>
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
