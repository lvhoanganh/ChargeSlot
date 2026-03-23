import { useState, useEffect, useRef } from "react";
import { useParams, useNavigate } from "react-router-dom";
import { disputeApi } from "@/services/api";

const STATUS_MAP = {
  Open: { label: "Mở", color: "#f59e0b", bg: "#fffbeb", icon: "📝" },
  WaitingOwnerEvidence: { label: "Chờ bạn phản hồi", color: "#f97316", bg: "#fff7ed", icon: "⏳" },
  PendingReview: { label: "Chờ Admin xem xét", color: "#3b82f6", bg: "#eff6ff", icon: "🔍" },
  ResolvedRefund: { label: "Hoàn tiền cho Driver", color: "#16a34a", bg: "#f0fdf4", icon: "✅" },
  ResolvedPayout: { label: "Thanh toán cho Owner", color: "#8b5cf6", bg: "#f5f3ff", icon: "💰" },
};

const MAX_FILE_SIZE = 5 * 1024 * 1024;

function getFileType(file) {
  if (file.type.startsWith("image/")) return "image";
  if (file.type.startsWith("video/")) return "video";
  return "document";
}

const toLocal = (dt) => {
  if (!dt) return "—";
  const s = String(dt);
  return new Date(s.endsWith("Z") ? s : s + "Z").toLocaleString("vi-VN");
};

