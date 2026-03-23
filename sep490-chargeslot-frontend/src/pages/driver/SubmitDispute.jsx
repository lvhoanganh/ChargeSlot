import { useState, useEffect, useRef } from "react";
import { useParams, useNavigate } from "react-router-dom";
import { bookingApi, disputeApi } from "@/services/api";

const REASONS = [
  "Sạc không đủ thời gian",
  "Trạm sạc hỏng / không hoạt động",
  "Giá tính sai",
  "Không thể check-in",
  "Vấn đề an toàn",
  "Khác",
];

const MAX_FILE_SIZE = 5 * 1024 * 1024; // 5MB per file

function getFileType(file) {
  if (file.type.startsWith("image/")) return "image";
  if (file.type.startsWith("video/")) return "video";
  return "document";
}

export default function SubmitDispute() {
  const { bookingId } = useParams();
  const navigate = useNavigate();
  const fileInputRef = useRef(null);

  const [booking, setBooking] = useState(null);
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState(false);

  const [reason, setReason] = useState("");
  const [description, setDescription] = useState("");
  const [evidences, setEvidences] = useState([]); // { file, preview, fileType, name }

  useEffect(() => {
    bookingApi
      .getById(Number(bookingId))
      .then(setBooking)
      .catch(() => setBooking(null))
      .finally(() => setLoading(false));
  }, [bookingId]);

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
    setEvidences((prev) => {
      const removed = prev[idx];
      if (removed?.preview) URL.revokeObjectURL(removed.preview);
      return prev.filter((_, i) => i !== idx);
    });
  }

  async function handleSubmit(e) {
    e.preventDefault();
    if (!reason) { setError("Vui lòng chọn lý do khiếu nại."); return; }
    if (!description.trim()) { setError("Vui lòng mô tả chi tiết vấn đề."); return; }

    setSubmitting(true);
    setError("");
    try {
      const files = evidences.map((ev) => ev.file);
      const result = await disputeApi.submit(
        Number(bookingId),
        reason,
        description,
        files,
      );
      setSuccess(true);
      setTimeout(() => navigate(`/driver/dispute/${result.id}`), 1500);
    } catch (err) {
      setError(err?.message || "Lỗi khi gửi khiếu nại, vui lòng thử lại.");
    } finally {
      setSubmitting(false);
    }
  }

  if (loading) {
    return (
      <div style={{ minHeight: "100vh", background: "#f8fafc", paddingTop: 100, textAlign: "center" }}>
        <div style={{ fontSize: 40 }}>⚡</div>
        <p style={{ color: "#6b7280" }}>Đang tải thông tin booking...</p>
      </div>
    );
  }

  if (!booking) {
    return (
      <div style={{ minHeight: "100vh", background: "#f8fafc", paddingTop: 100, textAlign: "center" }}>
        <div style={{ fontSize: 48 }}>📋</div>
        <h2 style={{ fontSize: 20, fontWeight: 700, color: "#1e293b" }}>Booking không tồn tại</h2>
        <button onClick={() => navigate("/driver/my-bookings")} style={btnStyle}>← Danh sách booking</button>
      </div>
    );
  }

  if (success) {
    return (
      <div style={{ minHeight: "100vh", background: "#f8fafc", paddingTop: 100, textAlign: "center" }}>
        <div style={{ fontSize: 64, marginBottom: 16 }}>✅</div>
        <h2 style={{ fontSize: 22, fontWeight: 700, color: "#16a34a", marginBottom: 8 }}>Gửi khiếu nại thành công!</h2>
        <p style={{ color: "#6b7280" }}>Đang chuyển đến trang chi tiết...</p>
      </div>
    );
  }

  return (
    <div style={{ minHeight: "100vh", background: "#f8fafc", paddingTop: 90 }}>
      <div style={{ maxWidth: 600, margin: "0 auto", padding: "0 16px 40px" }}>
        <button
          onClick={() => navigate(`/driver/booking/${bookingId}`)}
          style={{ background: "none", border: "none", cursor: "pointer", color: "#6b7280", fontSize: 14, marginBottom: 12, display: "flex", alignItems: "center", gap: 4 }}
        >
          <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><path d="M19 12H5M12 19l-7-7 7-7" /></svg>
          Quay lại booking
        </button>

        {/* Header */}
        <div style={{ background: "linear-gradient(135deg, #dc2626, #b91c1c)", borderRadius: 20, padding: "32px 24px", marginBottom: 24, textAlign: "center" }}>
          <div style={{ width: 56, height: 56, borderRadius: "50%", background: "rgba(255,255,255,0.2)", display: "flex", alignItems: "center", justifyContent: "center", margin: "0 auto 12px", fontSize: 28 }}>
            ⚠️
          </div>
          <h1 style={{ fontSize: 22, fontWeight: 800, color: "#fff", margin: 0 }}>Gửi khiếu nại</h1>
          <p style={{ color: "rgba(255,255,255,0.8)", fontSize: 14, marginTop: 4 }}>Booking #{bookingId} — {booking.stationName}</p>
        </div>

        {/* Form */}
        <form onSubmit={handleSubmit}>
          <div style={{ background: "#fff", borderRadius: 16, boxShadow: "0 2px 12px rgba(0,0,0,0.06)", padding: 24, marginBottom: 16 }}>
            {/* Reason */}
            <div style={{ marginBottom: 20 }}>
              <label style={labelStyle}>Lý do khiếu nại *</label>
              <select
                value={reason}
                onChange={(e) => setReason(e.target.value)}
                style={{ ...inputStyle, cursor: "pointer" }}
              >
                <option value="">— Chọn lý do —</option>
                {REASONS.map((r) => <option key={r} value={r}>{r}</option>)}
              </select>
            </div>

            {/* Description */}
            <div style={{ marginBottom: 20 }}>
              <label style={labelStyle}>Mô tả chi tiết *</label>
              <textarea
                value={description}
                onChange={(e) => setDescription(e.target.value)}
                placeholder="Mô tả chi tiết vấn đề bạn gặp phải..."
                maxLength={2000}
                rows={5}
                style={{ ...inputStyle, resize: "vertical", minHeight: 120 }}
              />
              <p style={{ fontSize: 12, color: "#9ca3af", textAlign: "right", marginTop: 4 }}>{description.length}/2000</p>
            </div>
          </div>

          {/* Evidence Upload */}
          <div style={{ background: "#fff", borderRadius: 16, boxShadow: "0 2px 12px rgba(0,0,0,0.06)", padding: 24, marginBottom: 16 }}>
            <label style={{ ...labelStyle, marginBottom: 12 }}>Bằng chứng (hình ảnh / video)</label>

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
                border: "2px dashed #d1d5db", borderRadius: 12, padding: "24px 16px",
                textAlign: "center", cursor: "pointer", marginBottom: 12,
                background: "#fafafa", transition: "all 0.2s",
              }}
              onDragOver={(e) => { e.preventDefault(); e.currentTarget.style.borderColor = "#3b82f6"; e.currentTarget.style.background = "#eff6ff"; }}
              onDragLeave={(e) => { e.currentTarget.style.borderColor = "#d1d5db"; e.currentTarget.style.background = "#fafafa"; }}
              onDrop={(e) => {
                e.preventDefault();
                e.currentTarget.style.borderColor = "#d1d5db";
                e.currentTarget.style.background = "#fafafa";
                const dt = e.dataTransfer;
                if (dt?.files?.length) handleFilesSelected({ target: { files: dt.files } });
              }}
            >
              <div style={{ fontSize: 32, marginBottom: 8 }}>📁</div>
              <p style={{ fontSize: 14, fontWeight: 600, color: "#374151", margin: 0 }}>Nhấn để chọn file hoặc kéo thả vào đây</p>
              <p style={{ fontSize: 12, color: "#9ca3af", marginTop: 4 }}>Hỗ trợ ảnh, video — tối đa 5MB/file</p>
            </div>

            {/* Preview grid */}
            {evidences.length > 0 && (
              <div style={{ display: "flex", flexWrap: "wrap", gap: 10, marginTop: 8 }}>
                {evidences.map((ev, idx) => (
                  <div key={idx} style={{ position: "relative", width: 90, height: 90, borderRadius: 10, overflow: "hidden", border: "2px solid #e5e7eb", background: "#f3f4f6" }}>
                    {ev.preview ? (
                      <img src={ev.preview} alt={ev.name} style={{ width: "100%", height: "100%", objectFit: "cover" }} />
                    ) : (
                      <div style={{ display: "flex", flexDirection: "column", alignItems: "center", justifyContent: "center", height: "100%", fontSize: 11, color: "#6b7280", padding: 4, textAlign: "center" }}>
                        <span style={{ fontSize: 22 }}>{ev.fileType === "video" ? "🎬" : "📄"}</span>
                        <span style={{ overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap", width: "100%" }}>{ev.name}</span>
                      </div>
                    )}
                    <button
                      type="button"
                      onClick={() => removeEvidence(idx)}
                      style={{
                        position: "absolute", top: 2, right: 2, width: 22, height: 22, borderRadius: "50%",
                        background: "rgba(239,68,68,0.9)", color: "#fff", border: "none", fontSize: 12,
                        cursor: "pointer", display: "flex", alignItems: "center", justifyContent: "center", lineHeight: 1,
                      }}
                    >
                      ✕
                    </button>
                  </div>
                ))}
              </div>
            )}
          </div>

          {/* Error */}
          {error && (
            <div style={{ background: "#fef2f2", border: "1px solid #fecaca", borderRadius: 12, padding: "12px 16px", marginBottom: 16 }}>
              <p style={{ fontSize: 14, color: "#dc2626", margin: 0 }}>❌ {error}</p>
            </div>
          )}

          {/* Submit */}
          <button
            type="submit"
            disabled={submitting}
            style={{
              width: "100%", padding: "16px 0", borderRadius: 14, border: "none",
              background: submitting ? "#d1d5db" : "linear-gradient(135deg, #dc2626, #b91c1c)",
              color: "#fff", fontWeight: 700, fontSize: 16, cursor: submitting ? "not-allowed" : "pointer",
              boxShadow: "0 4px 14px rgba(220,38,38,0.3)", transition: "all 0.2s",
            }}
          >
            {submitting ? "Đang gửi..." : "📤 Gửi khiếu nại"}
          </button>
        </form>
      </div>
    </div>
  );
}

const labelStyle = { display: "block", fontSize: 14, fontWeight: 600, color: "#374151", marginBottom: 6 };
const inputStyle = { width: "100%", padding: "10px 14px", borderRadius: 10, border: "1px solid #e5e7eb", fontSize: 14, outline: "none", boxSizing: "border-box" };
const btnStyle = { marginTop: 16, padding: "10px 20px", borderRadius: 10, border: "none", background: "#f97316", color: "#fff", fontWeight: 600, cursor: "pointer" };
