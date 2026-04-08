import { useState, useEffect } from "react";
import { useNavigate } from "react-router-dom";
import { walletApi, bankAccountApi } from "@/services/api";
import { showToast } from "@/components/Toast";
import { showConfirm } from "@/components/ConfirmDialog";
import BankCombobox from "@/components/BankCombobox";
import Pagination from "@/components/Pagination";

const txTypeLabels = {
  TopUp: { label: "Nạp tiền", icon: "💰", color: "#22c55e" },
  BookingPayment: { label: "Thanh toán booking", icon: "💳", color: "#3b82f6" },
  BookingCancel: { label: "Hoàn tiền hủy booking", icon: "↩️", color: "#f59e0b" },
  Payment: { label: "Thanh toán", icon: "💳", color: "#3b82f6" },
  Refund: { label: "Hoàn tiền", icon: "↩️", color: "#f59e0b" },
  Withdraw: { label: "Rút tiền", icon: "🏦", color: "#ef4444" },
  WithdrawRequest: { label: "Tạm giữ lệnh Rút tiền", icon: "⏳", color: "#f59e0b" },
  WithdrawRejected: { label: "Hoàn tiền huỷ lệnh rút", icon: "↩️", color: "#3b82f6" },
  Earning: { label: "Thu nhập", icon: "📈", color: "#22c55e" },
  OwnerPayout: { label: "Nhận thanh toán", icon: "💰", color: "#22c55e" },
  ChargingPayout: { label: "Thanh toán phiên sạc", icon: "⚡", color: "#22c55e" },
};

const withdrawStatusLabels = {
  Pending: { label: "Chờ xử lý", color: "#f59e0b", bg: "#fffbeb" },
  Approved: { label: "Đã duyệt", color: "#3b82f6", bg: "#eff6ff" },
  TransferCompleted: { label: "Đã chuyển khoản", color: "#22c55e", bg: "#f0fdf4" },
  Completed: { label: "Thành công", color: "#16a34a", bg: "#dcfce7" },
  IssueReported: { label: "Báo lỗi", color: "#ef4444", bg: "#fef2f2" },
  Rejected: { label: "Từ chối", color: "#ef4444", bg: "#fef2f2" },
};

const toLocal = (dt) => {
  if (!dt) return "";
  const s = String(dt);
  return new Date(String(s).replace("Z", "")).toLocaleString("vi-VN");
};

