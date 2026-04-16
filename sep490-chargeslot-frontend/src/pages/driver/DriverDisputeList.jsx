import { useState, useEffect } from "react";
import { useNavigate } from "react-router-dom";
import { disputeApi } from "@/services/api";
import Pagination from "@/components/Pagination";

const STATUS_MAP = {
  WaitingOwnerEvidence: { label: "Chờ Owner phản hồi", color: "#f97316", bg: "#fff7ed", icon: "" },
  PendingReview: { label: "Chờ Admin xem xét", color: "#3b82f6", bg: "#eff6ff", icon: "" },
  ResolvedRefund: { label: "Thắng — Hoàn tiền", color: "#16a34a", bg: "#f0fdf4", icon: "" },
  ResolvedPayout: { label: "Thua — Thanh toán Owner", color: "#8b5cf6", bg: "#f5f3ff", icon: "" },
};

const toLocal = (dt) => {
  if (!dt) return "—";
  return new Date(String(dt).replace("Z", "")).toLocaleDateString("vi-VN");
};

export default function DriverDisputeList() {
  const navigate = useNavigate();
  const [disputes, setDisputes] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [filter, setFilter] = useState("all");
  const [dateFrom, setDateFrom] = useState("");
  const [dateTo, setDateTo] = useState("");
  const [page, setPage] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const PAGE_SIZE = 20;

  useEffect(() => {
    setLoading(true);
    disputeApi
      .getMyDisputes(page, PAGE_SIZE)
      .then((data) => {
        // BE trả { total, page, pageSize, items } — phải unpack .items
        const list = data?.items ?? (Array.isArray(data) ? data : []);
        setDisputes(list);
        setTotalCount(data?.totalCount ?? data?.total ?? list.length);
      })
      .catch((e) => setError(e?.message || "Không thể tải danh sách khiếu nại."))
      .finally(() => setLoading(false));
  }, [page]);

  const filtered = disputes.filter((d) => {
    if (filter !== "all" && d.status !== filter) return false;
    const day = (d.createdAt || "").slice(0, 10);
    if (dateFrom && day < dateFrom) return false;
    if (dateTo && day > dateTo) return false;
    return true;
  });

  const counts = {
    all: disputes.length,
    Open: disputes.filter((d) => d.status === "Open").length,
    WaitingOwnerEvidence: disputes.filter((d) => d.status === "WaitingOwnerEvidence").length,
    PendingReview: disputes.filter((d) => d.status === "PendingReview").length,
    resolved: disputes.filter((d) =>
      d.status === "ResolvedRefund" || d.status === "ResolvedPayout"
    ).length,
  };

  const TABS = [
    { key: "all", label: "Tất cả", count: counts.all },
    { key: "WaitingOwnerEvidence", label: "Chờ Owner", count: counts.WaitingOwnerEvidence },
    { key: "PendingReview", label: "Đang xét", count: counts.PendingReview },
  ];

  return (
    <div style={{ minHeight: "100vh", background: "#f8fafc", paddingTop: 90, paddingBottom: 40 }}>
      <div style={{ maxWidth: 640, margin: "0 auto", padding: "0 16px" }}>
        {/* Header */}
        <div style={{
          background: "linear-gradient(135deg, #1e293b, #334155)",
          borderRadius: 20, padding: "28px 24px", marginBottom: 24,
        }}>
          <button
            onClick={() => navigate(-1)}
            style={{ background: "none", border: "none", color: "rgba(255,255,255,0.7)", cursor: "pointer", fontSize: 13, marginBottom: 12, display: "flex", alignItems: "center", gap: 4 }}
          >
            <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><path d="M19 12H5M12 19l-7-7 7-7" /></svg>
            Quay lại
          </button>
          <div style={{ display: "flex", alignItems: "center", gap: 14 }}>
            <span style={{ fontSize: 40 }}>️</span>
            <div>
              <h1 style={{ fontSize: 22, fontWeight: 800, color: "#fff", margin: 0 }}>Khiếu nại của tôi</h1>
              <p style={{ fontSize: 13, color: "rgba(255,255,255,0.6)", margin: "4px 0 0" }}>
                {disputes.length} khiếu nại
              </p>
            </div>
          </div>
        </div>

        {/* Filter Tabs */}
        <div style={{ display: "flex", gap: 8, marginBottom: 20, overflowX: "auto", paddingBottom: 4 }}>
          {TABS.map((tab) => (
            <button
              key={tab.key}
              onClick={() => setFilter(tab.key)}
              style={{
                padding: "8px 14px", borderRadius: 20, border: "none", cursor: "pointer",
                fontSize: 13, fontWeight: 600, whiteSpace: "nowrap",
                background: filter === tab.key ? "#f97316" : "#fff",
                color: filter === tab.key ? "#fff" : "#6b7280",
                boxShadow: "0 1px 4px rgba(0,0,0,0.08)",
                transition: "all 0.2s",
                flexShrink: 0,
              }}
            >
              {tab.label}
              {tab.count > 0 && (
                <span style={{
                  marginLeft: 6, fontSize: 11, fontWeight: 700,
                  background: filter === tab.key ? "rgba(255,255,255,0.3)" : "#f3f4f6",
                  color: filter === tab.key ? "#fff" : "#374151",
                  padding: "1px 6px", borderRadius: 10,
                }}>
                  {tab.count}
                </span>
              )}
            </button>
          ))}
        </div>
        {/* Date range filter */}
        <div style={{ display: "flex", alignItems: "center", gap: 8, marginBottom: 16, flexWrap: "wrap" }}>
          <svg width="15" height="15" fill="none" stroke="#64748b" strokeWidth={2} viewBox="0 0 24 24" style={{ flexShrink: 0 }}>
            <rect x="3" y="4" width="18" height="18" rx="2" /><line x1="16" y1="2" x2="16" y2="6"/><line x1="8" y1="2" x2="8" y2="6"/><line x1="3" y1="10" x2="21" y2="10"/>
          </svg>
          <input type="date" value={dateFrom} onChange={(e) => { setDateFrom(e.target.value); }}
            style={{ height: 36, padding: "0 10px", borderRadius: 10, border: "1.5px solid #e2e8f0", background: "#fff", fontSize: 13, outline: "none", color: "#475569" }}
            title="Từ ngày" />
          <span style={{ color: "#94a3b8", fontSize: 12 }}>—</span>
          <input type="date" value={dateTo} onChange={(e) => { setDateTo(e.target.value); }}
            style={{ height: 36, padding: "0 10px", borderRadius: 10, border: "1.5px solid #e2e8f0", background: "#fff", fontSize: 13, outline: "none", color: "#475569" }}
            title="Đến ngày" />
          {(dateFrom || dateTo) && (
            <button onClick={() => { setDateFrom(""); setDateTo(""); }}
              style={{ height: 34, padding: "0 12px", borderRadius: 10, border: "1px solid #fca5a5", background: "#fff", color: "#dc2626", fontSize: 12, fontWeight: 600, cursor: "pointer" }}>
              × Xóa
            </button>
          )}
        </div>

        {loading ? (
          <div style={{ textAlign: "center", padding: "60px 0" }}>
            <div style={{
              width: 40, height: 40, border: "3px solid #fed7aa",
              borderTop: "3px solid #f97316", borderRadius: "50%",
              animation: "spin 0.8s linear infinite", margin: "0 auto 12px",
            }} />
            <p style={{ color: "#9ca3af", fontSize: 14 }}>Đang tải...</p>
          </div>
        ) : error ? (
          <div style={{ background: "#fef2f2", border: "1px solid #fecaca", borderRadius: 12, padding: "16px 20px", textAlign: "center" }}>
            <p style={{ color: "#dc2626", fontSize: 14, margin: 0 }}> {error}</p>
            <button
              onClick={() => window.location.reload()}
              style={{ marginTop: 10, padding: "8px 16px", borderRadius: 8, border: "none", background: "#f97316", color: "#fff", fontWeight: 600, cursor: "pointer", fontSize: 13 }}
            >
              Thử lại
            </button>
          </div>
        ) : filtered.length === 0 ? (
          <div style={{ background: "#fff", borderRadius: 16, padding: "50px 24px", textAlign: "center", boxShadow: "0 2px 12px rgba(0,0,0,0.06)" }}>
            <div style={{ fontSize: 56, marginBottom: 12 }}></div>
            <h3 style={{ fontSize: 18, fontWeight: 700, color: "#1e293b", margin: "0 0 8px" }}>
              {filter === "all" ? "Chưa có khiếu nại nào" : "Không có khiếu nại nào"}
            </h3>
            <p style={{ fontSize: 13, color: "#9ca3af", margin: 0 }}>
              {filter === "all"
                ? "Khi bạn tạo khiếu nại, chúng sẽ xuất hiện ở đây."
                : "Không có khiếu nại nào với trạng thái này."}
            </p>
          </div>
        ) : (
          <div style={{ display: "flex", flexDirection: "column", gap: 12 }}>
            {filtered.map((dispute) => {
              const st = STATUS_MAP[dispute.status] || STATUS_MAP.Open;
              return (
                <div
                  key={dispute.id}
                  onClick={() => navigate(`/driver/dispute/${dispute.id}`)}
                  style={{
                    background: "#fff", borderRadius: 16, padding: "18px 20px",
                    boxShadow: "0 2px 12px rgba(0,0,0,0.06)",
                    cursor: "pointer", transition: "all 0.2s",
                    border: "1px solid #f1f5f9",
                  }}
                  onMouseEnter={(e) => {
                    e.currentTarget.style.boxShadow = "0 4px 20px rgba(0,0,0,0.1)";
                    e.currentTarget.style.transform = "translateY(-2px)";
                  }}
                  onMouseLeave={(e) => {
                    e.currentTarget.style.boxShadow = "0 2px 12px rgba(0,0,0,0.06)";
                    e.currentTarget.style.transform = "translateY(0)";
                  }}
                >
                  <div style={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", marginBottom: 10 }}>
                    <div style={{ display: "flex", alignItems: "center", gap: 10 }}>
                      <span style={{ fontSize: 24 }}>{st.icon}</span>
                      <div>
                        <p style={{ fontSize: 15, fontWeight: 700, color: "#1e293b", margin: 0 }}>
                          Khiếu nại #{dispute.id}
                        </p>
                        <p style={{ fontSize: 12, color: "#9ca3af", margin: "2px 0 0" }}>
                          Booking #{dispute.bookingId} • {toLocal(dispute.createdAt)}
                        </p>
                      </div>
                    </div>
                    <span style={{
                      fontSize: 11, fontWeight: 700, color: st.color,
                      background: st.bg, padding: "4px 10px", borderRadius: 20,
                      whiteSpace: "nowrap",
                    }}>
                      {st.label}
                    </span>
                  </div>

                  {dispute.reason && (
                    <div style={{
                      background: "#f8fafc", borderRadius: 10, padding: "8px 12px",
                      fontSize: 13, color: "#374151",
                    }}>
                      <span style={{ color: "#9ca3af", fontSize: 11, fontWeight: 600 }}>LÝ DO</span>
                      <p style={{ margin: "2px 0 0", fontWeight: 500 }}>{dispute.reason}</p>
                    </div>
                  )}

                  {/* Strike Status */}
                  {(dispute.strikeStatus || dispute.driverStrikeAdded) && (
                    <div style={{
                      marginTop: 8, padding: "6px 10px",
                      background: "#fef2f2", border: "1px solid #fecaca",
                      borderRadius: 8, display: "flex", alignItems: "center", gap: 6,
                    }}>
                      <span style={{ fontSize: 14 }}>️</span>
                      <span style={{ fontSize: 12, color: "#dc2626", fontWeight: 600 }}>
                        {dispute.strikeStatus === "Warned" ? "Đã cảnh cáo tài khoản"
                          : dispute.strikeStatus === "Suspended" ? "Tài khoản bị đình chỉ"
                          : dispute.strikeStatus === "Banned" ? "Tài khoản bị cấm"
                          : dispute.driverStrikeAdded ? "Strike đã được ghi nhận"
                          : `Strike: ${dispute.strikeStatus}`}
                      </span>
                    </div>
                  )}

                  <div style={{ display: "flex", justifyContent: "flex-end", marginTop: 10 }}>
                    <span style={{ fontSize: 12, color: "#f97316", fontWeight: 600 }}>
                      Xem chi tiết →
                    </span>
                  </div>
                </div>
              );
            })}
            <Pagination
              page={page}
              totalCount={totalCount}
              pageSize={PAGE_SIZE}
              onPageChange={(p) => { setPage(p); }}
            />
          </div>
        )}
      </div>

      <style>{`
        @keyframes spin { to { transform: rotate(360deg); } }
      `}</style>
    </div>
  );
}
