import { useState, useEffect } from "react";
import { ownerAnalyticsApi } from "@/services/api";
import { Banknote, Zap, BarChart3, XCircle } from "lucide-react";

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

export default function OwnerAnalytics() {
  const [periodIdx, setPeriodIdx] = useState(1);
  const [metrics, setMetrics] = useState(null);
  const [aiInsight, setAiInsight] = useState(null);
  const [loading, setLoading] = useState(true);
  const [aiLoading, setAiLoading] = useState(false);
  const [error, setError] = useState("");

  useEffect(() => {
    const { fromDate, toDate } = getDateRange(PERIODS[periodIdx].days);
    setLoading(true);
    setError("");
    setMetrics(null);
    setAiInsight(null);

    ownerAnalyticsApi.getMetrics(fromDate, toDate)
      .then(data => setMetrics(data))
      .catch(err => setError(err.message || "Không tải được dữ liệu"))
      .finally(() => setLoading(false));
  }, [periodIdx]);

  async function handleLoadAI() {
    const { fromDate, toDate } = getDateRange(PERIODS[periodIdx].days);
    setAiLoading(true);
    setAiInsight(null);
    try {
      const data = await ownerAnalyticsApi.getAiInsights(fromDate, toDate);
      setAiInsight(data);
    } catch (err) {
      setAiInsight({ error: err.message || "Không lấy được gợi ý AI" });
    } finally {
      setAiLoading(false);
    }
  }

  const m = metrics;

  // BE trả về: stationPerformances = [{stationId, stationName, totalBookings, totalRevenue, averageRating}]
  const stationPerfs = m?.stationPerformances || [];
  const maxRevenue = Math.max(1, ...stationPerfs.map(s => Number(s.totalRevenue) || 0));
  const maxBookings = Math.max(1, ...stationPerfs.map(s => s.totalBookings || 0));

  // Top services
  const topServices = m?.topServicesSold || [];
  const maxSvcRev = Math.max(1, ...topServices.map(s => Number(s.revenue) || 0));

  return (
    <div style={{ minHeight: "100vh", background: "#f0f4f8", paddingTop: 80, paddingBottom: 48 }}>
      <div style={{ maxWidth: 1080, margin: "0 auto", padding: "0 24px" }}>

        {/* Header */}
        <div style={{ marginBottom: 24 }}>
          <p style={{ fontSize: 12, fontWeight: 700, color: "#f97316", letterSpacing: "0.15em", textTransform: "uppercase", margin: 0 }}>
            Owner Dashboard
          </p>
          <h1 style={{ fontSize: 28, fontWeight: 800, color: "#1e293b", margin: "6px 0 4px" }}> Thống kê kinh doanh</h1>
          <p style={{ fontSize: 14, color: "#64748b", margin: 0 }}>Tổng quan doanh thu &amp; hoạt động trạm sạc của bạn</p>
        </div>

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
          <div style={{ textAlign: "center", paddingTop: 80, color: "#94a3b8" }}>
            <div style={{ fontSize: 48, marginBottom: 8 }}></div>
            <p>Đang tải dữ liệu...</p>
          </div>
        )}

        {!loading && m && (
          <>
            {/* Stat Cards — dùng đúng field từ OwnerDashboardMetricsDto */}
            <div style={{ display: "flex", flexWrap: "wrap", gap: 16, marginBottom: 24 }}>
              <StatCard icon={<Banknote size={24} color="#f97316" />} label="Doanh thu" color="#f97316" bg="#fff7ed"
                value={`${(m.revenueLast30Days ?? 0).toLocaleString("vi-VN")}đ`}
                sub={`Số dư ví: ${(m.walletBalance ?? 0).toLocaleString("vi-VN")}đ`}
              />
              <StatCard icon={<Zap size={24} color="#3b82f6" />} label="Lượt booking" color="#3b82f6" bg="#eff6ff"
                value={m.bookingsLast30Days ?? 0}
                sub={`${m.totalStations ?? 0} trạm đang quản lý`}
              />
              <StatCard icon={<BarChart3 size={24} color="#22c55e" />} label="Hiệu suất hoạt động" color="#22c55e" bg="#f0fdf4"
                value={m.activeTimeUtilizationRate != null
                  ? `${(m.activeTimeUtilizationRate * 100).toFixed(1)}%`
                  : "—"}
                sub="Tỷ lệ giờ slot được đặt"
              />
              <StatCard icon={<XCircle size={24} color="#ef4444" />} label="Tỷ lệ hủy" color="#ef4444" bg="#fef2f2"
                value={m.cancelRateLast30Days != null
                  ? `${(m.cancelRateLast30Days * 100).toFixed(1)}%`
                  : "—"}
                sub="Driver + Owner hủy"
              />
            </div>

            <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 20, marginBottom: 24 }}>
              {/* Doanh thu từng trạm */}
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

              {/* Booking từng trạm */}
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

            {/* AI Insights */}
            <div style={{ background: "linear-gradient(135deg,#1e293b,#334155)", borderRadius: 20, padding: 24, boxShadow: "0 4px 20px rgba(0,0,0,0.15)" }}>
              <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", marginBottom: 14, flexWrap: "wrap", gap: 10 }}>
                <div>
                  <h3 style={{ fontSize: 15, fontWeight: 800, color: "#fff", margin: 0 }}> AI Cố vấn kinh doanh</h3>
                  <p style={{ fontSize: 12, color: "#94a3b8", margin: "4px 0 0" }}>Phân tích dựa trên số liệu thực tế của bạn</p>
                </div>
                <button onClick={handleLoadAI} disabled={aiLoading} style={{
                  padding: "10px 20px", borderRadius: 12, border: "none", cursor: aiLoading ? "not-allowed" : "pointer",
                  background: aiLoading ? "#475569" : "linear-gradient(135deg,#f97316,#ea580c)",
                  color: "#fff", fontSize: 13, fontWeight: 700, transition: "all 0.15s",
                  boxShadow: aiLoading ? "none" : "0 2px 8px rgba(249,115,22,0.4)",
                }}>
                  {aiLoading ? " Đang phân tích..." : " Lấy gợi ý AI"}
                </button>
              </div>

              {!aiInsight && !aiLoading && (
                <p style={{ fontSize: 13, color: "#64748b", fontStyle: "italic", margin: 0 }}>
                  Nhấn "Lấy gợi ý AI" để nhận phân tích chuyên sâu từ AI về tình hình kinh doanh.
                </p>
              )}

              {aiInsight?.error && (
                <p style={{ fontSize: 13, color: "#fca5a5", margin: 0 }}>️ {aiInsight.error}</p>
              )}

              {/* BE trả về: { insightMarkdown: string, generatedAt: DateTime } */}
              {aiInsight && !aiInsight.error && aiInsight.insightMarkdown && (
                <div style={{ background: "#0f172a", borderRadius: 14, padding: 18, marginTop: 4 }}>
                  <pre style={{
                    fontSize: 13, color: "#cbd5e1", lineHeight: 1.7, margin: 0,
                    whiteSpace: "pre-wrap", wordBreak: "break-word", fontFamily: "inherit",
                  }}>
                    {aiInsight.insightMarkdown}
                  </pre>
                  {aiInsight.generatedAt && (
                    <p style={{ fontSize: 11, color: "#475569", margin: "12px 0 0", textAlign: "right" }}>
                      Tạo lúc: {new Date(aiInsight.generatedAt).toLocaleString("vi-VN")}
                    </p>
                  )}
                </div>
              )}
            </div>
          </>
        )}
      </div>
    </div>
  );
}