export default function OwnerWallet() {
  const navigate = useNavigate();
  const [wallet, setWallet] = useState(null);
  const [transactions, setTransactions] = useState([]);
  const [withdraws, setWithdraws] = useState([]);
  const [bankAccounts, setBankAccounts] = useState([]);
  const [loading, setLoading] = useState(true);
  const [activeTab, setActiveTab] = useState("transactions");
  
  const [txPage, setTxPage] = useState(1);
  const [txTotal, setTxTotal] = useState(0);
  const [wrPage, setWrPage] = useState(1);
  const [wrTotal, setWrTotal] = useState(0);

  const [selectedTx, setSelectedTx] = useState(null);

  // Withdraw form
  const [showWithdraw, setShowWithdraw] = useState(false);
  const [withdrawForm, setWithdrawForm] = useState({ amount: "", bankAccountId: "", note: "" });
  const [withdrawLoading, setWithdrawLoading] = useState(false);

  // Add bank account form
  const [showAddBank, setShowAddBank] = useState(false);
  const [bankForm, setBankForm] = useState({ bankName: "", bankAccountNumber: "", bankAccountHolder: "", isDefault: false });
  const [bankLoading, setBankLoading] = useState(false);

  function fetchWallet() {
    walletApi.getWallet().then(w => setWallet(w)).catch(() => null);
  }

  function fetchBankAccounts() {
    bankAccountApi.getAll().then(bas => setBankAccounts(Array.isArray(bas) ? bas : [])).catch(() => []);
  }

  function fetchTransactions() {
    walletApi.getTransactions(txPage, 20)
      .then(data => {
        const list = Array.isArray(data) ? data : (data?.items ?? []);
        setTransactions(list);
        setTxTotal(data?.totalCount ?? data?.total ?? list.length);
      }).catch(() => setTransactions([]));
  }

  function fetchWithdraws() {
    walletApi.getWithdrawRequests(wrPage, 20)
      .then(data => {
        const list = Array.isArray(data) ? data : (data?.items ?? []);
        setWithdraws(list);
        setWrTotal(data?.totalCount ?? data?.total ?? list.length);
      }).catch(() => setWithdraws([]));
  }

  useEffect(() => {
    fetchWallet();
    fetchBankAccounts();
    setLoading(false);
  }, []);

  useEffect(() => {
    fetchTransactions();
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [txPage]);

  useEffect(() => {
    fetchWithdraws();
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [wrPage]);

  // Backward compatibility alias
  function fetchAll() {
    fetchWallet();
    fetchTransactions();
    fetchWithdraws();
    fetchBankAccounts();
  }

  const [issueForm, setIssueForm] = useState({ id: null, reason: "" });
  const [issueLoading, setIssueLoading] = useState(false);

  async function handleConfirmWithdraw(id) {
    if (!(await showConfirm("Bạn xác nhận đã nhận được tiền rút trong tài khoản?", "Xác nhận nhận tiền"))) return;
    try {
      await walletApi.confirmWithdrawal(id);
      showToast.success("Cảm ơn bạn đã xác nhận!");
      fetchAll();
    } catch (err) {
      showToast.error(err.message || "Lỗi xác nhận");
    }
  }

  async function handleReportIssue() {
    if (!issueForm.reason.trim()) {
      showToast.error("Vui lòng nhập lý do");
      return;
    }
    setIssueLoading(true);
    try {
      await walletApi.reportWithdrawalIssue(issueForm.id, issueForm.reason);
      showToast.success("Đã gửi báo cáo lỗi thành công");
      setIssueForm({ id: null, reason: "" });
      fetchAll();
    } catch (err) {
      showToast.error(err.message || "Lỗi báo cáo");
    } finally {
      setIssueLoading(false);
    }
  }

  async function handleWithdraw() {
    const amt = Number(withdrawForm.amount);
    if (!amt || amt < 10000) {
      showToast.error("Số tiền rút tối thiểu là 10.000đ");
      return;
    }
    if (!withdrawForm.bankAccountId) {
      showToast.error("Vui lòng chọn tài khoản ngân hàng");
      return;
    }
    setWithdrawLoading(true);
    try {
      const bank = bankAccounts.find(b => b.id === Number(withdrawForm.bankAccountId));
      await walletApi.withdraw({
        amount: amt,
        bankName: bank.bankName,
        bankAccountNumber: bank.bankAccountNumber,
        bankAccountHolder: bank.bankAccountHolder,
        userNote: withdrawForm.note,
      });
      showToast.success("Yêu cầu rút tiền đã gửi! Chờ admin duyệt.");
      setShowWithdraw(false);
      setWithdrawForm({ amount: "", bankAccountId: "", note: "" });
      fetchAll();
    } catch (err) {
      showToast.error(err.message || "Lỗi tạo yêu cầu rút tiền");
    } finally {
      setWithdrawLoading(false);
    }
  }

  async function handleAddBank() {
    if (!bankForm.bankName || !bankForm.bankAccountNumber || !bankForm.bankAccountHolder) {
      showToast.error("Vui lòng điền đầy đủ thông tin ngân hàng");
      return;
    }
    setBankLoading(true);
    try {
      await bankAccountApi.create(bankForm);
      showToast.success("Thêm tài khoản ngân hàng thành công!");
      setShowAddBank(false);
      setBankForm({ bankName: "", bankAccountNumber: "", bankAccountHolder: "", isDefault: false });
      fetchAll();
    } catch (err) {
      showToast.error(err.message || "Lỗi thêm tài khoản");
    } finally {
      setBankLoading(false);
    }
  }

  async function handleDeleteBank(id) {
    if (!(await showConfirm("Bạn có chắc muốn xóa tài khoản ngân hàng này?", "Xác nhận xóa tài khoản"))) return;
    try {
      await bankAccountApi.delete(id);
      showToast.success("Đã xóa tài khoản ngân hàng");
      fetchAll();
    } catch (err) {
      showToast.error(err.message || "Lỗi xóa tài khoản");
    }
  }

  async function handleSetDefault(id) {
    try {
      await bankAccountApi.setDefault(id);
      showToast.success("Đã đặt làm tài khoản mặc định");
      fetchAll();
    } catch (err) {
      showToast.error(err.message || "Lỗi");
    }
  }

  if (loading) {
    return (
      <div style={{ minHeight: "100vh", background: "#f8fafc", paddingTop: 100, textAlign: "center" }}>
        <div style={{ fontSize: 40 }}>💰</div>
        <p style={{ color: "#6b7280" }}>Đang tải ví...</p>
      </div>
    );
  }

  const balance = wallet?.availableBalance || 0;
  const frozen = wallet?.frozenBalance || 0;

  return (
    <div style={{ minHeight: "100vh", background: "#f8fafc", paddingTop: 90 }}>
      <div style={{ maxWidth: 700, margin: "0 auto", padding: "0 16px 40px" }}>
        {/* Balance card */}
        <div style={{
          background: "linear-gradient(135deg, #22c55e 0%, #16a34a 100%)",
          borderRadius: 24, padding: "32px 28px", color: "#fff", marginBottom: 24,
          boxShadow: "0 8px 32px rgba(34,197,94,0.3)",
        }}>
          <p style={{ fontSize: 13, fontWeight: 600, opacity: 0.85, letterSpacing: 1, textTransform: "uppercase" }}>Số dư ví chủ trạm</p>
          <div style={{ fontSize: 36, fontWeight: 800, marginTop: 4 }}>
            {balance.toLocaleString("vi-VN")}đ
          </div>
          {frozen > 0 && (
            <div style={{ fontSize: 13, opacity: 0.75, marginTop: 6 }}>
              🔒 Đang giữ: {frozen.toLocaleString("vi-VN")}đ
            </div>
          )}
          <div style={{ display: "flex", gap: 10, marginTop: 20, flexWrap: "wrap" }}>
            <WalletBtn onClick={() => { setShowWithdraw(true); setShowAddBank(false); }}>🏦 Rút tiền</WalletBtn>
            <WalletBtn onClick={() => navigate("/owner/booking-requests")}>📋 Quản lý Booking</WalletBtn>
            <WalletBtn onClick={() => navigate("/stations")}>⚡ Quản lý trạm</WalletBtn>
          </div>
        </div>

        {/* Withdraw form */}
        {showWithdraw && (
          <div style={{
            background: "#fff", borderRadius: 20, padding: 24,
            boxShadow: "0 2px 12px rgba(0,0,0,0.06)", marginBottom: 20,
            border: "2px solid #22c55e",
          }}>
            <h3 style={{ fontSize: 16, fontWeight: 700, color: "#1e293b", marginBottom: 16 }}>🏦 Rút tiền về ngân hàng</h3>

            {bankAccounts.length === 0 ? (
              <div style={{ textAlign: "center", padding: "20px 0" }}>
                <p style={{ color: "#64748b", fontSize: 14, marginBottom: 12 }}>Bạn chưa thêm tài khoản ngân hàng nào</p>
                <button
                  onClick={() => { setShowAddBank(true); setShowWithdraw(false); }}
                  style={{
                    padding: "10px 20px", borderRadius: 10, border: "none",
                    background: "linear-gradient(135deg, #3b82f6, #2563eb)",
                    color: "#fff", fontWeight: 700, fontSize: 13, cursor: "pointer",
                  }}
                >+ Thêm tài khoản ngân hàng</button>
              </div>
            ) : (
              <>
                <div style={{ display: "flex", flexDirection: "column", gap: 12 }}>
                  <div>
                    <label style={{ fontSize: 13, fontWeight: 600, color: "#374151", display: "block", marginBottom: 4 }}>Tài khoản ngân hàng</label>
                    <select
                      value={withdrawForm.bankAccountId}
                      onChange={e => setWithdrawForm(f => ({ ...f, bankAccountId: e.target.value }))}
                      style={{
                        width: "100%", padding: "10px 14px", borderRadius: 10,
                        border: "1.5px solid #e5e7eb", fontSize: 14, outline: "none",
                        background: "#fff", cursor: "pointer",
                      }}
                    >
                      <option value="">-- Chọn tài khoản --</option>
                      {bankAccounts.map(ba => (
                        <option key={ba.id} value={ba.id}>
                          {ba.bankName} - {ba.bankAccountNumber} ({ba.bankAccountHolder}) {ba.isDefault ? "⭐" : ""}
                        </option>
                      ))}
                    </select>
                  </div>
                  <FormInput label="Số tiền rút" type="number" placeholder="VD: 100000"
                    value={withdrawForm.amount} onChange={v => setWithdrawForm(f => ({ ...f, amount: v }))} />
                  <FormInput label="Ghi chú (tùy chọn)" placeholder="VD: Rút tiền tháng 3"
                    value={withdrawForm.note} onChange={v => setWithdrawForm(f => ({ ...f, note: v }))} />
                </div>
                <div style={{ display: "flex", gap: 10, marginTop: 16 }}>
                  <ActionBtn onClick={handleWithdraw} disabled={withdrawLoading} bg="linear-gradient(135deg, #22c55e, #16a34a)">
                    {withdrawLoading ? "Đang xử lý..." : "Gửi yêu cầu rút tiền"}
                  </ActionBtn>
                  <button onClick={() => setShowWithdraw(false)}
                    style={{ padding: "12px 20px", borderRadius: 12, border: "1.5px solid #e5e7eb", background: "#fff", color: "#64748b", fontWeight: 600, cursor: "pointer" }}>
                    Hủy
                  </button>
                </div>
                <button
                  onClick={() => { setShowAddBank(true); setShowWithdraw(false); }}
                  style={{
                    marginTop: 12, padding: "8px 16px", borderRadius: 8, border: "1.5px solid #e5e7eb",
                    background: "#f9fafb", color: "#3b82f6", fontWeight: 600, fontSize: 12, cursor: "pointer",
                  }}
                >+ Thêm TK ngân hàng mới</button>
              </>
            )}
          </div>
        )}

        {/* Add bank account form */}
        {showAddBank && (
          <div style={{
            background: "#fff", borderRadius: 20, padding: 24,
            boxShadow: "0 2px 12px rgba(0,0,0,0.06)", marginBottom: 20,
            border: "2px solid #3b82f6",
          }}>
            <h3 style={{ fontSize: 16, fontWeight: 700, color: "#1e293b", marginBottom: 16 }}>🏦 Thêm tài khoản ngân hàng</h3>
            <div style={{ display: "flex", flexDirection: "column", gap: 12 }}>
              <BankCombobox 
                value={bankForm.bankName} 
                onChange={v => setBankForm(f => ({ ...f, bankName: v }))} 
              />
              <FormInput label="Số tài khoản" placeholder="VD: 1234567890"
                value={bankForm.bankAccountNumber} onChange={v => setBankForm(f => ({ ...f, bankAccountNumber: v }))} />
              <FormInput label="Chủ tài khoản" placeholder="VD: NGUYEN VAN A"
                value={bankForm.bankAccountHolder} onChange={v => setBankForm(f => ({ ...f, bankAccountHolder: v }))} />
              <label style={{ display: "flex", alignItems: "center", gap: 8, fontSize: 13, color: "#374151", cursor: "pointer" }}>
                <input type="checkbox" checked={bankForm.isDefault}
                  onChange={e => setBankForm(f => ({ ...f, isDefault: e.target.checked }))}
                  style={{ width: 16, height: 16, accentColor: "#3b82f6" }} />
                Đặt làm tài khoản mặc định
              </label>
            </div>
            <div style={{ display: "flex", gap: 10, marginTop: 16 }}>
              <ActionBtn onClick={handleAddBank} disabled={bankLoading} bg="linear-gradient(135deg, #3b82f6, #2563eb)">
                {bankLoading ? "Đang xử lý..." : "Thêm tài khoản"}
              </ActionBtn>
              <button onClick={() => setShowAddBank(false)}
                style={{ padding: "12px 20px", borderRadius: 12, border: "1.5px solid #e5e7eb", background: "#fff", color: "#64748b", fontWeight: 600, cursor: "pointer" }}>
                Hủy
              </button>
            </div>
          </div>
        )}

        {/* Tabs */}
        <div style={{
          display: "flex", gap: 4, background: "#f1f5f9", borderRadius: 12,
          padding: 4, marginBottom: 20,
        }}>
          {[
            { key: "transactions", label: `Giao dịch (${transactions.length})` },
            { key: "withdraws", label: `Rút tiền (${withdraws.length})` },
            { key: "bank-accounts", label: `TK Ngân hàng (${bankAccounts.length})` },
          ].map(t => (
            <button
              key={t.key}
              onClick={() => setActiveTab(t.key)}
              style={{
                flex: 1, padding: "10px 4px", borderRadius: 10, border: "none",
                background: activeTab === t.key ? "#fff" : "transparent",
                color: activeTab === t.key ? "#1e293b" : "#64748b",
                fontWeight: activeTab === t.key ? 700 : 500,
                fontSize: 12, cursor: "pointer",
                boxShadow: activeTab === t.key ? "0 1px 4px rgba(0,0,0,0.08)" : "none",
                transition: "all 0.2s",
              }}
            >{t.label}</button>
          ))}
        </div>

        {/* Transaction history */}
        {activeTab === "transactions" && (
          <>
            <h2 style={{ fontSize: 20, fontWeight: 800, color: "#1e293b", marginBottom: 16 }}>Lịch sử giao dịch</h2>
            {transactions.length === 0 ? (
              <EmptyState icon="📋" text="Chưa có giao dịch nào" />
            ) : (
              <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
                {transactions.filter(tx => {
                  const txType = tx.type || tx.transactionType || "";
                  if (txType === "WithdrawRequest") {
                    // Chỉ hiển thị giao dịch rút tiền nếu lệnh rút đã Hoàn tất (Completed)
                    const txTime = new Date(String(tx.createdAt).replace("Z", "")).getTime();
                    return withdraws.some(wr => {
                      if (wr.status !== "Completed") return false;
                      const wrTime = new Date(String(wr.requestedAt).replace("Z", "")).getTime();
                      return Math.abs(txTime - wrTime) < 5000;
                    });
                  }
                  return true;
                }).map((tx) => {
                  const txType = tx.type || tx.transactionType || "";
                  const typeInfo = txTypeLabels[txType] || { label: txType, icon: "📄", color: "#6b7280" };
                  const amount = tx.amount || 0;
                  const direction = (tx.direction || "").toLowerCase();
                  const isIncome = direction === "credit";
                  return (
                    <div key={tx.id} onClick={() => setSelectedTx(tx)} style={{
                      background: "#fff", borderRadius: 16, padding: "16px 20px",
                      boxShadow: "0 2px 8px rgba(0,0,0,0.04)", display: "flex",
                      alignItems: "center", gap: 12, cursor: "pointer",
                      transition: "transform 0.1s, box-shadow 0.1s"
                    }}
                    onMouseEnter={(e) => { e.currentTarget.style.transform = "scale(1.01)"; e.currentTarget.style.boxShadow = "0 4px 12px rgba(0,0,0,0.08)"; }}
                    onMouseLeave={(e) => { e.currentTarget.style.transform = "none"; e.currentTarget.style.boxShadow = "0 2px 8px rgba(0,0,0,0.04)"; }}
                    >
                      <div style={{
                        width: 42, height: 42, borderRadius: 12,
                        background: `${typeInfo.color}15`, display: "flex",
                        alignItems: "center", justifyContent: "center", fontSize: 20, flexShrink: 0,
                      }}>{typeInfo.icon}</div>
                      <div style={{ flex: 1, minWidth: 0 }}>
                        <div style={{ fontWeight: 700, fontSize: 14, color: "#1e293b" }}>{typeInfo.label}</div>
                        <div style={{ fontSize: 12, color: "#94a3b8", marginTop: 2 }}>{tx.memo || tx.description || tx.note || "—"}</div>
                        <div style={{ fontSize: 11, color: "#cbd5e1", marginTop: 2 }}>{toLocal(tx.createdAt)}</div>
                      </div>
                      <div style={{ fontWeight: 800, fontSize: 15, color: isIncome ? "#22c55e" : "#ef4444" }}>
                        {isIncome ? "+" : "−"}{Math.abs(amount).toLocaleString("vi-VN")}đ
                      </div>
                    </div>
                  );
                })}
                <Pagination
                  page={txPage}
                  totalCount={txTotal}
                  pageSize={20}
                  onPageChange={(p) => setTxPage(p)}
                />
              </div>
            )}
          </>
        )}

        {/* Withdraw history */}
        {activeTab === "withdraws" && (
          <>
            <h2 style={{ fontSize: 20, fontWeight: 800, color: "#1e293b", marginBottom: 16 }}>Lịch sử rút tiền</h2>

            {withdraws.length === 0 ? (
              <EmptyState icon="🏦" text="Chưa có yêu cầu rút tiền nào" />
            ) : (
              <div style={{ display: "flex", flexDirection: "column", gap: 12 }}>
                {withdraws.map((p) => {
                  const st = withdrawStatusLabels[p.status] || withdrawStatusLabels.Pending;
                  return (
                    <div key={p.id} style={{
                      background: "#fff", borderRadius: 16, padding: "16px 20px",
                      boxShadow: "0 2px 8px rgba(0,0,0,0.04)", borderLeft: `4px solid ${st.color}`,
                    }}>
                      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 8 }}>
                        <div style={{ fontWeight: 700, fontSize: 16, color: "#1e293b" }}>
                          {(p.amount || 0).toLocaleString("vi-VN")}đ
                        </div>
                        <span style={{
                          fontSize: 11, fontWeight: 700, padding: "4px 12px", borderRadius: 20,
                          background: st.bg, color: st.color,
                        }}>{st.label}</span>
                      </div>
                      <div style={{ fontSize: 13, color: "#64748b" }}>
                        <div>Ngân hàng: <strong>{p.bankName}</strong> - {p.bankAccountNumber}</div>
                        {p.userNote && <div style={{ marginTop: 4 }}>📝 Ghi chú: {p.userNote}</div>}
                        {p.adminNote && <div style={{ color: "#dc2626", marginTop: 4 }}>⚠️ Admin: {p.adminNote}</div>}
                        {p.issueReason && <div style={{ color: "#ef4444", marginTop: 4, fontWeight: 600 }}>🔴 Báo lỗi: {p.issueReason}</div>}
                        <div style={{ fontSize: 11, color: "#cbd5e1", marginTop: 6 }}>{toLocal(p.requestedAt)}</div>
                      </div>

                      {p.transferReceiptUrl && (
                        <div style={{ marginTop: 12, padding: 12, background: "#f8fafc", borderRadius: 12 }}>
                          <p style={{ fontSize: 13, fontWeight: 700, color: "#374151", marginBottom: 8 }}>🧾 Biên lai giao dịch (từ Kế toán):</p>
                          <a href={p.transferReceiptUrl} target="_blank" rel="noreferrer">
                            <img src={p.transferReceiptUrl} alt="Biên lai" style={{ width: "100%", maxWidth: 300, objectFit: "contain", borderRadius: 8, border: "1px solid #e2e8f0", background: "#fff" }} />
                          </a>
                        </div>
                      )}

                      {p.status === "TransferCompleted" && (
                        <div style={{ marginTop: 16, paddingTop: 16, borderTop: "1px dashed #e2e8f0" }}>
                          <p style={{ fontSize: 13, color: "#475569", marginBottom: 10, fontWeight: 500 }}>
                            Hệ thống đã chuyển khoản. Vui lòng xác nhận hoặc báo lỗi nếu chưa nhận được tiền:
                          </p>
                          {issueForm.id === p.id ? (
                            <div style={{ display: "flex", flexDirection: "column", gap: 10 }}>
                              <input 
                                type="text"
                                placeholder="Nhập lý do chưa nhận được tiền..."
                                value={issueForm.reason}
                                onChange={(e) => setIssueForm({ ...issueForm, reason: e.target.value })}
                                style={{ width: "100%", padding: "10px 14px", borderRadius: 10, border: "1.5px solid #ef4444", outline: "none", fontSize: 13, boxSizing: "border-box" }}
                              />
                              <div style={{ display: "flex", gap: 8 }}>
                                <button onClick={handleReportIssue} disabled={issueLoading} style={{ flex: 1, padding: "10px", background: "#ef4444", color: "#fff", borderRadius: 10, border: "none", fontWeight: 600, cursor: "pointer" }}>Gửi báo cáo</button>
                                <button onClick={() => setIssueForm({ id: null, reason: "" })} disabled={issueLoading} style={{ flex: 1, padding: "10px", background: "#f1f5f9", color: "#475569", borderRadius: 10, border: "none", fontWeight: 600, cursor: "pointer" }}>Hủy</button>
                              </div>
                            </div>
                          ) : (
                            <div style={{ display: "flex", gap: 10 }}>
                              <button onClick={() => handleConfirmWithdraw(p.id)} style={{ flex: 1, padding: "10px", background: "linear-gradient(135deg, #22c55e, #16a34a)", color: "#fff", borderRadius: 10, border: "none", fontWeight: 700, cursor: "pointer", boxShadow: "0 4px 10px rgba(34,197,94,0.2)" }}>
                                🟢 Đã nhận tiền
                              </button>
                              <button onClick={() => setIssueForm({ id: p.id, reason: "" })} style={{ flex: 1, padding: "10px", background: "#fff", color: "#ef4444", borderRadius: 10, border: "1.5px solid #fca5a5", fontWeight: 700, cursor: "pointer" }}>
                                🔴 Báo lỗi
                              </button>
                            </div>
                          )}
                        </div>
                      )}
                    </div>
                  );
                })}
                <Pagination
                  page={wrPage}
                  totalCount={wrTotal}
                  pageSize={20}
                  onPageChange={(p) => setWrPage(p)}
                />
              </div>
            )}
          </>
        )}

        {/* Bank accounts */}
        {activeTab === "bank-accounts" && (
          <>
            <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 16 }}>
              <h2 style={{ fontSize: 20, fontWeight: 800, color: "#1e293b", margin: 0 }}>Tài khoản ngân hàng</h2>
              <button
                onClick={() => setShowAddBank(true)}
                style={{
                  padding: "8px 16px", borderRadius: 10, border: "none",
                  background: "linear-gradient(135deg, #3b82f6, #2563eb)",
                  color: "#fff", fontWeight: 600, fontSize: 12, cursor: "pointer",
                }}
              >+ Thêm mới</button>
            </div>
            {bankAccounts.length === 0 ? (
              <EmptyState icon="🏦" text="Chưa có tài khoản ngân hàng nào" />
            ) : (
              <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
                {bankAccounts.map((ba) => (
                  <div key={ba.id} style={{
                    background: "#fff", borderRadius: 16, padding: "16px 20px",
                    boxShadow: "0 2px 8px rgba(0,0,0,0.04)",
                    borderLeft: ba.isDefault ? "4px solid #22c55e" : "4px solid #e5e7eb",
                  }}>
                    <div style={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start" }}>
                      <div>
                        <div style={{ fontWeight: 700, fontSize: 15, color: "#1e293b" }}>
                          {ba.bankName} {ba.isDefault && <span style={{ fontSize: 11, color: "#22c55e" }}>⭐ Mặc định</span>}
                        </div>
                        <div style={{ fontSize: 13, color: "#64748b", marginTop: 4 }}>
                          STK: {ba.bankAccountNumber}
                        </div>
                        <div style={{ fontSize: 13, color: "#64748b" }}>
                          👤 {ba.bankAccountHolder}
                        </div>
                      </div>
                      <div style={{ display: "flex", gap: 6 }}>
                        {!ba.isDefault && (
                          <button onClick={() => handleSetDefault(ba.id)}
                            style={{ padding: "6px 12px", borderRadius: 8, border: "1.5px solid #e5e7eb", background: "#fff", color: "#22c55e", fontWeight: 600, fontSize: 11, cursor: "pointer" }}>
                            ⭐ Mặc định
                          </button>
                        )}
                        <button onClick={() => handleDeleteBank(ba.id)}
                          style={{ padding: "6px 12px", borderRadius: 8, border: "1.5px solid #fca5a5", background: "#fff", color: "#ef4444", fontWeight: 600, fontSize: 11, cursor: "pointer" }}>
                          🗑 Xóa
                        </button>
                      </div>
                    </div>
                  </div>
                ))}
              </div>
            )}
          </>
        )}
      </div>

      {/* Transaction Detail Modal */}
      {selectedTx && (
        <div 
          onClick={() => setSelectedTx(null)}
          style={{
            position: "fixed", inset: 0, zIndex: 9999, background: "rgba(15,23,42,0.6)", backdropFilter: "blur(4px)",
            display: "flex", alignItems: "center", justifyContent: "center", padding: 20
          }}
        >
          <div 
            onClick={e => e.stopPropagation()}
            style={{
              background: "#fff", borderRadius: 24, width: "100%", maxWidth: 400, overflow: "hidden",
              boxShadow: "0 20px 40px rgba(0,0,0,0.2)", display: "flex", flexDirection: "column"
            }}
          >
            {/* Header */}
            <div style={{ padding: "20px 24px", borderBottom: "1px solid #f1f5f9", display: "flex", justifyContent: "space-between", alignItems: "center" }}>
              <h3 style={{ fontSize: 18, fontWeight: 700, color: "#1e293b", margin: 0 }}>Chi tiết giao dịch</h3>
              <button 
                onClick={() => setSelectedTx(null)} 
                style={{ background: "none", border: "none", fontSize: 24, color: "#94a3b8", cursor: "pointer", display: "flex", alignItems: "center", justifyContent: "center", width: 32, height: 32, borderRadius: 8 }}
                onMouseEnter={e => e.currentTarget.style.background = "#f1f5f9"}
                onMouseLeave={e => e.currentTarget.style.background = "none"}
              >&times;</button>
            </div>
            
            {/* Content */}
            <div style={{ padding: 24 }}>
              <div style={{ textAlign: "center", marginBottom: 24 }}>
                <div style={{ fontSize: 48, marginBottom: 12 }}>
                  {txTypeLabels[selectedTx.type || selectedTx.transactionType]?.icon || "📄"}
                </div>
                <div style={{ fontSize: 24, fontWeight: 800, color: (selectedTx.direction || "").toLowerCase() === "credit" ? "#22c55e" : "#ef4444" }}>
                  {(selectedTx.direction || "").toLowerCase() === "credit" ? "+" : "−"}{Math.abs(selectedTx.amount || 0).toLocaleString("vi-VN")}đ
                </div>
                <div style={{ fontSize: 15, fontWeight: 600, color: "#475569", marginTop: 4 }}>
                  {txTypeLabels[selectedTx.type || selectedTx.transactionType]?.label || selectedTx.type || selectedTx.transactionType}
                </div>
              </div>
              
              <div style={{ background: "#f8fafc", borderRadius: 16, padding: 16, display: "flex", flexDirection: "column", gap: 12 }}>
                <div style={{ display: "flex", justifyContent: "space-between" }}>
                  <span style={{ color: "#64748b", fontSize: 13 }}>Mã giao dịch</span>
                  <span style={{ color: "#1e293b", fontSize: 13, fontWeight: 600 }}>#{selectedTx.id}</span>
                </div>
                <div style={{ display: "flex", justifyContent: "space-between" }}>
                  <span style={{ color: "#64748b", fontSize: 13 }}>Thời gian</span>
                  <span style={{ color: "#1e293b", fontSize: 13, fontWeight: 600 }}>{toLocal(selectedTx.createdAt)}</span>
                </div>
                {selectedTx.bookingId && (
                  <div style={{ display: "flex", justifyContent: "space-between" }}>
                    <span style={{ color: "#64748b", fontSize: 13 }}>Mã Booking</span>
                    <span style={{ color: "#1e293b", fontSize: 13, fontWeight: 600 }}>#{selectedTx.bookingId}</span>
                  </div>
                )}
                {(selectedTx.memo || selectedTx.description || selectedTx.note) && (
                  <div style={{ borderTop: "1px dashed #cbd5e1", paddingTop: 12, marginTop: 4 }}>
                    <div style={{ color: "#64748b", fontSize: 13, marginBottom: 4 }}>Chi tiết</div>
                    <div style={{ color: "#1e293b", fontSize: 14, fontWeight: 500, lineHeight: 1.5 }}>
                      {selectedTx.memo || selectedTx.description || selectedTx.note}
                    </div>
                  </div>
                )}
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

function WalletBtn({ onClick, children }) {
  return (
    <button onClick={onClick}
      style={{
        padding: "10px 20px", borderRadius: 12, border: "2px solid rgba(255,255,255,0.4)",
        background: "rgba(255,255,255,0.15)", color: "#fff", fontWeight: 600,
        fontSize: 13, cursor: "pointer", backdropFilter: "blur(4px)", transition: "all 0.2s",
      }}
      onMouseEnter={e => { e.currentTarget.style.background = "rgba(255,255,255,0.3)"; }}
      onMouseLeave={e => { e.currentTarget.style.background = "rgba(255,255,255,0.15)"; }}
    >{children}</button>
  );
}

function FormInput({ label, type = "text", placeholder, value, onChange }) {
  return (
    <div>
      <label style={{ fontSize: 13, fontWeight: 600, color: "#374151", display: "block", marginBottom: 4 }}>{label}</label>
      <input type={type} value={value} onChange={e => onChange(e.target.value)} placeholder={placeholder}
        style={{
          width: "100%", padding: "10px 14px", borderRadius: 10,
          border: "1.5px solid #e5e7eb", fontSize: 14, outline: "none", boxSizing: "border-box",
        }}
        onFocus={e => { e.target.style.borderColor = "#22c55e"; }}
        onBlur={e => { e.target.style.borderColor = "#e5e7eb"; }}
      />
    </div>
  );
}

function ActionBtn({ onClick, disabled, bg, children }) {
  return (
    <button onClick={onClick} disabled={disabled}
      style={{
        flex: 1, padding: "12px 0", borderRadius: 12, border: "none",
        background: disabled ? "#d1d5db" : bg,
        color: "#fff", fontWeight: 700, fontSize: 14,
        cursor: disabled ? "not-allowed" : "pointer",
        boxShadow: "0 4px 14px rgba(0,0,0,0.1)",
      }}
    >{children}</button>
  );
}

function EmptyState({ icon, text }) {
  return (
    <div style={{
      textAlign: "center", padding: 40, background: "#fff", borderRadius: 16,
      boxShadow: "0 2px 8px rgba(0,0,0,0.04)",
    }}>
      <div style={{ fontSize: 48, marginBottom: 8 }}>{icon}</div>
      <p style={{ color: "#6b7280" }}>{text}</p>
    </div>
  );
}