export default function OwnerDisputeDetail() {
  const { disputeId } = useParams();
  const navigate = useNavigate();
  const fileInputRef = useRef(null);

  const [dispute, setDispute] = useState(null);
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState(false);

  const [response, setResponse] = useState("");
  const [evidences, setEvidences] = useState([]);

  useEffect(() => {
    disputeApi
      .getById(Number(disputeId))
      .then(setDispute)
      .catch(() => setDispute(null))
      .finally(() => setLoading(false));
  }, [disputeId]);

  function handleFilesSelected(e) {
    const files = Array.from(e.target.files || []);
    const newEvidences = [];
    for (const file of files) {
      if (file.size > MAX_FILE_SIZE) {
        setError(`File "${file.name}" vượt quá 5MB.`);
        continue;
      }
      newEvidences.push({
        file,
        preview: file.type.startsWith("image/") ? URL.createObjectURL(file) : null,
        fileType: getFileType(file),
        name: file.name,
      });
    }
    setEvidences((prev) => [...prev, ...newEvidences]);
    if (fileInputRef.current) fileInputRef.current.value = "";
  }

  function removeEvidence(idx) {
    setEvidences((prev) => prev.filter((_, i) => i !== idx));
  }

  async function handleSubmitEvidence(e) {
    e.preventDefault();
    setSubmitting(true);
    setError("");
    try {
      const files = evidences.map((ev) => ev.file);
      const result = await disputeApi.submitOwnerEvidence(
        Number(disputeId),
        response || "",
        files,
      );
      setDispute(result);
      setSuccess(true);
    } catch (err) {
      setError(err?.message || "Lỗi khi gửi phản hồi.");
    } finally {
      setSubmitting(false);
    }
  }

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
        <button onClick={() => navigate("/owner/booking-requests")} style={btnStyle}>← Danh sách booking</button>
      </div>
    );
  }

  const st = STATUS_MAP[dispute.status] || STATUS_MAP.Open;
  const canRespond = dispute.status === "WaitingOwnerEvidence" && !success;
  const isResolved = dispute.status === "ResolvedRefund" || dispute.status === "ResolvedPayout";

  return (
    <div style={{ minHeight: "100vh", background: "#f8fafc", paddingTop: 90 }}>
      <div style={{ maxWidth: 600, margin: "0 auto", padding: "0 16px 40px" }}>
        <button
          onClick={() => navigate("/owner/booking-requests")}
          style={{ background: "none", border: "none", cursor: "pointer", color: "#6b7280", fontSize: 14, marginBottom: 12, display: "flex", alignItems: "center", gap: 4 }}
        >
          <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><path d="M19 12H5M12 19l-7-7 7-7"/></svg>
          Quay lại
        </button>

        {/* Status Header */}
        <div style={{ background: "linear-gradient(135deg, #f97316, #ea580c)", borderRadius: 20, padding: "32px 24px", marginBottom: 24, textAlign: "center" }}>
          <div style={{ fontSize: 48, marginBottom: 12 }}>{st.icon}</div>
          <h1 style={{ fontSize: 22, fontWeight: 800, color: "#fff", margin: "0 0 8px" }}>Khiếu nại #{dispute.id}</h1>
          <span style={{ display: "inline-block", fontSize: 13, fontWeight: 700, color: st.color, background: "rgba(255,255,255,0.95)", padding: "6px 16px", borderRadius: 20 }}>
            {st.label}
          </span>
        </div>

        {/* Driver's complaint */}
        <div style={{ background: "#fff", borderRadius: 16, boxShadow: "0 2px 12px rgba(0,0,0,0.06)", padding: 20, marginBottom: 16 }}>
          <h3 style={{ fontSize: 14, fontWeight: 700, color: "#374151", marginBottom: 12 }}>📤 Khiếu nại từ Driver</h3>
          <InfoRow label="Người khiếu nại" value={dispute.createdByName} />
          <InfoRow label="Mã Booking" value={`#${dispute.bookingId}`} />
          <InfoRow label="Lý do" value={dispute.reason} />
          <InfoRow label="Ngày gửi" value={toLocal(dispute.createdAt)} />
          <div style={{ marginTop: 12 }}>
            <span style={{ fontSize: 13, color: "#64748b" }}>Mô tả:</span>
            <p style={{ fontSize: 14, color: "#1e293b", marginTop: 4, lineHeight: 1.6, background: "#f8fafc", padding: 12, borderRadius: 10 }}>{dispute.description}</p>
          </div>
          {dispute.evidences?.length > 0 && (
            <div style={{ marginTop: 12 }}>
              <span style={{ fontSize: 13, color: "#64748b", marginBottom: 8, display: "block" }}>Bằng chứng:</span>
              <EvidenceGallery evidences={dispute.evidences} />
            </div>
          )}
        </div>

        {/* Owner Response Form */}
        {canRespond && (
          <form onSubmit={handleSubmitEvidence}>
            <div style={{ background: "#fff", borderRadius: 16, boxShadow: "0 2px 12px rgba(0,0,0,0.06)", padding: 20, marginBottom: 16, border: "2px solid #f97316" }}>
              <h3 style={{ fontSize: 14, fontWeight: 700, color: "#f97316", marginBottom: 16 }}>🏢 Phản hồi của bạn</h3>

              <div style={{ marginBottom: 16 }}>
                <label style={labelStyle}>Nội dung phản hồi</label>
                <textarea
                  value={response}
                  onChange={(e) => setResponse(e.target.value)}
                  placeholder="Giải thích, phản bác hoặc xác nhận vấn đề..."
                  maxLength={2000}
                  rows={4}
                  style={{ ...inputStyle, resize: "vertical", minHeight: 100 }}
                />
              </div>

              {/* File upload evidence */}
              <div style={{ marginBottom: 16 }}>
                <label style={labelStyle}>Bằng chứng</label>
                <input
                  ref={fileInputRef}
                  type="file"
                  accept="image/*,video/*"
                  multiple
                  onChange={handleFilesSelected}
                  style={{ display: "none" }}
                />
                <div
                  onClick={() => fileInputRef.current?.click()}
                  style={{
                    border: "2px dashed #d1d5db", borderRadius: 12, padding: "20px 16px",
                    textAlign: "center", cursor: "pointer", background: "#fafafa",
                  }}
                >
                  <div style={{ fontSize: 28, marginBottom: 4 }}>📁</div>
                  <p style={{ fontSize: 13, fontWeight: 600, color: "#374151", margin: 0 }}>Nhấn để chọn file</p>
                  <p style={{ fontSize: 11, color: "#9ca3af", marginTop: 2 }}>Ảnh, video — tối đa 5MB/file</p>
                </div>

                {evidences.length > 0 && (
                  <div style={{ display: "flex", flexWrap: "wrap", gap: 8, marginTop: 10 }}>
                    {evidences.map((ev, idx) => (
                      <div key={idx} style={{ position: "relative", width: 80, height: 80, borderRadius: 10, overflow: "hidden", border: "2px solid #e5e7eb", background: "#f3f4f6" }}>
                        {ev.preview ? (
                          <img src={ev.preview} alt={ev.name} style={{ width: "100%", height: "100%", objectFit: "cover" }} />
                        ) : (
                          <div style={{ display: "flex", flexDirection: "column", alignItems: "center", justifyContent: "center", height: "100%", fontSize: 10, color: "#6b7280", padding: 4, textAlign: "center" }}>
                            <span style={{ fontSize: 20 }}>{ev.fileType === "video" ? "🎬" : "📄"}</span>
                            <span style={{ overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap", width: "100%" }}>{ev.name}</span>
                          </div>
                        )}
                        <button
                          type="button"
                          onClick={() => removeEvidence(idx)}
                          style={{
                            position: "absolute", top: 2, right: 2, width: 20, height: 20, borderRadius: "50%",
                            background: "rgba(239,68,68,0.9)", color: "#fff", border: "none", fontSize: 11,
                            cursor: "pointer", display: "flex", alignItems: "center", justifyContent: "center",
                          }}
                        >
                          ✕
                        </button>
                      </div>
                    ))}
                  </div>
                )}
              </div>

              {error && (
                <div style={{ background: "#fef2f2", border: "1px solid #fecaca", borderRadius: 10, padding: "10px 14px", marginBottom: 12 }}>
                  <p style={{ fontSize: 13, color: "#dc2626", margin: 0 }}>❌ {error}</p>
                </div>
              )}

              <button
                type="submit"
                disabled={submitting}
                style={{
                  width: "100%", padding: "14px 0", borderRadius: 12, border: "none",
                  background: submitting ? "#d1d5db" : "linear-gradient(135deg, #f97316, #ea580c)",
                  color: "#fff", fontWeight: 700, fontSize: 15, cursor: submitting ? "not-allowed" : "pointer",
                  boxShadow: "0 4px 14px rgba(249,115,22,0.3)",
                }}
              >
                {submitting ? "Đang gửi..." : "📤 Gửi phản hồi"}
              </button>
            </div>
          </form>
        )}

        {/* Success */}
        {success && (
          <div style={{ background: "#f0fdf4", border: "1px solid #bbf7d0", borderRadius: 12, padding: "14px 18px", marginBottom: 16, textAlign: "center" }}>
            <p style={{ fontSize: 14, fontWeight: 600, color: "#16a34a", margin: 0 }}>✅ Đã gửi phản hồi thành công! Admin sẽ xem xét sớm nhất.</p>
          </div>
        )}

        {/* Owner response (read-only if already responded) */}
        {dispute.ownerResponse && !canRespond && (
          <div style={{ background: "#fff", borderRadius: 16, boxShadow: "0 2px 12px rgba(0,0,0,0.06)", padding: 20, marginBottom: 16 }}>
            <h3 style={{ fontSize: 14, fontWeight: 700, color: "#374151", marginBottom: 12 }}>🏢 Phản hồi của bạn</h3>
            <p style={{ fontSize: 14, color: "#1e293b", lineHeight: 1.6, background: "#f8fafc", padding: 12, borderRadius: 10 }}>{dispute.ownerResponse}</p>
          </div>
        )}

        {/* Resolution */}
        {isResolved && (
          <div style={{ background: "#fff", borderRadius: 16, boxShadow: "0 2px 12px rgba(0,0,0,0.06)", padding: 20 }}>
            <h3 style={{ fontSize: 14, fontWeight: 700, color: "#374151", marginBottom: 12 }}>⚖️ Kết quả xử lý</h3>
            <div style={{ padding: 12, borderRadius: 10, background: dispute.status === "ResolvedRefund" ? "#fef2f2" : "#f0fdf4", marginBottom: 8 }}>
              <span style={{ fontSize: 14, fontWeight: 700, color: dispute.status === "ResolvedRefund" ? "#dc2626" : "#16a34a" }}>
                {dispute.status === "ResolvedRefund" ? "❌ Driver thắng — Hoàn tiền" : "✅ Owner thắng — Thanh toán cho bạn"}
              </span>
            </div>
            {dispute.adminNote && (
              <>
                <span style={{ fontSize: 13, color: "#64748b" }}>Ghi chú Admin:</span>
                <p style={{ fontSize: 14, color: "#1e293b", marginTop: 4, lineHeight: 1.6 }}>{dispute.adminNote}</p>
              </>
            )}
            {dispute.resolvedAt && <InfoRow label="Ngày xử lý" value={toLocal(dispute.resolvedAt)} />}
          </div>
        )}
      </div>
    </div>
  );
}

function EvidenceGallery({ evidences }) {
  const API_BASE = "http://localhost:5162";
  return (
    <div style={{ display: "flex", flexWrap: "wrap", gap: 8 }}>
      {evidences.map((ev) => {
        const url = ev.fileUrl?.startsWith("/") ? `${API_BASE}${ev.fileUrl}` : ev.fileUrl;
        return (
          <a key={ev.id} href={url} target="_blank" rel="noopener noreferrer"
            style={{ display: "block", width: 80, height: 80, borderRadius: 10, overflow: "hidden", border: "2px solid #e5e7eb", background: "#f3f4f6" }}>
            {ev.fileType === "image" ? (
              <img src={url} alt="evidence" style={{ width: "100%", height: "100%", objectFit: "cover" }} />
            ) : (
              <div style={{ display: "flex", alignItems: "center", justifyContent: "center", height: "100%", fontSize: 24 }}>
                {ev.fileType === "video" ? "🎬" : "📄"}
              </div>
            )}
          </a>
        );
      })}
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

const labelStyle = { display: "block", fontSize: 14, fontWeight: 600, color: "#374151", marginBottom: 6 };
const inputStyle = { width: "100%", padding: "10px 14px", borderRadius: 10, border: "1px solid #e5e7eb", fontSize: 14, outline: "none", boxSizing: "border-box" };
const btnStyle = { marginTop: 16, padding: "10px 20px", borderRadius: 10, border: "none", background: "#f97316", color: "#fff", fontWeight: 600, cursor: "pointer" };
