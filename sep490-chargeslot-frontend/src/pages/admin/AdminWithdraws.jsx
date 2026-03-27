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
  const [noteInput, setNoteInput] = useState({});

  function fetchWithdraws() {
    setLoading(true);
    adminWithdrawApi.getPending()
      .then(data => setWithdraws(Array.isArray(data) ? data : []))
      .catch(() => setWithdraws([]))
      .finally(() => setLoading(false));
  }

  useEffect(() => { fetchWithdraws(); }, []);

  async function handleProcess(id, approve) {
    setProcessing(id);
    try {
      await adminWithdrawApi.process(id, approve, noteInput[id] || "");
      showToast.success(approve ? "Đã duyệt yêu cầu rút tiền" : "Đã từ chối yêu cầu");
      fetchWithdraws();
    } catch (err) {
      showToast.error(err.message || "Lỗi xử lý yêu cầu");
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
                  {w.userNote && <div>📝 {w.userNote}</div>}
                  <div style={{ fontSize: 11, color: "#cbd5e1" }}>{toLocal(w.createdAt)}</div>
                </div>

                {w.status === "Pending" && (
                  <>
                    <input
                      type="text"
                      placeholder="Ghi chú admin (tùy chọn)..."
                      value={noteInput[w.id] || ""}
                      onChange={e => setNoteInput(prev => ({ ...prev, [w.id]: e.target.value }))}
                      style={{
                        width: "100%", padding: "10px 14px", borderRadius: 10,
                        border: "1.5px solid #e5e7eb", fontSize: 13, outline: "none",
                        boxSizing: "border-box", marginBottom: 12,
                      }}
                    />
                    <div style={{ display: "flex", gap: 10 }}>
                      <button
                        onClick={() => handleProcess(w.id, true)}
                        disabled={processing === w.id}
                        style={{
                          flex: 1, padding: "12px 0", borderRadius: 12, border: "none",
                          background: processing === w.id ? "#d1d5db" : "linear-gradient(135deg, #22c55e, #16a34a)",
                          color: "#fff", fontWeight: 700, fontSize: 14,
                          cursor: processing === w.id ? "not-allowed" : "pointer",
                        }}
                      >✅ Duyệt</button>
                      <button
                        onClick={() => handleProcess(w.id, false)}
                        disabled={processing === w.id}
                        style={{
                          flex: 1, padding: "12px 0", borderRadius: 12, border: "none",
                          background: processing === w.id ? "#d1d5db" : "linear-gradient(135deg, #ef4444, #dc2626)",
                          color: "#fff", fontWeight: 700, fontSize: 14,
                          cursor: processing === w.id ? "not-allowed" : "pointer",
                        }}
                      >❌ Từ chối</button>
                    </div>
                  </>
                )}
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
}
