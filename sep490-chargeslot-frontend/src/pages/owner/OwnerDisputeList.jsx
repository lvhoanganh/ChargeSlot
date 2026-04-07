import { useState, useEffect } from "react";
import { useNavigate } from "react-router-dom";
import { disputeApi } from "@/services/api";

const STATUS_MAP = {
  Open: { label: "Mở", color: "#f59e0b", bg: "#fffbeb", icon: "📝" },
  WaitingOwnerEvidence: { label: "Chờ bạn phản hồi", color: "#f97316", bg: "#fff7ed", icon: "⏳" },
  PendingReview: { label: "Chờ Admin xem xét", color: "#3b82f6", bg: "#eff6ff", icon: "🔍" },
  ResolvedRefund: { label: "Driver thắng — Hoàn tiền", color: "#dc2626", bg: "#fef2f2", icon: "❌" },
  ResolvedPayout: { label: "Bạn thắng — Đã thanh toán", color: "#16a34a", bg: "#f0fdf4", icon: "✅" },
};

const toLocal = (dt) => {
  if (!dt) return "—";
  return new Date(String(dt).replace("Z", "")).toLocaleDateString("vi-VN");
};

export default function OwnerDisputeList() {
  const navigate = useNavigate();
  const [disputes, setDisputes] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [filter, setFilter] = useState("all");

  useEffect(() => {
    disputeApi
      .getOwnerDisputes()
      .then((data) => setDisputes(Array.isArray(data) ? data : []))
      .catch((e) => setError(e?.message || "Không thể tải danh sách khiếu nại."))
      .finally(() => setLoading(false));
  }, []);

  const filtered =
    filter === "all" ? disputes : disputes.filter((d) => d.status === filter);

  const counts = {
    all: disputes.length,
    WaitingOwnerEvidence: disputes.filter((d) => d.status === "WaitingOwnerEvidence").length,
    PendingReview: disputes.filter((d) => d.status === "PendingReview").length,
    resolved: disputes.filter((d) =>
      d.status === "ResolvedRefund" || d.status === "ResolvedPayout"
    ).length,
  };

  const TABS = [
    { key: "all", label: "Tất cả", count: counts.all },
    { key: "WaitingOwnerEvidence", label: "Cần phản hồi", count: counts.WaitingOwnerEvidence },
    { key: "PendingReview", label: "Đang xét", count: counts.PendingReview },
  ];

  return (
    <div style={{ minHeight: "100vh", background: "#f8fafc", paddingTop: 90, paddingBottom: 40 }}>
      <div style={{ maxWidth: 640, margin: "0 auto", padding: "0 16px" }}>
        {/* Header */}
        <div style={{
          background: "linear-gradient(135deg, #f97316, #ea580c)",
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
            <span style={{ fontSize: 40 }}>⚠️</span>
            <div>
              <h1 style={{ fontSize: 22, fontWeight: 800, color: "#fff", margin: 0 }}>Khiếu nại trạm của tôi</h1>
              <p style={{ fontSize: 13, color: "rgba(255,255,255,0.7)", margin: "4px 0 0" }}>
                {disputes.length} khiếu nại
                {counts.WaitingOwnerEvidence > 0 && (
                  <span style={{ marginLeft: 8, background: "rgba(255,255,255,0.2)", padding: "2px 8px", borderRadius: 10, fontWeight: 700 }}>
                    {counts.WaitingOwnerEvidence} cần phản hồi
                  </span>
                )}
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

        {/* Content */}
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
            <p style={{ color: "#dc2626", fontSize: 14, margin: 0 }}>❌ {error}</p>
            <button
              onClick={() => window.location.reload()}
              style={{ marginTop: 10, padding: "8px 16px", borderRadius: 8, border: "none", background: "#f97316", color: "#fff", fontWeight: 600, cursor: "pointer", fontSize: 13 }}
            >
              Thử lại
            </button>
          </div>
        ) : filtered.length === 0 ? (
          <div style={{ background: "#fff", borderRadius: 16, padding: "50px 24px", textAlign: "center", boxShadow: "0 2px 12px rgba(0,0,0,0.06)" }}>
            <div style={{ fontSize: 56, marginBottom: 12 }}>📋</div>
            <h3 style={{ fontSize: 18, fontWeight: 700, color: "#1e293b", margin: "0 0 8px" }}>
              {filter === "all" ? "Chưa có khiếu nại nào" : "Không có khiếu nại nào"}
            </h3>
            <p style={{ fontSize: 13, color: "#9ca3af", margin: 0 }}>
              {filter === "all"
                ? "Khi Driver gửi khiếu nại liên quan đến trạm của bạn, chúng sẽ xuất hiện ở đây."
                : "Không có khiếu nại nào với trạng thái này."}
            </p>
          </div>
        ) : (
          <div style={{ display: "flex", flexDirection: "column", gap: 12 }}>
            {filtered.map((dispute) => {
              const st = STATUS_MAP[dispute.status] || STATUS_MAP.Open;
              const needsAction = dispute.status === "WaitingOwnerEvidence";
              return (
                <div
                  key={dispute.id}
                  onClick={() => navigate(`/owner/dispute/${dispute.id}`)}
                  style={{
                    background: "#fff", borderRadius: 16, padding: "18px 20px",
                    boxShadow: needsAction
                      ? "0 2px 12px rgba(249,115,22,0.15)"
                      : "0 2px 12px rgba(0,0,0,0.06)",
                    cursor: "pointer", transition: "all 0.2s",
                    border: needsAction ? "1px solid #fdba74" : "1px solid #f1f5f9",
                  }}
                  onMouseEnter={(e) => {
                    e.currentTarget.style.boxShadow = "0 4px 20px rgba(0,0,0,0.1)";
                    e.currentTarget.style.transform = "translateY(-2px)";
                  }}
                  onMouseLeave={(e) => {
                    e.currentTarget.style.boxShadow = needsAction
                      ? "0 2px 12px rgba(249,115,22,0.15)"
                      : "0 2px 12px rgba(0,0,0,0.06)";
                    e.currentTarget.style.transform = "translateY(0)";
                  }}
                >
                  {needsAction && (
                    <div style={{
                      background: "#fff7ed", borderRadius: 8, padding: "6px 12px",
                      marginBottom: 12, display: "flex", alignItems: "center", gap: 6,
                    }}>
                      <span style={{ fontSize: 14 }}>🔔</span>
                      <span style={{ fontSize: 12, fontWeight: 700, color: "#ea580c" }}>
                        Cần phản hồi của bạn!
                      </span>
                    </div>
                  )}

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

                  {dispute.createdByName && (
                    <p style={{ fontSize: 12, color: "#6b7280", marginTop: 8, marginBottom: 0 }}>
                      👤 {dispute.createdByName}
                    </p>
                  )}

                  <div style={{ display: "flex", justifyContent: "flex-end", marginTop: 10 }}>
                    <span style={{ fontSize: 12, color: "#f97316", fontWeight: 600 }}>
                      {needsAction ? "Phản hồi ngay →" : "Xem chi tiết →"}
                    </span>
                  </div>
                </div>
              );
            })}
          </div>
        )}
      </div>

      <style>{`
        @keyframes spin { to { transform: rotate(360deg); } }
      `}</style>
    </div>
  );
}
