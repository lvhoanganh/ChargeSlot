import { useState, useEffect } from "react";
import { adminRevenueApi } from "../../services/api";
import { formatVN } from "../../utils/dateVN";

const fmt = (n) => (n || 0).toLocaleString("vi-VN") + "đ";
const toLocal = (dt) => formatVN(dt);

export default function AdminRevenue() {
  const [period, setPeriod] = useState("all");

  const [summary, setSummary] = useState(null);
  const [monthly, setMonthly] = useState([]);
  const [topStations, setTopStations] = useState([]);
  const [recentTx, setRecentTx] = useState([]);
  const [selectedTxId, setSelectedTxId] = useState(null);
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
        <div className="rev-grid-4">
          <SummaryCard icon="💰" label="Tổng doanh thu" value={fmt(data.totalRevenue)} color="#16a34a" bg="linear-gradient(135deg, #f0fdf4 0%, #dcfce7 100%)" />
          <SummaryCard icon="🏦" label="Phí nền tảng (5%)" value={fmt(data.platformFee)} color="#8b5cf6" bg="linear-gradient(135deg, #f5f3ff 0%, #ede9fe 100%)" />
          <SummaryCard icon="📋" label="Tổng booking" value={data.totalBookings} sub={`${data.completedBookings} hoàn thành`} color="#3b82f6" bg="linear-gradient(135deg, #eff6ff 0%, #dbeafe 100%)" />
          <SummaryCard icon="⚠️" label="Tranh chấp" value={data.disputedBookings} sub={`VAT thu: ${fmt(data.vatCollected)}`} color="#f59e0b" bg="linear-gradient(135deg, #fffbeb 0%, #fef3c7 100%)" />
        </div>
        {/* Charts Row */}
        <div className="rev-grid-2-1">
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
        <div className="rev-grid-2">
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
                  <div 
                    key={tx.id} 
                    onClick={() => setSelectedTxId(tx.id)}
                    style={{ display: "flex", justifyContent: "space-between", alignItems: "center", padding: "8px 10px", borderRadius: 8, background: "#f8fafc", cursor: "pointer", transition: "all 0.2s" }}
                    onMouseEnter={e => { e.currentTarget.style.background = "#eff6ff"; }}
                    onMouseLeave={e => { e.currentTarget.style.background = "#f8fafc"; }}
                  >
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
      {selectedTxId && (
        <TransactionDetailModal txId={selectedTxId} onClose={() => setSelectedTxId(null)} />
      )}
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

function TransactionDetailModal({ txId, onClose }) {
  const [detail, setDetail] = useState(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    adminRevenueApi.getTransactionDetail(txId)
      .then(setDetail)
      .catch(() => setDetail(null))
      .finally(() => setLoading(false));
  }, [txId]);

  return (
    <div style={{
      position: "fixed", inset: 0, zIndex: 9999, background: "rgba(15,23,42,0.6)",
      display: "flex", alignItems: "center", justifyContent: "center", padding: 20
    }}>
      <div style={{ background: "#fff", borderRadius: 20, width: "100%", maxWidth: 600, maxHeight: "85vh", overflowY: "auto", boxShadow: "0 20px 25px -5px rgba(0,0,0,0.1)" }}>
        <div style={{ padding: "24px 24px 16px", borderBottom: "1px solid #e2e8f0", position: "sticky", top: 0, background: "#fff", zIndex: 10 }}>
          <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
            <h2 style={{ fontSize: 18, fontWeight: 800, color: "#0f172a", margin: 0 }}>📋 Chi tiết sổ cái (Ledger)</h2>
            <button onClick={onClose} style={{ background: "none", border: "none", fontSize: 24, color: "#64748b", cursor: "pointer", padding: 0 }}>×</button>
          </div>
        </div>

        <div style={{ padding: 24 }}>
          {loading ? (
            <div style={{ textAlign: "center", padding: "40px 0", color: "#64748b" }}>⏳ Đang tải chi tiết dòng tiền...</div>
          ) : !detail ? (
            <div style={{ textAlign: "center", padding: "40px 0", color: "#ef4444", fontWeight: 600 }}>❌ Không tìm thấy thông tin giao dịch này</div>
          ) : (
            <div>
              <div style={{ background: "#f8fafc", borderRadius: 12, padding: 16, marginBottom: 20 }}>
                <p style={{ margin: "0 0 8px", fontSize: 13, color: "#64748b" }}>Giao dịch gốc: <strong style={{ color: "#3b82f6" }}>{detail.referenceType} #{detail.referenceId}</strong></p>
                <h3 style={{ margin: "0 0 12px", fontSize: 16, color: "#0f172a", fontWeight: 700 }}>{detail.memo}</h3>
                <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", borderTop: "1px dashed #cbd5e1", paddingTop: 12 }}>
                  <span style={{ fontSize: 12, color: "#94a3b8" }}>Ngày tạo: {toLocal(detail.createdAt)}</span>
                  <span style={{ fontSize: 12, fontWeight: 700, color: "#64748b", background: "#e2e8f0", padding: "4px 8px", borderRadius: 6 }}>
                    Mã GD: #{detail.id}
                  </span>
                </div>
              </div>

              <h4 style={{ fontSize: 14, fontWeight: 700, color: "#334155", marginBottom: 12 }}>Bút toán chi tiết (Entries):</h4>
              <div style={{ display: "flex", flexDirection: "column", gap: 10 }}>
                {detail.entries && detail.entries.length > 0 ? (
                  detail.entries.map((entry, i) => (
                    <div key={i} style={{ display: "flex", justifyContent: "space-between", alignItems: "center", border: "1px solid #e2e8f0", borderRadius: 12, padding: 16 }}>
                      <div>
                        <div style={{ display: "flex", alignItems: "center", gap: 8, marginBottom: 4 }}>
                          <span style={{ fontSize: 12, fontWeight: 700, background: entry.walletType === "System" ? "#fef3c7" : "#e0e7ff", color: entry.walletType === "System" ? "#d97706" : "#4338ca", padding: "2px 8px", borderRadius: 4 }}>
                            {entry.walletType === "System" ? "Hệ thống" : "Người dùng"}
                          </span>
                          <span style={{ fontSize: 13, color: "#64748b" }}>Ví #{entry.walletId}</span>
                        </div>
                        <div style={{ fontSize: 14, fontWeight: 700, color: "#0f172a" }}>{entry.ownerName}</div>
                      </div>
                      <div style={{ textAlign: "right" }}>
                        <div style={{ fontSize: 16, fontWeight: 800, color: entry.direction === "Credit" ? "#16a34a" : "#dc2626" }}>
                          {entry.direction === "Credit" ? "+" : "-"}{fmt(entry.amount)}
                        </div>
                        <div style={{ fontSize: 11, color: "#94a3b8", marginTop: 4 }}>
                          {entry.direction === "Credit" ? "Ghi Có" : "Ghi Nợ"}
                        </div>
                      </div>
                    </div>
                  ))
                ) : (
                  <p style={{ fontSize: 13, color: "#94a3b8", textAlign: "center", padding: 20 }}>Không có bút toán nào</p>
                )}
              </div>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
