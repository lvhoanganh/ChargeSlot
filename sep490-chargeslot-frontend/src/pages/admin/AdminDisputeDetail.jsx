import { useState } from "react";
import { useParams, useNavigate } from "react-router-dom";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { instance } from "@/lib/httpRequest";
import { showToast } from "@/components/Toast";

/* ─── API helpers ─── */
const disputeApiAdmin = {
  getById: async (id) => {
    const { data } = await instance.get(`/dispute/${id}`);
    return data;
  },
  resolve: async (id, body) => {
    const { data } = await instance.post(`/dispute/${id}/resolve`, body);
    return data;
  },
};

function formatDate(dateStr) {
  if (!dateStr) return "—";
  const s = String(dateStr);
  const d = new Date(String(s).replace("Z", ""));
  return d.toLocaleString("vi-VN");
}

function getStatusLabel(status) {
  switch (status) {
    case "Open": return "Mở";
    case "WaitingOwnerEvidence": return "Chờ Owner phản hồi";
    case "PendingReview": return "Sẵn sàng xem xét";
    case "ResolvedRefund": return "Hoàn tiền Driver";
    case "ResolvedPayout": return "Thanh toán Owner";
    default: return status;
  }
}

function getStatusType(status) {
  switch (status) {
    case "Open": return "pending";
    case "WaitingOwnerEvidence": return "warning";
    case "PendingReview": return "info";
    case "ResolvedRefund": return "active";
    case "ResolvedPayout": return "purple";
    default: return "draft";
  }
}

function getStatusIcon(status) {
  switch (status) {
    case "Open": return "📝";
    case "WaitingOwnerEvidence": return "⏳";
    case "PendingReview": return "🔍";
    case "ResolvedRefund": return "✅";
    case "ResolvedPayout": return "💰";
    default: return "📄";
  }
}

