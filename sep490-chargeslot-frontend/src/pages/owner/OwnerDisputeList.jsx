import { useState, useEffect } from "react";
import { useNavigate } from "react-router-dom";
import { disputeApi } from "@/services/api";
import Pagination from "@/components/Pagination";

const STATUS_MAP = {
  WaitingOwnerEvidence: { label: "Chờ bạn phản hồi", color: "#f97316", bg: "#fff7ed", icon: "" },
  PendingReview: { label: "Chờ Admin xem xét", color: "#3b82f6", bg: "#eff6ff", icon: "" },
  ResolvedRefund: { label: "Tài xế thắng — Hoàn tiền", color: "#dc2626", bg: "#fef2f2", icon: "" },
  ResolvedPayout: { label: "Bạn thắng — Đã thanh toán", color: "#16a34a", bg: "#f0fdf4", icon: "" },
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
  const [strikeWarnings, setStrikeWarnings] = useState([]);
  
  const [page, setPage] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const [dateFrom, setDateFrom] = useState("");
  const [dateTo, setDateTo] = useState("");

  useEffect(() => {
    setLoading(true);
    disputeApi
      .getOwnerDisputes(page, 20)
      .then((data) => {
        // BE trả { totalCount, page, pageSize, items } — phải unpack .items
        const list = data?.items ?? (Array.isArray(data) ? data : []);
        setDisputes(list);
        setTotalCount(data?.totalCount ?? data?.total ?? list.length);

        // Load strike status cho tất cả station xuất hiện trong disputes
        const stationIds = [...new Set(list.map(d => d.booking?.chargingSlot?.stationId || d.stationId).filter(Boolean))];
        if (stationIds.length > 0) {
          Promise.all(
            stationIds.map(id =>
              disputeApi.getStationStrikeStatus(id)
                .then(s => ({ ...s, stationId: id }))
                .catch(() => null)
            )
          ).then(results => {
            setStrikeWarnings(results.filter(r => r && (r.loseCountThisMonth > 0 || r.isBanned)));
          });
        }
      })
      .catch((e) => setError(e?.message || "Không thể tải danh sách khiếu nại."))
      .finally(() => setLoading(false));
  }, [page]);

  const filteredDisputes = disputes.filter((d) => {
    const day = (d.createdAt || "").slice(0, 10);
    if (dateFrom && day < dateFrom) return false;
    if (dateTo && day > dateTo) return false;
    return true;
  });

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
            <span style={{ fontSize: 40 }}>️</span>
            <div>
              <h1 style={{ fontSize: 22, fontWeight: 800, color: "#fff", margin: 0 }}>Khiếu nại trạm của tôi</h1>
              <p style={{ fontSize: 13, color: "rgba(255,255,255,0.7)", margin: "4px 0 0" }}>
                Danh sách khiếu nại của trạm
              </p>
            </div>
          </div>
        </div>

        {/* Strike Warning Banners */}
        {strikeWarnings.length > 0 && strikeWarnings.map((sw, idx) => (
          <div key={idx} style={{
            borderRadius: 16, padding: "14px 18px", marginBottom: 12,
            background: sw.isBanned ? "linear-gradient(135deg, #fef2f2, #fee2e2)" : "linear-gradient(135deg, #fffbeb, #fef3c7)",
            border: sw.isBanned ? "1.5px solid #fca5a5" : "1.5px solid #fde68a",
            display: "flex", alignItems: "flex-start", gap: 10,
          }}>
            <span style={{ fontSize: 20, flexShrink: 0, lineHeight: 1 }}>{sw.isBanned ? "🚫" : "⚠️"}</span>
            <div style={{ flex: 1 }}>
              <p style={{ fontSize: 13, fontWeight: 700, color: sw.isBanned ? "#dc2626" : "#92400e", margin: "0 0 2px" }}>
                {sw.isBanned ? "Trạm đang bị đình chỉ" : "Cảnh báo chất lượng trạm"}
              </p>
              <p style={{ fontSize: 12, color: sw.isBanned ? "#b91c1c" : "#78350f", margin: 0, lineHeight: 1.4 }}>
                {sw.isBanned
                  ? `Trạm bị đình chỉ${sw.bannedUntil ? ` đến ${new Date(sw.bannedUntil).toLocaleDateString("vi-VN")}` : ""}. Số lần bị phạt: ${sw.banCount}.`
                  : `Trạm đã thua ${sw.loseCountThisMonth}/${sw.banThreshold} lượt khiếu nại tháng này. Còn ${sw.remainingBeforeBan} lượt nữa trạm sẽ bị đình chỉ 30 ngày.`
                }
              </p>
            </div>
          </div>
        ))}

        {/* Date range filter */}
        <div style={{ display: "flex", alignItems: "center", gap: 8, marginBottom: 20, flexWrap: "wrap" }}>
          <svg width="15" height="15" fill="none" stroke="#64748b" strokeWidth={2} viewBox="0 0 24 24" style={{ flexShrink: 0 }}>
            <rect x="3" y="4" width="18" height="18" rx="2" /><line x1="16" y1="2" x2="16" y2="6"/><line x1="8" y1="2" x2="8" y2="6"/><line x1="3" y1="10" x2="21" y2="10"/>
          </svg>
          <input type="date" value={dateFrom} onChange={(e) => setDateFrom(e.target.value)}
            style={{ height: 36, padding: "0 10px", borderRadius: 10, border: "1.5px solid #e2e8f0", background: "#fff", fontSize: 13, outline: "none", color: "#475569" }}
            title="Từ ngày" />
          <span style={{ color: "#94a3b8", fontSize: 12 }}>—</span>
          <input type="date" value={dateTo} onChange={(e) => setDateTo(e.target.value)}
            style={{ height: 36, padding: "0 10px", borderRadius: 10, border: "1.5px solid #e2e8f0", background: "#fff", fontSize: 13, outline: "none", color: "#475569" }}
            title="Đến ngày" />
          {(dateFrom || dateTo) && (
            <button onClick={() => { setDateFrom(""); setDateTo(""); }}
              style={{ height: 34, padding: "0 12px", borderRadius: 10, border: "1px solid #fca5a5", background: "#fff", color: "#dc2626", fontSize: 12, fontWeight: 600, cursor: "pointer" }}>
              × Xóa
            </button>
          )}
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
            <p style={{ color: "#dc2626", fontSize: 14, margin: 0 }}> {error}</p>
            <button
              onClick={() => window.location.reload()}
              style={{ marginTop: 10, padding: "8px 16px", borderRadius: 8, border: "none", background: "#f97316", color: "#fff", fontWeight: 600, cursor: "pointer", fontSize: 13 }}
            >
              Thử lại
            </button>
          </div>
        ) : filteredDisputes.length === 0 ? (
          <div style={{ background: "#fff", borderRadius: 16, padding: "50px 24px", textAlign: "center", boxShadow: "0 2px 12px rgba(0,0,0,0.06)" }}>
            <div style={{ fontSize: 56, marginBottom: 12 }}></div>
            <h3 style={{ fontSize: 18, fontWeight: 700, color: "#1e293b", margin: "0 0 8px" }}>
              Chưa có khiếu nại nào
            </h3>
            <p style={{ fontSize: 13, color: "#9ca3af", margin: 0 }}>
              Khi Driver gửi khiếu nại liên quan đến trạm của bạn, chúng sẽ xuất hiện ở đây.
            </p>
          </div>
        ) : (
          <div style={{ display: "flex", flexDirection: "column", gap: 12 }}>
            {filteredDisputes.map((dispute) => {
              const st = STATUS_MAP[dispute.status] || STATUS_MAP.WaitingOwnerEvidence;
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
                      <span style={{ fontSize: 14 }}></span>
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
                       {dispute.createdByName}
                    </p>
                  )}

                  {/* Strike Status — thông báo nếu Driver bị phạt do dispute này */}
                  {(dispute.strikeStatus || dispute.driverStrikeAdded) && (
                    <div style={{
                      marginTop: 8, padding: "6px 10px",
                      background: "#fef3c7", border: "1px solid #fde68a",
                      borderRadius: 8, display: "flex", alignItems: "center", gap: 6,
                    }}>
                      <span style={{ fontSize: 14 }}>️</span>
                      <span style={{ fontSize: 12, color: "#92400e", fontWeight: 600 }}>
                        Driver bị xử lý:{" "}
                        {dispute.strikeStatus === "Warned" ? "Cảnh cáo tài khoản"
                          : dispute.strikeStatus === "Suspended" ? "Đình chỉ tài khoản"
                          : dispute.strikeStatus === "Banned" ? "Cấm tài khoản"
                          : dispute.driverStrikeAdded ? "Strike đã được ghi nhận"
                          : dispute.strikeStatus}
                      </span>
                    </div>
                  )}

                  <div style={{ display: "flex", justifyContent: "flex-end", marginTop: 10 }}>
                    <span style={{ fontSize: 12, color: "#f97316", fontWeight: 600 }}>
                      {needsAction ? "Phản hồi ngay →" : "Xem chi tiết →"}
                    </span>
                  </div>
                </div>
              );
            })}
            <Pagination 
              page={page} 
              totalCount={totalCount} 
              pageSize={20} 
              onPageChange={(p) => setPage(p)} 
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
