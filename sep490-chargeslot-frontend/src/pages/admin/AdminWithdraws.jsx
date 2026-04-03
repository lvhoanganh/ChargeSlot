import { useState, useEffect } from "react";
import { adminWithdrawApi } from "@/services/api";
import { showToast } from "@/components/Toast";

const statusLabels = {
  Pending: { label: "Chờ duyệt", color: "#f59e0b", bg: "#fffbeb" },
  Approved: { label: "Đã duyệt (Chờ CK)", color: "#3b82f6", bg: "#eff6ff" },
  TransferCompleted: { label: "Đã chuyển khoản", color: "#22c55e", bg: "#f0fdf4" },
  Rejected: { label: "Từ chối", color: "#ef4444", bg: "#fef2f2" },
  IssueReported: { label: "Báo lỗi", color: "#dc2626", bg: "#fef2f2" },
};

const toLocal = (dt) => {
  if (!dt) return "";
  return new Date(String(dt).replace("Z", "")).toLocaleString("vi-VN");
};

export default function AdminWithdraws() {
  const [activeTab, setActiveTab] = useState("pending"); // pending | issue
  const [withdraws, setWithdraws] = useState([]);
  const [loading, setLoading] = useState(true);
  const [processing, setProcessing] = useState(null);

  // Modals
  const [processModal, setProcessModal] = useState(null); // { id, isApprove }
  const [processNote, setProcessNote] = useState("");
  const [secPass, setSecPass] = useState("");

  const [receiptModal, setReceiptModal] = useState(null); // { id }
  const [receiptFile, setReceiptFile] = useState(null);

  function fetchData() {
    setLoading(true);
    const apiCall = activeTab === "pending" ? adminWithdrawApi.getPending() : adminWithdrawApi.getIssueReported();
    apiCall
      .then(data => setWithdraws(Array.isArray(data) ? data : []))
      .catch(() => setWithdraws([]))
      .finally(() => setLoading(false));
  }

  useEffect(() => { fetchData(); }, [activeTab]);

  async function submitProcess(e) {
    e.preventDefault();
    if (!secPass) return showToast.error("Vui lòng nhập mật khẩu cấp 2");
    if (!processModal.isApprove && !processNote.trim()) return showToast.error("Vui lòng nhập lý do từ chối");

    setProcessing(processModal.id);
    try {
      await adminWithdrawApi.process(processModal.id, processModal.isApprove, processNote, secPass);
      showToast.success(processModal.isApprove ? "Đã duyệt yêu cầu!" : "Đã từ chối yêu cầu!");
      setProcessModal(null);
      setProcessNote("");
      setSecPass("");
      fetchData();
    } catch (err) {
      showToast.error(err.message || "Lỗi xử lý yêu cầu");
    } finally {
      setProcessing(null);
    }
  }

  async function submitReceipt(e) {
    e.preventDefault();
    if (!receiptFile) return showToast.error("Vui lòng chọn ảnh biên lai");
    setProcessing(receiptModal.id);
    try {
      await adminWithdrawApi.confirmTransfer(receiptModal.id, receiptFile);
      showToast.success("Đã tải lên biên lai và xác nhận chuyển khoản!");
      setReceiptModal(null);
      setReceiptFile(null);
      fetchData();
    } catch (err) {
      showToast.error(err.message || "Lỗi tải ảnh");
    } finally {
      setProcessing(null);
    }
  }

  async function handleResolveIssue(id) {
    if (!window.confirm("Xác nhận đã giải quyết xong sự cố này?")) return;
    setProcessing(id);
    try {
      await adminWithdrawApi.resolveIssue(id);
      showToast.success("Sự cố đã được giải quyết!");
      fetchData();
    } catch (err) {
      showToast.error(err.message || "Lỗi giải quyết sự cố");
    } finally {
      setProcessing(null);
    }
  }

  return (
    <div className="cs-admin-page" style={{ paddingTop: 88, paddingBottom: 40, maxWidth: 1000, margin: "0 auto", paddingLeft: 16, paddingRight: 16 }}>
      <div className="cs-admin-page__header" style={{ marginBottom: 20 }}>
        <div>
          <h1 className="cs-admin-page__title" style={{ fontSize: 26, fontWeight: 800 }}>🏦 Quản lý Rút tiền</h1>
          <p className="cs-admin-page__subtitle" style={{ color: "#64748b", marginTop: 4 }}>
            Xử lý yêu cầu rút tiền và giải quyết sự cố
          </p>
        </div>
      </div>

      <div style={{ display: "flex", gap: 8, marginBottom: 24, padding: 4, background: "#e2e8f0", borderRadius: 12, width: "fit-content" }}>
        <button
          onClick={() => setActiveTab("pending")}
          style={{ padding: "8px 20px", borderRadius: 8, fontSize: 14, fontWeight: 600, border: "none", cursor: "pointer", transition: "0.2s", background: activeTab === "pending" ? "#fff" : "transparent", color: activeTab === "pending" ? "#0f172a" : "#64748b", boxShadow: activeTab === "pending" ? "0 2px 4px rgba(0,0,0,0.05)" : "none" }}
        >
          Yêu cầu Rút tiền
        </button>
        <button
          onClick={() => setActiveTab("issue")}
          style={{ padding: "8px 20px", borderRadius: 8, fontSize: 14, fontWeight: 600, border: "none", cursor: "pointer", transition: "0.2s", background: activeTab === "issue" ? "#fff" : "transparent", color: activeTab === "issue" ? "#dc2626" : "#64748b", boxShadow: activeTab === "issue" ? "0 2px 4px rgba(0,0,0,0.05)" : "none" }}
        >
          Sự cố Giao dịch
        </button>
      </div>

      {loading ? (
        <div style={{ textAlign: "center", padding: 60 }}>
          <div style={{ width: 40, height: 40, border: "4px solid #e5e7eb", borderTopColor: "#f97316", borderRadius: "50%", animation: "spin 1s linear infinite", margin: "0 auto 12px" }} />
          <p style={{ color: "#64748b", fontSize: 14 }}>Đang tải...</p>
        </div>
      ) : withdraws.length === 0 ? (
        <div style={{ textAlign: "center", padding: 60, background: "#fff", borderRadius: 16 }}>
          <div style={{ fontSize: 48, marginBottom: 12 }}>✅</div>
          <p style={{ fontSize: 16, fontWeight: 700, color: "#374151" }}>Không có dữ liệu trong mục này</p>
        </div>
      ) : (
        <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
          {withdraws.map(w => {
            const st = statusLabels[w.status] || statusLabels.Pending;
            return (
              <div key={w.id} style={{
                background: "#fff", borderRadius: 16, padding: 24,
                boxShadow: "0 2px 12px rgba(0,0,0,0.06)", borderLeft: `4px solid ${st.color}`
              }}>
                <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 16, flexWrap: "wrap", gap: 10 }}>
                  <div>
                    <div style={{ fontWeight: 800, fontSize: 22, color: "#1e293b" }}>{(w.amount || 0).toLocaleString("vi-VN")}đ</div>
                    <div style={{ fontSize: 13, color: "#64748b", marginTop: 4 }}>User: {w.driverName || w.ownerName || w.fullName || `ID #${w.userId}`}</div>
                  </div>
                  <span style={{ fontSize: 13, fontWeight: 700, padding: "6px 16px", borderRadius: 20, background: st.bg, color: st.color }}>{st.label}</span>
                </div>

                <div style={{ fontSize: 14, color: "#475569", display: "flex", flexDirection: "column", gap: 6, marginBottom: 20 }}>
                  <div style={{ display: "flex", alignItems: "center", gap: 8 }}><span>🏦</span> <strong>{w.bankName}</strong> {w.bankAccountNumber}</div>
                  <div style={{ display: "flex", alignItems: "center", gap: 8 }}><span>👤</span> {w.bankAccountHolder}</div>
                  {w.userNote && <div>📝 Ghi chú: {w.userNote}</div>}
                  {w.issueNote && <div style={{ color: "#dc2626", padding: "8px 12px", background: "#fef2f2", borderRadius: 8, marginTop: 4 }}>🚨 Khách Báo Lỗi: {w.issueNote}</div>}
                  <div style={{ fontSize: 12, color: "#94a3b8", marginTop: 4 }}>🕒 {toLocal(w.createdAt)}</div>
                </div>

                <div style={{ display: "flex", gap: 10 }}>
                  {w.status === "Pending" && (
                    <>
                      <button onClick={() => setProcessModal({ id: w.id, isApprove: true })} style={{ flex: 1, padding: "12px 0", borderRadius: 10, border: "none", background: "#22c55e", color: "#fff", fontWeight: 600, cursor: "pointer" }}>Duyệt</button>
                      <button onClick={() => setProcessModal({ id: w.id, isApprove: false })} style={{ flex: 1, padding: "12px 0", borderRadius: 10, border: "none", background: "#ef4444", color: "#fff", fontWeight: 600, cursor: "pointer" }}>Từ chối</button>
                    </>
                  )}
                  {w.status === "Approved" && (
                    <button onClick={() => setReceiptModal({ id: w.id })} style={{ width: "100%", padding: "12px 0", borderRadius: 10, border: "none", background: "#3b82f6", color: "#fff", fontWeight: 600, cursor: "pointer" }}>📤 Úp ảnh Biên lai & Xác nhận Ck</button>
                  )}
                  {w.status === "IssueReported" && (
                    <button onClick={() => handleResolveIssue(w.id)} disabled={processing === w.id} style={{ width: "100%", padding: "12px 0", borderRadius: 10, border: "none", background: processing === w.id ? "#d1d5db" : "#f97316", color: "#fff", fontWeight: 600, cursor: processing === w.id ? "not-allowed" : "pointer" }}>{processing === w.id ? "..." : "🛠️ Đã giải quyết / Đóng sự cố"}</button>
                  )}
                </div>
              </div>
            );
          })}
        </div>
      )}

      {/* Modal Duyệt / Từ chối (Cần có MK cấp 2) */}
      {processModal && (
        <div style={{ position: "fixed", inset: 0, zIndex: 50, background: "rgba(0,0,0,0.5)", display: "flex", alignItems: "center", justifyContent: "center", padding: 24, backdropFilter: "blur(4px)" }}>
          <div style={{ background: "#fff", borderRadius: 20, padding: 32, maxWidth: 400, width: "100%", boxShadow: "0 20px 40px rgba(0,0,0,0.2)" }}>
            <h2 style={{ fontSize: 20, fontWeight: 800, color: processModal.isApprove ? "#16a34a" : "#dc2626", marginBottom: 16 }}>
              {processModal.isApprove ? "✅ Xác nhận Duyệt" : "❌ Từ chối Rút tiền"}
            </h2>
            <form onSubmit={submitProcess} style={{ display: "flex", flexDirection: "column", gap: 16 }}>
              {!processModal.isApprove && (
                <div>
                  <label style={{ fontSize: 13, fontWeight: 600, marginBottom: 6, display: "block" }}>Lý do từ chối</label>
                  <input autoFocus value={processNote} onChange={e => setProcessNote(e.target.value)} type="text" placeholder="Nhập lý do hoàn tiền..." required style={{ width: "100%", padding: 12, borderRadius: 10, border: "1.5px solid #e2e8f0", outline: "none" }} />
                </div>
              )}
              <div>
                <label style={{ fontSize: 13, fontWeight: 600, marginBottom: 6, display: "block" }}>Mật khẩu cấp 2</label>
                <input value={secPass} onChange={e => setSecPass(e.target.value)} autoFocus={processModal.isApprove} type="password" required placeholder="Nhập mật khẩu cấp 2..." style={{ width: "100%", padding: 12, borderRadius: 10, border: "1.5px solid #e2e8f0", outline: "none" }} />
              </div>
              <div style={{ display: "flex", gap: 10, marginTop: 8 }}>
                <button type="submit" disabled={processing} style={{ flex: 1, padding: "12px 0", borderRadius: 10, border: "none", background: processModal.isApprove ? "#22c55e" : "#ef4444", color: "#fff", fontWeight: 700, cursor: "pointer" }}>{processing ? "..." : "Xác nhận"}</button>
                <button type="button" onClick={() => { setProcessModal(null); setSecPass(""); setProcessNote(""); }} style={{ flex: 1, padding: "12px 0", borderRadius: 10, border: "none", background: "#f1f5f9", color: "#64748b", fontWeight: 600, cursor: "pointer" }}>Hủy</button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Modal Upload Biên Lai */}
      {receiptModal && (
        <div style={{ position: "fixed", inset: 0, zIndex: 50, background: "rgba(0,0,0,0.5)", display: "flex", alignItems: "center", justifyContent: "center", padding: 24, backdropFilter: "blur(4px)" }}>
          <div style={{ background: "#fff", borderRadius: 20, padding: 32, maxWidth: 400, width: "100%", boxShadow: "0 20px 40px rgba(0,0,0,0.2)" }}>
            <h2 style={{ fontSize: 20, fontWeight: 800, color: "#3b82f6", marginBottom: 16 }}>📤 Xác nhận Đã CK</h2>
            <p style={{ fontSize: 13, color: "#64748b", marginBottom: 20 }}>Tải ảnh biên lai ngân hàng lên hệ thống để gửi đến người dùng.</p>
            <form onSubmit={submitReceipt} style={{ display: "flex", flexDirection: "column", gap: 16 }}>
              <div>
                <input type="file" accept="image/*" onChange={e => setReceiptFile(e.target.files[0])} required style={{ width: "100%", padding: 10, borderRadius: 10, border: "1.5px dashed #cbd5e1" }} />
              </div>
              <div style={{ display: "flex", gap: 10, marginTop: 8 }}>
                <button type="submit" disabled={processing} style={{ flex: 1, padding: "12px 0", borderRadius: 10, border: "none", background: "#3b82f6", color: "#fff", fontWeight: 700, cursor: "pointer" }}>{processing ? "..." : "Tải lên"}</button>
                <button type="button" onClick={() => { setReceiptModal(null); setReceiptFile(null); }} style={{ flex: 1, padding: "12px 0", borderRadius: 10, border: "none", background: "#f1f5f9", color: "#64748b", fontWeight: 600, cursor: "pointer" }}>Hủy</button>
              </div>
            </form>
          </div>
        </div>
      )}
      <style>{`@keyframes spin { to { transform: rotate(360deg); } }`}</style>
    </div>
  );
}