export default function AdminDisputeDetail() {
  const { disputeId } = useParams();
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  const [showResolveModal, setShowResolveModal] = useState(false);
  const [isDriverWin, setIsDriverWin] = useState(true);
  const [adminNote, setAdminNote] = useState("");

  const { data: dispute, isLoading, error } = useQuery({
    queryKey: ["admin-dispute", disputeId],
    queryFn: () => disputeApiAdmin.getById(Number(disputeId)),
  });

  const resolveMutation = useMutation({
    mutationFn: ({ isDriverWin, adminNote }) =>
      disputeApiAdmin.resolve(Number(disputeId), { isDriverWin, adminNote }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["admin-dispute", disputeId] });
      queryClient.invalidateQueries({ queryKey: ["admin-disputes-pending"] });
      setShowResolveModal(false);
    },
    onError: (err) => {
      const msg = err?.response?.data?.message || err?.message || "Lỗi không xác định";
      showToast.error("Lỗi: " + msg);
    },
  });

  if (isLoading) {
    return (
      <div className="cs-dispute-detail">
        <div style={{ textAlign: "center", paddingTop: 120 }}>
          <div className="cs-dispute-detail__spinner" />
          <p style={{ color: "#64748b", fontSize: 14, marginTop: 16 }}>Đang tải chi tiết khiếu nại...</p>
        </div>
        <style>{styles}</style>
      </div>
    );
  }

  if (error || !dispute) {
    return (
      <div className="cs-dispute-detail">
        <div style={{ textAlign: "center", paddingTop: 120 }}>
          <p style={{ color: "#ef4444", fontSize: 16, marginBottom: 16 }}>❌ {error?.message || "Không tìm thấy khiếu nại"}</p>
          <button onClick={() => navigate("/admin/disputes")} className="cs-dispute-detail__btn-back-main">
            ← Danh sách khiếu nại
          </button>
        </div>
        <style>{styles}</style>
      </div>
    );
  }

  const statusType = getStatusType(dispute.status);
  const canResolve = dispute.status === "PendingReview" || dispute.status === "WaitingOwnerEvidence";
  const isResolved = dispute.status === "ResolvedRefund" || dispute.status === "ResolvedPayout";

  return (
    <div className="cs-dispute-detail">
      {/* Back button */}
      <button onClick={() => navigate("/admin/disputes")} className="cs-dispute-detail__back">
        <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><path d="M19 12H5M12 19l-7-7 7-7" /></svg>
        Danh sách khiếu nại
      </button>

      {/* Header Card */}
      <div className="cs-dispute-detail__header-card">
        <div className="cs-dispute-detail__header-top">
          <div>
            <h1 className="cs-dispute-detail__title">Khiếu nại #{dispute.id}</h1>
            <p className="cs-dispute-detail__subtitle">Booking #{dispute.bookingId}</p>
          </div>
          <span className={`cs-dispute-detail__status cs-dispute-detail__status--${statusType}`}>
            {getStatusIcon(dispute.status)} {getStatusLabel(dispute.status)}
          </span>
        </div>

        <div className="cs-dispute-detail__meta">
          <div className="cs-dispute-detail__meta-item">
            <span className="cs-dispute-detail__meta-label">Người khiếu nại</span>
            <span className="cs-dispute-detail__meta-value">{dispute.createdByName}</span>
          </div>
          <div className="cs-dispute-detail__meta-item">
            <span className="cs-dispute-detail__meta-label">Ngày tạo</span>
            <span className="cs-dispute-detail__meta-value">{formatDate(dispute.createdAt)}</span>
          </div>
          {dispute.resolvedAt && (
            <div className="cs-dispute-detail__meta-item">
              <span className="cs-dispute-detail__meta-label">Ngày xử lý</span>
              <span className="cs-dispute-detail__meta-value">{formatDate(dispute.resolvedAt)}</span>
            </div>
          )}
        </div>
      </div>

      {/* Two-column: Driver + Owner */}
      <div className="cs-dispute-detail__grid">
        {/* Driver's complaint */}
        <div className="cs-dispute-detail__card">
          <div className="cs-dispute-detail__card-header">
            <div className="cs-dispute-detail__card-icon cs-dispute-detail__card-icon--driver">🚗</div>
            <h2 className="cs-dispute-detail__card-title">Khiếu nại từ Driver</h2>
          </div>

          <div className="cs-dispute-detail__field">
            <span className="cs-dispute-detail__field-label">Lý do</span>
            <p className="cs-dispute-detail__field-value">{dispute.reason}</p>
          </div>

          <div className="cs-dispute-detail__field">
            <span className="cs-dispute-detail__field-label">Mô tả</span>
            <div className="cs-dispute-detail__field-box">{dispute.description}</div>
          </div>

          {dispute.evidences?.length > 0 && (
            <div className="cs-dispute-detail__field">
              <span className="cs-dispute-detail__field-label">Bằng chứng ({dispute.evidences.length})</span>
              <div className="cs-dispute-detail__evidences">
                {dispute.evidences.map((ev) => {
                  const url = ev.fileUrl?.startsWith("http") ? ev.fileUrl : `https://chargeslot-api-f8b5brexe2b0ekhp.japaneast-01.azurewebsites.net${ev.fileUrl}`;
                  return (
                    <a key={ev.id} href={url} target="_blank" rel="noopener noreferrer" className="cs-dispute-detail__evidence">
                      {ev.fileType === "image" ? (
                        <img src={url} alt="evidence" />
                      ) : (
                        <span>{ev.fileType === "video" ? "🎬" : "📄"}</span>
                      )}
                    </a>
                  );
                })}
              </div>
            </div>
          )}
        </div>

        {/* Owner's response */}
        <div className="cs-dispute-detail__card">
          <div className="cs-dispute-detail__card-header">
            <div className="cs-dispute-detail__card-icon cs-dispute-detail__card-icon--owner">🏢</div>
            <h2 className="cs-dispute-detail__card-title">Phản hồi từ Owner</h2>
          </div>

          {dispute.ownerResponse ? (
            <div className="cs-dispute-detail__field">
              <span className="cs-dispute-detail__field-label">Nội dung phản hồi</span>
              <div className="cs-dispute-detail__field-box">{dispute.ownerResponse}</div>
            </div>
          ) : (
            <div className="cs-dispute-detail__empty-response">
              <span>⏳</span>
              <p>Chưa có phản hồi từ Owner</p>
            </div>
          )}
        </div>
      </div>

      {/* Resolution result (if resolved) */}
      {isResolved && (
        <div className={`cs-dispute-detail__resolution cs-dispute-detail__resolution--${dispute.status === "ResolvedRefund" ? "refund" : "payout"}`}>
          <h2 className="cs-dispute-detail__resolution-title">⚖️ Kết quả xử lý</h2>
          <span className={`cs-dispute-detail__status cs-dispute-detail__status--${statusType}`} style={{ marginBottom: 12 }}>
            {dispute.status === "ResolvedRefund" ? "✅ Driver thắng — Hoàn tiền" : "💰 Owner thắng — Thanh toán"}
          </span>
          {dispute.adminNote && (
            <div className="cs-dispute-detail__field" style={{ marginTop: 12 }}>
              <span className="cs-dispute-detail__field-label">Ghi chú</span>
              <p className="cs-dispute-detail__field-value">{dispute.adminNote}</p>
            </div>
          )}
        </div>
      )}

      {/* Action button */}
      {canResolve && (
        <div style={{ display: "flex", justifyContent: "center", marginTop: 24 }}>
          <button onClick={() => setShowResolveModal(true)} className="cs-dispute-detail__resolve-btn">
            ⚖️ Phán quyết khiếu nại
          </button>
        </div>
      )}

      {/* Resolve Modal */}
      {showResolveModal && (
        <div className="cs-admin-modal-overlay">
          <div className="cs-admin-modal">
            <div className="cs-admin-modal__icon">⚖️</div>
            <h2 className="cs-admin-modal__title">Phán quyết khiếu nại #{dispute.id}</h2>

            {/* Decision radio */}
            <div style={{ textAlign: "left", marginBottom: 20 }}>
              <label style={{ display: "block", fontSize: 13, fontWeight: 600, color: "#374151", marginBottom: 10 }}>
                Kết quả phán quyết <span style={{ color: "#ef4444" }}>*</span>
              </label>
              <div style={{ display: "flex", flexDirection: "column", gap: 10 }}>
                <label className={`cs-dispute-detail__radio-option ${isDriverWin ? "cs-dispute-detail__radio-option--selected-green" : ""}`}>
                  <input type="radio" checked={isDriverWin} onChange={() => setIsDriverWin(true)} style={{ accentColor: "#22c55e" }} />
                  <div>
                    <span style={{ fontWeight: 600, fontSize: 13 }}>✅ Driver thắng — Hoàn tiền</span>
                    <p style={{ fontSize: 11, color: "#64748b", marginTop: 2 }}>Tiền từ ESCROW sẽ hoàn về ví Driver</p>
                  </div>
                </label>
                <label className={`cs-dispute-detail__radio-option ${!isDriverWin ? "cs-dispute-detail__radio-option--selected-purple" : ""}`}>
                  <input type="radio" checked={!isDriverWin} onChange={() => setIsDriverWin(false)} style={{ accentColor: "#7c3aed" }} />
                  <div>
                    <span style={{ fontWeight: 600, fontSize: 13 }}>💰 Owner thắng — Thanh toán</span>
                    <p style={{ fontSize: 11, color: "#64748b", marginTop: 2 }}>Tiền từ ESCROW sẽ chuyển cho Owner (trừ phí nền tảng)</p>
                  </div>
                </label>
              </div>
            </div>

            {/* Admin note */}
            <div style={{ textAlign: "left", marginBottom: 20 }}>
              <label style={{ display: "block", fontSize: 13, fontWeight: 600, color: "#374151", marginBottom: 6 }}>
                Ghi chú phán quyết <span style={{ color: "#ef4444" }}>*</span>
              </label>
              <textarea
                value={adminNote}
                onChange={(e) => setAdminNote(e.target.value)}
                placeholder="Lý do phán quyết, giải thích cho các bên..."
                maxLength={2000}
                rows={3}
                style={{
                  width: "100%", padding: "10px 14px", borderRadius: 12,
                  border: "1.5px solid #e5e7eb", fontSize: 14, outline: "none",
                  resize: "vertical", boxSizing: "border-box",
                }}
              />
            </div>

            <div className="cs-admin-modal__actions">
              <button
                onClick={() => { setShowResolveModal(false); setAdminNote(""); }}
                disabled={resolveMutation.isPending}
                className="cs-admin-modal__btn cs-admin-modal__btn--cancel"
              >
                Hủy
              </button>
              <button
                onClick={() => resolveMutation.mutate({ isDriverWin, adminNote })}
                disabled={resolveMutation.isPending || !adminNote.trim()}
                className={`cs-admin-modal__btn ${isDriverWin ? "cs-admin-modal__btn--success" : "cs-admin-modal__btn--purple"}`}
              >
                {resolveMutation.isPending ? "Đang xử lý..." : "Xác nhận phán quyết"}
              </button>
            </div>
          </div>
        </div>
      )}

      <style>{styles}</style>
    </div>
  );
}

