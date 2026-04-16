import { useState, useEffect } from "react";
import { adminWithdrawApi } from "@/services/api";
import { showToast } from "@/components/Toast";
import Pagination from "@/components/Pagination";

const statusLabels = {
  Pending: { label: "Chờ duyệt", color: "#f59e0b", bg: "#fffbeb" },
  Approved: { label: "Đã duyệt (Chờ CK)", color: "#3b82f6", bg: "#eff6ff" },
  TransferCompleted: { label: "Đã chuyển khoản", color: "#22c55e", bg: "#f0fdf4" },
  Completed: { label: "Thành công", color: "#16a34a", bg: "#dcfce7" },
  Rejected: { label: "Từ chối", color: "#ef4444", bg: "#fef2f2" },
  IssueReported: { label: "Báo lỗi", color: "#dc2626", bg: "#fef2f2" },
};

const toLocal = (dt) => {
  if (!dt) return "";
  return new Date(String(dt).replace("Z", "")).toLocaleString("vi-VN");
};

export default function AdminWithdraws() {
  const [activeTab, setActiveTab] = useState("pending");
  const [withdraws, setWithdraws] = useState([]);
  const [loading, setLoading] = useState(true);
  const [processing, setProcessing] = useState(null);
  const [page, setPage] = useState(1);
  const PAGE_SIZE = 10;

  const [processModal, setProcessModal] = useState(null);
  const [processNote, setProcessNote] = useState("");
  const [secPass, setSecPass] = useState("");

  const [receiptModal, setReceiptModal] = useState(null);
  const [receiptFile, setReceiptFile] = useState(null);

  const [resolveModal, setResolveModal] = useState(null);
  const [resolveRefund, setResolveRefund] = useState(true);
  const [resolveNote, setResolveNote] = useState("");

  function fetchData() {
    setLoading(true);
    const apiCall = activeTab === "pending"
      ? adminWithdrawApi.getPending()
      : adminWithdrawApi.getAll();

    apiCall
      .then(data => {
        let list = Array.isArray(data) ? data : (data?.items || []);
        if (activeTab === "approved") list = list.filter(w => w.status === "Approved");
        else if (activeTab === "issue") list = list.filter(w => w.status === "IssueReported");
        setWithdraws(list);
      })
      .catch(() => setWithdraws([]))
      .finally(() => setLoading(false));
  }

  useEffect(() => { setPage(1); fetchData(); }, [activeTab]);

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

  function handleResolveIssue(id) {
    setResolveModal({ id });
    setResolveRefund(true);
    setResolveNote("");
  }

  async function submitResolveIssue(e) {
    e.preventDefault();
    if (!resolveModal) return;
    setProcessing(resolveModal.id);
    try {
      await adminWithdrawApi.resolveIssue(resolveModal.id, resolveRefund, resolveNote);
      showToast.success(resolveRefund ? "Đã hoàn tiền vào ví!" : "Đã yêu cầu chuyển khoản lại!");
      setResolveModal(null);
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
          <h1 className="cs-admin-page__title" style={{ fontSize: 26, fontWeight: 800 }}>Quản lý Rút tiền</h1>
          <p className="cs-admin-page__subtitle" style={{ color: "#64748b", marginTop: 4 }}>
            Xử lý yêu cầu rút tiền và giải quyết sự cố
          </p>
        </div>
      </div>

      <div style={{ display: "flex", gap: 8, marginBottom: 24, padding: 4, background: "#e2e8f0", borderRadius: 12, width: "fit-content", flexWrap: "wrap" }}>
        {[
          { id: "pending", label: "Yêu cầu Rút tiền", color: "#0f172a" },
          { id: "approved", label: "Chờ chuyển khoản", color: "#3b82f6" },
          { id: "issue", label: "Sự cố Giao dịch", color: "#dc2626" },
          { id: "all", label: "Lịch sử chung", color: "#16a34a" },
        ].map(tab => (
          <button
            key={tab.id}
            onClick={() => setActiveTab(tab.id)}
            style={{
              padding: "8px 20px", borderRadius: 8, fontSize: 14, fontWeight: 600, border: "none", cursor: "pointer", transition: "0.2s",
              background: activeTab === tab.id ? "#fff" : "transparent",
              color: activeTab === tab.id ? tab.color : "#64748b",
              boxShadow: activeTab === tab.id ? "0 2px 4px rgba(0,0,0,0.05)" : "none"
            }}
          >
            {tab.label}
          </button>
        ))}
      </div>

      {loading ? (
        <div style={{ textAlign: "center", padding: 60 }}>
          <div style={{ width: 40, height: 40, border: "4px solid #e5e7eb", borderTopColor: "#f97316", borderRadius: "50%", animation: "spin 1s linear infinite", margin: "0 auto 12px" }} />
          <p style={{ color: "#64748b", fontSize: 14 }}>Đang tải...</p>
        </div>
      ) : withdraws.length === 0 ? (
        <div style={{ textAlign: "center", padding: 60, background: "#fff", borderRadius: 16 }}>
          <div style={{ fontSize: 48, marginBottom: 12 }}></div>
          <p style={{ fontSize: 16, fontWeight: 700, color: "#374151" }}>Không có dữ liệu trong mục này</p>
        </div>
      ) : (
        <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
          {withdraws.slice((page - 1) * PAGE_SIZE, page * PAGE_SIZE).map(w => {
            const st = statusLabels[w.status] || statusLabels.Pending;
            return (
              <div key={w.id} style={{
                background: "#fff", borderRadius: 16, padding: 24,
                boxShadow: "0 2px 12px rgba(0,0,0,0.06)", borderLeft: `4px solid ${st.color}`
              }}>
                <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 16, flexWrap: "wrap", gap: 10 }}>
                  <div>
                    <div style={{ fontWeight: 800, fontSize: 22, color: "#1e293b" }}>{(w.amount || 0).toLocaleString("vi-VN")}đ</div>
                    <div style={{ fontSize: 13, color: "#64748b", marginTop: 4 }}>Người dùng: {w.driverName || w.ownerName || w.fullName || `ID #${w.userId}`}</div>
                  </div>
                  <span style={{ fontSize: 13, fontWeight: 700, padding: "6px 16px", borderRadius: 20, background: st.bg, color: st.color }}>{st.label}</span>
                </div>

                <div style={{ fontSize: 14, color: "#475569", display: "flex", flexDirection: "column", gap: 6, marginBottom: 20 }}>
                  <div style={{ display: "flex", alignItems: "center", gap: 8 }}><span></span> <strong>{w.bankName}</strong> {w.bankAccountNumber}</div>
                  <div style={{ display: "flex", alignItems: "center", gap: 8 }}><span></span> {w.bankAccountHolder}</div>
                  {w.userNote && <div>Ghi chú: {w.userNote}</div>}
                  {w.issueNote && <div style={{ color: "#dc2626", padding: "8px 12px", background: "#fef2f2", borderRadius: 8, marginTop: 4 }}>Khách Báo Lỗi: {w.issueNote}</div>}
                  <div style={{ fontSize: 12, color: "#94a3b8", marginTop: 4 }}>Bắt đầu tạo: {toLocal(w.requestedAt)}</div>
                </div>

                {w.transferReceiptUrl && (
                  <div style={{ marginTop: 8, marginBottom: 20, padding: 12, background: "#f8fafc", borderRadius: 12, border: "1px dashed #cbd5e1" }}>
                    <p style={{ fontSize: 13, fontWeight: 700, color: "#3b82f6", marginBottom: 8 }}>Biên lai đã tải lên:</p>
                    <a href={w.transferReceiptUrl} target="_blank" rel="noreferrer">
                      <img src={w.transferReceiptUrl} alt="Biên lai" style={{ width: "100%", maxWidth: 300, objectFit: "contain", borderRadius: 8, border: "1px solid #e2e8f0", background: "#fff" }} />
                    </a>
                  </div>
                )}

                <div style={{ display: "flex", gap: 10 }}>
                  {w.status === "Pending" && (
                    <>
                      <button onClick={() => setProcessModal({ id: w.id, isApprove: true })} style={{ flex: 1, padding: "12px 0", borderRadius: 10, border: "none", background: "#22c55e", color: "#fff", fontWeight: 600, cursor: "pointer" }}>Duyệt</button>
                      <button onClick={() => setProcessModal({ id: w.id, isApprove: false })} style={{ flex: 1, padding: "12px 0", borderRadius: 10, border: "none", background: "#ef4444", color: "#fff", fontWeight: 600, cursor: "pointer" }}>Từ chối</button>
                    </>
                  )}
                  {w.status === "Approved" && (
                    <button onClick={() => setReceiptModal({ id: w.id })} style={{ width: "100%", padding: "12px 0", borderRadius: 10, border: "none", background: "#3b82f6", color: "#fff", fontWeight: 600, cursor: "pointer" }}> Úp ảnh Biên lai & Xác nhận chuyển khoản</button>
                  )}
                  {w.status === "IssueReported" && (
                    <button onClick={() => handleResolveIssue(w.id)} disabled={processing === w.id} style={{ width: "100%", padding: "12px 0", borderRadius: 10, border: "none", background: processing === w.id ? "#d1d5db" : "#f97316", color: "#fff", fontWeight: 600, cursor: processing === w.id ? "not-allowed" : "pointer" }}>{processing === w.id ? "..." : "️ Đã giải quyết / Đóng sự cố"}</button>
                  )}
                </div>
              </div>
            );
          })}
          <Pagination
            page={page}
            totalCount={withdraws.length}
            pageSize={PAGE_SIZE}
            onPageChange={(p) => setPage(p)}
          />
        </div>
      )}

      {processModal && (
        <div style={{ position: "fixed", inset: 0, zIndex: 50, background: "rgba(0,0,0,0.5)", display: "flex", alignItems: "center", justifyContent: "center", padding: 24, backdropFilter: "blur(4px)" }}>
          <div style={{ background: "#fff", borderRadius: 20, padding: 32, maxWidth: 400, width: "100%", boxShadow: "0 20px 40px rgba(0,0,0,0.2)" }}>
            <h2 style={{ fontSize: 20, fontWeight: 800, color: processModal.isApprove ? "#16a34a" : "#dc2626", marginBottom: 16 }}>
              {processModal.isApprove ? "Xác nhận Duyệt" : "Từ chối Rút tiền"}
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

      {receiptModal && (
        <div style={{ position: "fixed", inset: 0, zIndex: 50, background: "rgba(0,0,0,0.5)", display: "flex", alignItems: "center", justifyContent: "center", padding: 24, backdropFilter: "blur(4px)" }}>
          <div style={{ background: "#fff", borderRadius: 20, padding: 32, maxWidth: 400, width: "100%", boxShadow: "0 20px 40px rgba(0,0,0,0.2)" }}>
            <h2 style={{ fontSize: 20, fontWeight: 800, color: "#3b82f6", marginBottom: 16 }}> Xác nhận đã chuyển khoản</h2>
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
      {resolveModal && (
        <div style={{ position: "fixed", inset: 0, zIndex: 50, background: "rgba(0,0,0,0.5)", display: "flex", alignItems: "center", justifyContent: "center", padding: 24, backdropFilter: "blur(4px)" }}>
          <div style={{ background: "#fff", borderRadius: 20, padding: 32, maxWidth: 440, width: "100%", boxShadow: "0 20px 40px rgba(0,0,0,0.2)" }}>
            <h2 style={{ fontSize: 20, fontWeight: 800, color: "#f97316", marginBottom: 8 }}>️ Giải quyết Sự cố</h2>
            <p style={{ fontSize: 13, color: "#64748b", marginBottom: 20 }}>Chọn cách xử lý và nhập ghi chú trước khi xác nhận.</p>
            <form onSubmit={submitResolveIssue} style={{ display: "flex", flexDirection: "column", gap: 16 }}>
              <div style={{ display: "flex", flexDirection: "column", gap: 10 }}>
                <label style={{ fontSize: 13, fontWeight: 600, display: "block", marginBottom: 4 }}>Cách xử lý</label>
                <label style={{ display: "flex", alignItems: "center", gap: 10, padding: "12px 16px", borderRadius: 10, border: `2px solid ${resolveRefund ? "#22c55e" : "#e5e7eb"}`, cursor: "pointer", transition: "0.2s" }}>
                  <input type="radio" name="refund" checked={resolveRefund === true} onChange={() => setResolveRefund(true)} />
                  <div>
                    <div style={{ fontWeight: 700, color: "#16a34a" }}>Hoàn tiền vào ví</div>
                    <div style={{ fontSize: 12, color: "#64748b" }}>Trả lại tiền vào ví Owner ngay lập tức</div>
                  </div>
                </label>
                <label style={{ display: "flex", alignItems: "center", gap: 10, padding: "12px 16px", borderRadius: 10, border: `2px solid ${resolveRefund === false ? "#3b82f6" : "#e5e7eb"}`, cursor: "pointer", transition: "0.2s" }}>
                  <input type="radio" name="refund" checked={resolveRefund === false} onChange={() => setResolveRefund(false)} />
                  <div>
                    <div style={{ fontWeight: 700, color: "#3b82f6" }}>Chuyển khoản lại</div>
                    <div style={{ fontSize: 12, color: "#64748b" }}>Thực hiện lại lệnh chuyển khoản, chờ user xác nhận</div>
                  </div>
                </label>
              </div>
              <div>
                <label style={{ fontSize: 13, fontWeight: 600, marginBottom: 6, display: "block" }}>Ghi chú admin (tuỳ chọn)</label>
                <input value={resolveNote} onChange={e => setResolveNote(e.target.value)} type="text" placeholder="Lý do xử lý..." style={{ width: "100%", padding: 12, borderRadius: 10, border: "1.5px solid #e2e8f0", outline: "none", boxSizing: "border-box" }} />
              </div>
              <div style={{ display: "flex", gap: 10, marginTop: 8 }}>
                <button type="submit" disabled={!!processing} style={{ flex: 1, padding: "12px 0", borderRadius: 10, border: "none", background: resolveRefund ? "#22c55e" : "#3b82f6", color: "#fff", fontWeight: 700, cursor: "pointer" }}>
                  {processing ? "..." : "Xác nhận"}
                </button>
                <button type="button" onClick={() => setResolveModal(null)} style={{ flex: 1, padding: "12px 0", borderRadius: 10, border: "none", background: "#f1f5f9", color: "#64748b", fontWeight: 600, cursor: "pointer" }}>Hủy</button>
              </div>
            </form>
          </div>
        </div>
      )}
      <style>{`@keyframes spin { to { transform: rotate(360deg); } }`}</style>
    </div>
  );
}
