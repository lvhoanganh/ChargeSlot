import { useState, useEffect } from "react";
import { adminWithdrawApi } from "@/services/api";
import { showToast } from "@/components/Toast";

const statusLabels = {
  Pending: { label: "Chờ duyệt", color: "#f59e0b", bg: "#fffbeb" },
  Approved: { label: "Đã duyệt", color: "#22c55e", bg: "#f0fdf4" },
  Rejected: { label: "Từ chối", color: "#ef4444", bg: "#fef2f2" },
};

const toLocal = (dt) => {
  if (!dt) return "";
  return new Date(String(dt).replace("Z", "")).toLocaleString("vi-VN");
};

export default function AdminWithdraws() {
  const [withdraws, setWithdraws] = useState([]);
  const [loading, setLoading] = useState(true);
  const [processing, setProcessing] = useState(null);
  // Reject modal state
  const [rejectModal, setRejectModal] = useState(null); // { id }
  const [rejectNote, setRejectNote] = useState("");

  function fetchWithdraws() {
    setLoading(true);
    adminWithdrawApi.getPending()
      .then(data => setWithdraws(Array.isArray(data) ? data : []))
      .catch(() => setWithdraws([]))
      .finally(() => setLoading(false));
  }

  useEffect(() => { fetchWithdraws(); }, []);

  async function handleApprove(id) {
    setProcessing(id);
    try {
      await adminWithdrawApi.process(id, true, "");
      showToast.success("✅ Đã duyệt yêu cầu rút tiền — nhớ chuyển khoản thủ công cho tài xế!");
      fetchWithdraws();
    } catch (err) {
      showToast.error(err.message || "Lỗi duyệt yêu cầu");
    } finally {
      setProcessing(null);
    }
  }

  async function handleRejectSubmit() {
    if (!rejectNote.trim()) {
      showToast.error("Vui lòng nhập lý do từ chối!");
      return;
    }
    setProcessing(rejectModal.id);
    try {
      await adminWithdrawApi.process(rejectModal.id, false, rejectNote);
      showToast.success("Đã từ chối yêu cầu và hoàn tiền về ví Driver.");
      setRejectModal(null);
      setRejectNote("");
      fetchWithdraws();
    } catch (err) {
      showToast.error(err.message || "Lỗi từ chối yêu cầu");
    } finally {
      setProcessing(null);
    }
  }

  return (
    <div className="cs-admin-page">
      <div className="cs-admin-page__header">
        <div>
          <h1 className="cs-admin-page__title">🏦 Duyệt rút tiền Driver</h1>
          <p className="cs-admin-page__subtitle">
            Xử lý các yêu cầu rút tiền từ tài xế
          </p>
        </div>
      </div>

      {/* Reject Reason Modal */}
      {rejectModal && (
        <div style={{
          position: "fixed", inset: 0, zIndex: 9999,
          background: "rgba(0,0,0,0.5)", backdropFilter: "blur(4px)",
          display: "flex", alignItems: "center", justifyContent: "center", padding: 24,
        }}>
          <div style={{
            background: "#fff", borderRadius: 20, padding: "32px 28px",
            maxWidth: 440, width: "100%",
            boxShadow: "0 20px 60px rgba(0,0,0,0.2)",
          }}>
            <div style={{ fontSize: 36, textAlign: "center", marginBottom: 12 }}>❌</div>
            <h2 style={{ fontSize: 18, fontWeight: 800, color: "#dc2626", textAlign: "center", marginBottom: 6 }}>
              Từ chối yêu cầu rút tiền
            </h2>
            <p style={{ fontSize: 13, color: "#64748b", textAlign: "center", marginBottom: 20 }}>
              Tiền sẽ được <strong>hoàn về ví</strong> của Driver. Vui lòng nhập lý do.
            </p>
            <label style={{ fontSize: 13, fontWeight: 600, color: "#374151", display: "block", marginBottom: 6 }}>
              Lý do từ chối <span style={{ color: "#ef4444" }}>*</span>
            </label>
            <textarea
              value={rejectNote}
              onChange={e => setRejectNote(e.target.value)}
              placeholder="Ví dụ: Thông tin ngân hàng không chính xác..."
              rows={3}
              style={{
                width: "100%", padding: "10px 14px", borderRadius: 10,
                border: "1.5px solid #fca5a5", fontSize: 13, outline: "none",
                resize: "vertical", boxSizing: "border-box", marginBottom: 16,
              }}
            />
            <div style={{ display: "flex", gap: 10 }}>
              <button
                onClick={handleRejectSubmit}
                disabled={processing === rejectModal.id}
                style={{
                  flex: 1, padding: "12px 0", borderRadius: 12, border: "none",
                  background: processing ? "#d1d5db" : "linear-gradient(135deg, #ef4444, #dc2626)",
                  color: "#fff", fontWeight: 700, fontSize: 14,
                  cursor: processing ? "not-allowed" : "pointer",
                }}
              >
                {processing ? "Đang xử lý..." : "Xác nhận từ chối"}
              </button>
              <button
                onClick={() => { setRejectModal(null); setRejectNote(""); }}
                disabled={!!processing}
                style={{
                  flex: 1, padding: "12px 0", borderRadius: 12,
                  border: "1.5px solid #e5e7eb", background: "#f8fafc",
                  color: "#64748b", fontWeight: 600, fontSize: 14, cursor: "pointer",
                }}
              >
                Hủy bỏ
              </button>
            </div>
          </div>
        </div>
      )}

      {loading ? (
        <div style={{ textAlign: "center", padding: 60 }}>
          <div style={{ width: 40, height: 40, border: "4px solid #e5e7eb", borderTopColor: "#f97316", borderRadius: "50%", animation: "spin 1s linear infinite", margin: "0 auto 12px" }} />
          <p style={{ color: "#64748b", fontSize: 14 }}>Đang tải...</p>
          <style>{`@keyframes spin { to { transform: rotate(360deg); } }`}</style>
        </div>
      ) : withdraws.length === 0 ? (
        <div style={{ textAlign: "center", padding: 60, background: "#fff", borderRadius: 16 }}>
          <div style={{ fontSize: 48, marginBottom: 12 }}>✅</div>
          <p style={{ fontSize: 16, fontWeight: 700, color: "#374151" }}>Không có yêu cầu nào cần duyệt</p>
        </div>
      ) : (
        <div style={{ display: "flex", flexDirection: "column", gap: 12 }}>
          {withdraws.map(w => {
            const st = statusLabels[w.status] || statusLabels.Pending;
            return (
              <div key={w.id} style={{
                background: "#fff", borderRadius: 16, padding: 24,
                boxShadow: "0 2px 12px rgba(0,0,0,0.06)",
                borderLeft: `4px solid ${st.color}`,
              }}>
                <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 12 }}>
                  <div>
                    <div style={{ fontWeight: 800, fontSize: 20, color: "#1e293b" }}>
                      {(w.amount || 0).toLocaleString("vi-VN")}đ
                    </div>
                    <div style={{ fontSize: 13, color: "#64748b", marginTop: 2 }}>
                      Driver: {w.driverName || w.fullName || `ID #${w.driverId || w.userId}`}
                    </div>
                  </div>
                  <span style={{
                    fontSize: 12, fontWeight: 700, padding: "5px 14px", borderRadius: 20,
                    background: st.bg, color: st.color,
                  }}>{st.label}</span>
                </div>

                <div style={{ fontSize: 13, color: "#64748b", display: "flex", flexDirection: "column", gap: 4, marginBottom: 16 }}>
                  <div>🏦 {w.bankName} · {w.bankAccountNumber}</div>
                  <div>👤 {w.bankAccountHolder}</div>
                  {w.userNote && <div>📝 Ghi chú người dùng: {w.userNote}</div>}
                  {w.adminNote && <div style={{ color: "#ef4444" }}>⚠️ Lý do từ chối: {w.adminNote}</div>}
                  <div style={{ fontSize: 11, color: "#cbd5e1" }}>{toLocal(w.createdAt)}</div>
                </div>

                {w.status === "Pending" && (
                  <div style={{ display: "flex", gap: 10 }}>
                    <button
                      onClick={() => handleApprove(w.id)}
                      disabled={processing === w.id}
                      style={{
                        flex: 1, padding: "12px 0", borderRadius: 12, border: "none",
                        background: processing === w.id ? "#d1d5db" : "linear-gradient(135deg, #22c55e, #16a34a)",
                        color: "#fff", fontWeight: 700, fontSize: 14,
                        cursor: processing === w.id ? "not-allowed" : "pointer",
                      }}
                    >✅ Duyệt — Chuyển khoản</button>
                    <button
                      onClick={() => { setRejectModal({ id: w.id }); setRejectNote(""); }}
                      disabled={processing === w.id}
                      style={{
                        flex: 1, padding: "12px 0", borderRadius: 12, border: "none",
                        background: processing === w.id ? "#d1d5db" : "linear-gradient(135deg, #ef4444, #dc2626)",
                        color: "#fff", fontWeight: 700, fontSize: 14,
                        cursor: processing === w.id ? "not-allowed" : "pointer",
                      }}
                    >❌ Từ chối — Hoàn ví</button>
                  </div>
                )}
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
}
