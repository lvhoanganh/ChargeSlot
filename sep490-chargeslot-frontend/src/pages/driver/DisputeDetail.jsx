import { useState, useEffect } from "react";
import { useParams, useNavigate } from "react-router-dom";
import { disputeApi } from "@/services/api";

const STATUS_MAP = {
  Open: { label: "Mở", color: "#f59e0b", bg: "#fffbeb", icon: "📝" },
  WaitingOwnerEvidence: { label: "Chờ Owner phản hồi", color: "#f97316", bg: "#fff7ed", icon: "⏳" },
  PendingReview: { label: "Chờ Admin xem xét", color: "#3b82f6", bg: "#eff6ff", icon: "🔍" },
  ResolvedRefund: { label: "Hoàn tiền cho Driver", color: "#16a34a", bg: "#f0fdf4", icon: "✅" },
  ResolvedPayout: { label: "Thanh toán cho Owner", color: "#8b5cf6", bg: "#f5f3ff", icon: "💰" },
};

const toLocal = (dt) => {
  if (!dt) return "—";
  const s = String(dt);
  return new Date(String(s).replace("Z", "")).toLocaleString("vi-VN");
};

export default function DisputeDetail() {
  const { disputeId } = useParams();
  const navigate = useNavigate();
  const [dispute, setDispute] = useState(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    disputeApi
      .getById(Number(disputeId))
      .then(setDispute)
      .catch(() => setDispute(null))
      .finally(() => setLoading(false));
  }, [disputeId]);

  if (loading) {
    return (
      <div style={{ minHeight: "100vh", background: "#f8fafc", paddingTop: 100, textAlign: "center" }}>
        <div style={{ fontSize: 40 }}>⚡</div>
        <p style={{ color: "#6b7280" }}>Đang tải thông tin khiếu nại...</p>
      </div>
    );
  }

  if (!dispute) {
    return (
      <div style={{ minHeight: "100vh", background: "#f8fafc", paddingTop: 100, textAlign: "center" }}>
        <div style={{ fontSize: 48 }}>📋</div>
        <h2 style={{ fontSize: 20, fontWeight: 700, color: "#1e293b" }}>Khiếu nại không tồn tại</h2>
        <button onClick={() => navigate("/driver/my-bookings")} style={btnStyle}>← Danh sách booking</button>
      </div>
    );
  }

  const st = STATUS_MAP[dispute.status] || STATUS_MAP.Open;
  const isResolved = dispute.status === "ResolvedRefund" || dispute.status === "ResolvedPayout";

  return (
    <div style={{ minHeight: "100vh", background: "#f8fafc", paddingTop: 90 }}>
      <div style={{ maxWidth: 600, margin: "0 auto", padding: "0 16px 40px" }}>
        <button
          onClick={() => navigate(`/driver/dispute-list`)}
          style={{ background: "none", border: "none", cursor: "pointer", color: "#6b7280", fontSize: 14, marginBottom: 12, display: "flex", alignItems: "center", gap: 4 }}
        >
          <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><path d="M19 12H5M12 19l-7-7 7-7" /></svg>
          Quay lại danh sách khiếu nại
        </button>

        {/* Status Header */}
        <div style={{ background: "linear-gradient(135deg, #1e293b, #334155)", borderRadius: 20, padding: "32px 24px", marginBottom: 24, textAlign: "center" }}>
          <div style={{ fontSize: 48, marginBottom: 12 }}>{st.icon}</div>
          <h1 style={{ fontSize: 22, fontWeight: 800, color: "#fff", margin: "0 0 8px" }}>Khiếu nại #{dispute.id}</h1>
          <span style={{ display: "inline-block", fontSize: 13, fontWeight: 700, color: st.color, background: st.bg, padding: "6px 16px", borderRadius: 20 }}>
            {st.label}
          </span>
        </div>

        {/* Timeline */}
        <div style={{ background: "#fff", borderRadius: 16, boxShadow: "0 2px 12px rgba(0,0,0,0.06)", overflow: "hidden", marginBottom: 16 }}>
          {/* Step 1: Submitted */}
          <TimelineStep
            icon="📤" title="Đã gửi khiếu nại"
            time={toLocal(dispute.createdAt)}
            isActive={true} isLast={false}
          >
            <InfoRow label="Lý do" value={dispute.reason} />
            <div style={{ marginTop: 8 }}>
              <span style={{ fontSize: 13, color: "#64748b" }}>Mô tả:</span>
              <p style={{ fontSize: 14, color: "#1e293b", marginTop: 4, lineHeight: 1.6 }}>{dispute.description}</p>
            </div>
          </TimelineStep>

          {/* Driver evidence */}
          {dispute.evidences?.length > 0 && (
            <TimelineStep icon="📎" title="Bằng chứng từ Driver" isActive={true} isLast={false}>
              <EvidenceGallery evidences={dispute.evidences} />
            </TimelineStep>
          )}

          {/* Step 2: Owner response */}
          <TimelineStep
            icon="🏢" title="Phản hồi từ Owner"
            isActive={!!dispute.ownerResponse}
            isLast={!isResolved}
          >
            {dispute.ownerResponse ? (
              <p style={{ fontSize: 14, color: "#1e293b", lineHeight: 1.6 }}>{dispute.ownerResponse}</p>
            ) : (
              <p style={{ fontSize: 13, color: "#9ca3af", fontStyle: "italic" }}>Đang chờ phản hồi...</p>
            )}
          </TimelineStep>

          {/* Step 3: Admin resolution */}
          {isResolved && (
            <TimelineStep
              icon="⚖️" title="Kết quả xử lý"
              time={dispute.resolvedAt ? toLocal(dispute.resolvedAt) : undefined}
              isActive={true} isLast={true}
            >
              <div style={{ padding: 12, borderRadius: 10, background: dispute.status === "ResolvedRefund" ? "#f0fdf4" : "#f5f3ff", marginBottom: 8 }}>
                <span style={{ fontSize: 14, fontWeight: 700, color: dispute.status === "ResolvedRefund" ? "#16a34a" : "#8b5cf6" }}>
                  {dispute.status === "ResolvedRefund" ? "✅ Driver thắng — Hoàn tiền" : "💰 Owner thắng — Thanh toán"}
                </span>
              </div>
              {dispute.adminNote && (
                <div style={{ marginTop: 8 }}>
                  <span style={{ fontSize: 13, color: "#64748b" }}>Ghi chú Admin:</span>
                  <p style={{ fontSize: 14, color: "#1e293b", marginTop: 4, lineHeight: 1.6 }}>{dispute.adminNote}</p>
                </div>
              )}
            </TimelineStep>
          )}
        </div>

        {/* Booking Info */}
        <div style={{ background: "#fff", borderRadius: 16, boxShadow: "0 2px 12px rgba(0,0,0,0.06)", padding: 20 }}>
          <h3 style={{ fontSize: 14, fontWeight: 700, color: "#374151", marginBottom: 12 }}>📋 Thông tin Booking</h3>
          <InfoRow label="Mã Booking" value={`#${dispute.bookingId}`} />
          <InfoRow label="Người khiếu nại" value={dispute.createdByName} />
          <InfoRow label="Ngày tạo" value={toLocal(dispute.createdAt)} />
        </div>
      </div>
    </div>
  );
}