const styles = `
  .cs-dispute-detail {
    max-width: 960px;
    width: 95%;
    margin: 0 auto;
    padding: 88px 0 40px;
  }

  .cs-dispute-detail__spinner {
    width: 28px; height: 28px;
    border: 3px solid #f3f4f6;
    border-top-color: #f97316;
    border-radius: 50%;
    animation: cs-spin 0.8s linear infinite;
    margin: 0 auto;
  }
  @keyframes cs-spin { to { transform: rotate(360deg); } }

  .cs-dispute-detail__btn-back-main {
    padding: 10px 20px;
    border-radius: 12px;
    border: none;
    background: linear-gradient(135deg, #f97316, #ea580c);
    color: white;
    font-weight: 600;
    font-size: 14px;
    cursor: pointer;
  }

  .cs-dispute-detail__back {
    display: inline-flex;
    align-items: center;
    gap: 6px;
    margin-bottom: 20px;
    color: #64748b;
    font-size: 14px;
    background: none;
    border: none;
    cursor: pointer;
    transition: color 0.2s;
  }
  .cs-dispute-detail__back:hover { color: #f97316; }

  /* Header Card */
  .cs-dispute-detail__header-card {
    background: white;
    border: 1px solid rgba(0,0,0,0.06);
    border-radius: 20px;
    padding: 28px;
    margin-bottom: 24px;
  }
  .cs-dispute-detail__header-top {
    display: flex;
    align-items: flex-start;
    justify-content: space-between;
    margin-bottom: 20px;
    flex-wrap: wrap;
    gap: 12px;
  }
  .cs-dispute-detail__title {
    font-size: 22px;
    font-weight: 800;
    color: #1e293b;
    letter-spacing: -0.5px;
  }
  .cs-dispute-detail__subtitle {
    font-size: 14px;
    color: #64748b;
    margin-top: 4px;
  }
  .cs-dispute-detail__status {
    display: inline-flex;
    align-items: center;
    gap: 6px;
    padding: 6px 16px;
    border-radius: 50px;
    font-size: 13px;
    font-weight: 600;
  }
  .cs-dispute-detail__status--pending { background: #fffbeb; color: #f59e0b; }
  .cs-dispute-detail__status--warning { background: #fff7ed; color: #f97316; }
  .cs-dispute-detail__status--info { background: #eff6ff; color: #3b82f6; }
  .cs-dispute-detail__status--active { background: #f0fdf4; color: #16a34a; }
  .cs-dispute-detail__status--purple { background: #f5f3ff; color: #7c3aed; }

  .cs-dispute-detail__meta {
    display: grid;
    grid-template-columns: repeat(3, 1fr);
    gap: 16px;
  }
  @media (max-width: 640px) {
    .cs-dispute-detail__meta { grid-template-columns: 1fr; }
  }
  .cs-dispute-detail__meta-item { }
  .cs-dispute-detail__meta-label { display: block; font-size: 12px; color: #94a3b8; text-transform: uppercase; letter-spacing: 0.5px; }
  .cs-dispute-detail__meta-value { display: block; font-size: 14px; font-weight: 600; color: #1e293b; margin-top: 4px; }

  /* Grid */
  .cs-dispute-detail__grid {
    display: grid;
    grid-template-columns: 1fr 1fr;
    gap: 20px;
    margin-bottom: 24px;
  }
  @media (max-width: 768px) {
    .cs-dispute-detail__grid { grid-template-columns: 1fr; }
  }

  /* Cards */
  .cs-dispute-detail__card {
    background: white;
    border: 1px solid rgba(0,0,0,0.06);
    border-radius: 20px;
    padding: 24px;
  }
  .cs-dispute-detail__card-header {
    display: flex;
    align-items: center;
    gap: 12px;
    margin-bottom: 20px;
  }
  .cs-dispute-detail__card-icon {
    width: 40px;
    height: 40px;
    border-radius: 12px;
    display: flex;
    align-items: center;
    justify-content: center;
    font-size: 18px;
  }
  .cs-dispute-detail__card-icon--driver { background: #fef2f2; }
  .cs-dispute-detail__card-icon--owner { background: #fff7ed; }
  .cs-dispute-detail__card-title {
    font-size: 16px;
    font-weight: 700;
    color: #1e293b;
  }
  .cs-dispute-detail__field {
    margin-bottom: 16px;
  }
  .cs-dispute-detail__field-label {
    display: block;
    font-size: 11px;
    color: #94a3b8;
    text-transform: uppercase;
    letter-spacing: 0.5px;
    margin-bottom: 6px;
  }
  .cs-dispute-detail__field-value {
    font-size: 14px;
    color: #1e293b;
    font-weight: 500;
  }
  .cs-dispute-detail__field-box {
    font-size: 14px;
    color: #374151;
    background: #f8fafc;
    padding: 14px;
    border-radius: 12px;
    line-height: 1.6;
  }
  .cs-dispute-detail__evidences {
    display: flex;
    flex-wrap: wrap;
    gap: 8px;
  }
  .cs-dispute-detail__evidence {
    width: 72px;
    height: 72px;
    border-radius: 12px;
    overflow: hidden;
    border: 2px solid #e5e7eb;
    transition: border-color 0.2s;
    display: flex;
    align-items: center;
    justify-content: center;
    background: #f8fafc;
    font-size: 24px;
  }
  .cs-dispute-detail__evidence:hover { border-color: #f97316; }
  .cs-dispute-detail__evidence img {
    width: 100%;
    height: 100%;
    object-fit: cover;
  }
  .cs-dispute-detail__empty-response {
    text-align: center;
    padding: 40px 0;
    color: #94a3b8;
  }
  .cs-dispute-detail__empty-response span { font-size: 32px; }
  .cs-dispute-detail__empty-response p { font-size: 14px; margin-top: 8px; }

  /* Resolution */
  .cs-dispute-detail__resolution {
    border-radius: 20px;
    padding: 24px;
    margin-bottom: 24px;
  }
  .cs-dispute-detail__resolution--refund {
    background: #f0fdf4;
    border: 1px solid #bbf7d0;
  }
  .cs-dispute-detail__resolution--payout {
    background: #f5f3ff;
    border: 1px solid #ddd6fe;
  }
  .cs-dispute-detail__resolution-title {
    font-size: 16px;
    font-weight: 700;
    color: #1e293b;
    margin-bottom: 12px;
  }

  /* Resolve button */
  .cs-dispute-detail__resolve-btn {
    padding: 14px 36px;
    border-radius: 14px;
    border: none;
    background: linear-gradient(135deg, #3b82f6, #2563eb);
    color: white;
    font-size: 15px;
    font-weight: 700;
    cursor: pointer;
    box-shadow: 0 8px 24px rgba(59,130,246,0.3);
    transition: all 0.3s;
  }
  .cs-dispute-detail__resolve-btn:hover {
    transform: translateY(-2px);
    box-shadow: 0 12px 32px rgba(59,130,246,0.4);
  }

  /* Radio Options */
  .cs-dispute-detail__radio-option {
    display: flex;
    align-items: center;
    gap: 12px;
    padding: 14px 16px;
    border-radius: 14px;
    border: 2px solid #e5e7eb;
    cursor: pointer;
    transition: all 0.2s;
  }
  .cs-dispute-detail__radio-option:hover { border-color: #d1d5db; }
  .cs-dispute-detail__radio-option--selected-green {
    border-color: #22c55e;
    background: #f0fdf4;
  }
  .cs-dispute-detail__radio-option--selected-purple {
    border-color: #7c3aed;
    background: #f5f3ff;
  }

  /* Modal (shared) */
  .cs-admin-modal-overlay {
    position: fixed;
    inset: 0;
    z-index: 50;
    background: rgba(0,0,0,0.5);
    backdrop-filter: blur(4px);
    display: flex;
    align-items: center;
    justify-content: center;
    padding: 24px;
  }
  .cs-admin-modal {
    width: 100%;
    max-width: 500px;
    background: white;
    border-radius: 20px;
    padding: 32px;
    text-align: center;
    animation: cs-fadeInUp 0.3s ease-out;
    box-shadow: 0 20px 60px rgba(0,0,0,0.2);
  }
  @keyframes cs-fadeInUp {
    from { opacity: 0; transform: translateY(20px); }
    to { opacity: 1; transform: translateY(0); }
  }
  .cs-admin-modal__icon { font-size: 40px; margin-bottom: 16px; }
  .cs-admin-modal__title {
    font-size: 20px;
    font-weight: 700;
    color: #1e293b;
    margin-bottom: 20px;
  }
  .cs-admin-modal__actions { display: flex; gap: 12px; }
  .cs-admin-modal__btn {
    flex: 1;
    height: 44px;
    border-radius: 12px;
    font-size: 14px;
    font-weight: 600;
    cursor: pointer;
    transition: all 0.2s;
    border: none;
  }
  .cs-admin-modal__btn--cancel { background: #f1f5f9; color: #374151; }
  .cs-admin-modal__btn--cancel:hover { background: #e2e8f0; }
  .cs-admin-modal__btn--success { background: #22c55e; color: white; }
  .cs-admin-modal__btn--success:hover { background: #16a34a; }
  .cs-admin-modal__btn--purple { background: #7c3aed; color: white; }
  .cs-admin-modal__btn--purple:hover { background: #6d28d9; }
  .cs-admin-modal__btn:disabled { opacity: 0.6; cursor: not-allowed; }
`;