function TimelineStep({ icon, title, time, isActive, isLast, children }) {
  return (
    <div style={{ display: "flex", padding: "20px 20px 0" }}>
      <div style={{ display: "flex", flexDirection: "column", alignItems: "center", marginRight: 16, flexShrink: 0 }}>
        <div style={{
          width: 36, height: 36, borderRadius: "50%",
          background: isActive ? "#eff6ff" : "#f3f4f6",
          display: "flex", alignItems: "center", justifyContent: "center", fontSize: 18,
          opacity: isActive ? 1 : 0.5,
        }}>
          {icon}
        </div>
        {!isLast && <div style={{ width: 2, flex: 1, background: isActive ? "#bfdbfe" : "#e5e7eb", marginTop: 8 }} />}
      </div>
      <div style={{ flex: 1, paddingBottom: 20 }}>
        <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 8 }}>
          <span style={{ fontWeight: 700, fontSize: 14, color: isActive ? "#1e293b" : "#9ca3af" }}>{title}</span>
          {time && <span style={{ fontSize: 12, color: "#9ca3af" }}>{time}</span>}
        </div>
        {children}
      </div>
    </div>
  );
}

function EvidenceGallery({ evidences }) {
  const toUrl = (url) => url?.startsWith("http") ? url : `https://chargeslot-api-f8b5brexe2b0ekhp.japaneast-01.azurewebsites.net${url}`;
  return (
    <div style={{ display: "flex", flexWrap: "wrap", gap: 8 }}>
      {evidences.map((ev) => (
        <a
          key={ev.id}
          href={toUrl(ev.fileUrl)}
          target="_blank"
          rel="noopener noreferrer"
          style={{
            display: "block", width: 80, height: 80, borderRadius: 10, overflow: "hidden",
            border: "2px solid #e5e7eb", position: "relative", background: "#f3f4f6",
          }}
        >
          {ev.fileType === "image" ? (
            <img src={toUrl(ev.fileUrl)} alt="evidence" style={{ width: "100%", height: "100%", objectFit: "cover" }} />
          ) : (
            <div style={{ display: "flex", alignItems: "center", justifyContent: "center", height: "100%", fontSize: 24 }}>
              {ev.fileType === "video" ? "🎬" : "📄"}
            </div>
          )}
        </a>
      ))}
    </div>
  );
}

function InfoRow({ label, value }) {
  return (
    <div style={{ display: "flex", justifyContent: "space-between", fontSize: 14, padding: "6px 0", borderBottom: "1px solid #f1f5f9" }}>
      <span style={{ color: "#64748b" }}>{label}</span>
      <span style={{ fontWeight: 600, color: "#1e293b" }}>{value}</span>
    </div>
  );
}

const btnStyle = { marginTop: 16, padding: "10px 20px", borderRadius: 10, border: "none", background: "#f97316", color: "#fff", fontWeight: 600, cursor: "pointer" };
