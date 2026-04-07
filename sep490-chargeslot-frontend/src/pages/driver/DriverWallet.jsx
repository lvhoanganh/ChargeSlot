import { useState, useEffect } from "react";
import { useNavigate } from "react-router-dom";
import { walletApi } from "@/services/api";
import { showToast } from "@/components/Toast";
import QRCodeModal from "@/components/QRCodeModal";
import BankCombobox from "@/components/BankCombobox";
import { showConfirm } from "@/components/ConfirmDialog";

const txTypeLabels = {
  TopUp: { label: "Nạp tiền", icon: "💰", color: "#22c55e" },
  BookingPayment: { label: "Thanh toán booking", icon: "💳", color: "#ef4444" },
  BookingCancel: { label: "Hoàn tiền hủy booking", icon: "↩️", color: "#3b82f6" },
  Payment: { label: "Thanh toán", icon: "💳", color: "#ef4444" },
  Refund: { label: "Hoàn tiền", icon: "↩️", color: "#3b82f6" },
  Withdraw: { label: "Rút tiền", icon: "🏦", color: "#f59e0b" },
  WithdrawRequest: { label: "Tạm giữ lệnh Rút tiền", icon: "⏳", color: "#f59e0b" },
  WithdrawRejected: { label: "Hoàn tiền huỷ lệnh rút", icon: "↩️", color: "#3b82f6" },
  Earning: { label: "Thu nhập", icon: "📈", color: "#22c55e" },
  OwnerPayout: { label: "Nhận thanh toán", icon: "💰", color: "#22c55e" },
};

const withdrawStatusLabels = {
  Pending: { label: "Chờ xử lý", color: "#f59e0b", bg: "#fffbeb" },
  Approved: { label: "Đã duyệt", color: "#3b82f6", bg: "#eff6ff" },
  TransferCompleted: { label: "Đã chuyển khoản", color: "#22c55e", bg: "#f0fdf4" },
  Completed: { label: "Thành công", color: "#16a34a", bg: "#dcfce7" },
  IssueReported: { label: "Báo lỗi", color: "#ef4444", bg: "#fef2f2" },
  Rejected: { label: "Từ chối", color: "#ef4444", bg: "#fef2f2" },
};

const TOP_UP_AMOUNTS = [50000, 100000, 200000, 500000];

const toLocal = (dt) => {
  if (!dt) return "";
  const s = String(dt);
  return new Date(String(s).replace("Z", "")).toLocaleString("vi-VN");
};

export default function DriverWallet() {
  const navigate = useNavigate();
  const [wallet, setWallet] = useState(null);
  const [transactions, setTransactions] = useState([]);
  const [withdrawRequests, setWithdrawRequests] = useState([]);
  const [loading, setLoading] = useState(true);
  const [activeTab, setActiveTab] = useState("transactions"); // transactions | withdraw-history
  const [withdrawFilter, setWithdrawFilter] = useState("all");
  const [selectedTx, setSelectedTx] = useState(null);

  // Top-up
  const [showTopUp, setShowTopUp] = useState(false);
  const [topUpAmount, setTopUpAmount] = useState("");
  const [topUpLoading, setTopUpLoading] = useState(false);
  const [sepayOpen, setSepayOpen] = useState(false);
  const [sepayUrl, setSepayUrl] = useState("");
  const [initialBalance, setInitialBalance] = useState(0);

  // Withdraw
  const [showWithdraw, setShowWithdraw] = useState(false);
  const [withdrawForm, setWithdrawForm] = useState({
    amount: "", bankName: "", bankAccountNumber: "", bankAccountHolder: "", userNote: "",
  });
  const [withdrawLoading, setWithdrawLoading] = useState(false);

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

  function fetchAll() {
    Promise.all([
      walletApi.getWallet().catch(() => null),
      walletApi.getTransactions().catch(() => ({})),
      walletApi.getWithdrawRequests().catch(() => ({})),
    ]).then(([w, txsData, wrsData]) => {
      setWallet(w);
      // BE trả { total, page, pageSize, items } — phải unpack .items
      setTransactions(txsData?.items ?? (Array.isArray(txsData) ? txsData : []));
      setWithdrawRequests(wrsData?.items ?? (Array.isArray(wrsData) ? wrsData : []));
    }).finally(() => setLoading(false));
  }

  useEffect(() => { fetchAll(); }, []);

  useEffect(() => {
    if (!sepayOpen) return;
    const timer = setInterval(() => {
      walletApi.getWallet()
        .then((data) => {
          if (data && data.availableBalance > initialBalance) {
            setSepayOpen(false);
            showToast.success("🎉 Nạp tiền thành công!");
            setShowTopUp(false);
            setTopUpAmount("");
            fetchAll();
          }
        })
        .catch(() => {});
    }, 3000);
    return () => clearInterval(timer);
  }, [sepayOpen, initialBalance]);

  async function handleTopUp() {
    const amt = Number(topUpAmount);
    if (!amt || amt < 10000) {
      showToast.error("Số tiền tối thiểu là 10.000đ");
      return;
    }
    setTopUpLoading(true);
    setInitialBalance(wallet?.availableBalance || 0);
    try {
      const res = await walletApi.topUp(amt);
      if (res?.qrUrl) {
         setSepayUrl(res.qrUrl);
         setSepayOpen(true);
      } else if (typeof res === 'string') {
         setSepayUrl(res);
         setSepayOpen(true);
      } else if (res?.paymentUrl) {
        window.location.href = res.paymentUrl;
      } else {
        showToast.success("Nạp tiền thành công!");
        setShowTopUp(false);
        setTopUpAmount("");
        fetchAll();
      }
    } catch (err) {
      showToast.error(err.message || "Lỗi nạp tiền");
    } finally {
      setTopUpLoading(false);
    }
  }

  async function handleWithdraw() {
    const amt = Number(withdrawForm.amount);
    if (!amt || amt < 10000) {
      showToast.error("Số tiền rút tối thiểu là 10.000đ");
      return;
    }
    if (!withdrawForm.bankName || !withdrawForm.bankAccountNumber || !withdrawForm.bankAccountHolder) {
      showToast.error("Vui lòng điền đầy đủ thông tin ngân hàng");
      return;
    }
    setWithdrawLoading(true);
    try {
      await walletApi.withdraw({
        amount: amt,
        bankName: withdrawForm.bankName,
        bankAccountNumber: withdrawForm.bankAccountNumber,
        bankAccountHolder: withdrawForm.bankAccountHolder,
        userNote: withdrawForm.userNote,
      });
      showToast.success("Yêu cầu rút tiền đã gửi! Chờ admin duyệt.");
      setShowWithdraw(false);
      setWithdrawForm({ amount: "", bankName: "", bankAccountNumber: "", bankAccountHolder: "", userNote: "" });
      fetchAll();
    } catch (err) {
      showToast.error(err.message || "Lỗi rút tiền");
    } finally {
      setWithdrawLoading(false);
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
    <div style={{ minHeight: "100vh", background: "#f8fafc", paddingTop: 68 }}>
      <div style={{ maxWidth: 700, margin: "0 auto", padding: "0 16px", paddingBottom: "calc(80px + env(safe-area-inset-bottom, 0px))" }}>
        {/* Balance card */}
        <div style={{
          background: "linear-gradient(135deg, #f97316 0%, #ea580c 100%)",
          borderRadius: 24, padding: "32px 28px", color: "#fff", marginBottom: 24,
          boxShadow: "0 8px 32px rgba(249,115,22,0.3)",
        }}>
          <p style={{ fontSize: 13, fontWeight: 600, opacity: 0.85, letterSpacing: 1, textTransform: "uppercase" }}>Số dư ví</p>
          <div style={{ fontSize: 36, fontWeight: 800, marginTop: 4 }}>
            {balance.toLocaleString("vi-VN")}đ
          </div>
          {frozen > 0 && (
            <div style={{ fontSize: 13, opacity: 0.75, marginTop: 6 }}>
              🔒 Đang giữ: {frozen.toLocaleString("vi-VN")}đ
            </div>
          )}
          <div style={{ display: "flex", gap: 10, marginTop: 20, flexWrap: "wrap" }}>
            <WalletBtn onClick={() => { setShowTopUp(true); setShowWithdraw(false); }}>💰 Nạp tiền</WalletBtn>
            <WalletBtn onClick={() => { setShowWithdraw(true); setShowTopUp(false); }}>🏦 Rút tiền</WalletBtn>
            <WalletBtn onClick={() => navigate("/driver/my-bookings")}>📋 Booking</WalletBtn>
          </div>
        </div>

        {/* Top-up form */}
        {showTopUp && (
          <div style={{
            background: "#fff", borderRadius: 20, padding: 24,
            boxShadow: "0 2px 12px rgba(0,0,0,0.06)", marginBottom: 20,
            border: "2px solid #f97316",
          }}>
            <h3 style={{ fontSize: 16, fontWeight: 700, color: "#1e293b", marginBottom: 16 }}>💰 Nạp tiền vào ví</h3>
            <div style={{ display: "flex", gap: 8, flexWrap: "wrap", marginBottom: 12 }}>
              {TOP_UP_AMOUNTS.map(amt => (
                <button
                  key={amt}
                  onClick={() => setTopUpAmount(String(amt))}
                  style={{
                    padding: "8px 16px", borderRadius: 10, fontSize: 13, fontWeight: 600, cursor: "pointer",
                    border: topUpAmount === String(amt) ? "2px solid #f97316" : "1.5px solid #e5e7eb",
                    background: topUpAmount === String(amt) ? "#fff7ed" : "#fff",
                    color: topUpAmount === String(amt) ? "#ea580c" : "#374151",
                  }}
                >
                  {amt.toLocaleString("vi-VN")}đ
                </button>
              ))}
            </div>
            <input
              type="number"
              value={topUpAmount}
              onChange={e => setTopUpAmount(e.target.value)}
              placeholder="Hoặc nhập số tiền..."
              style={{
                width: "100%", padding: "12px 16px", borderRadius: 12,
                border: "1.5px solid #e5e7eb", fontSize: 14, outline: "none", boxSizing: "border-box",
              }}
            />
            <div style={{ display: "flex", gap: 10, marginTop: 16 }}>
              <ActionButton onClick={handleTopUp} disabled={topUpLoading} bg="linear-gradient(135deg, #f97316, #ea580c)">
                {topUpLoading ? "Đang xử lý..." : "Nạp tiền qua VietQR"}
              </ActionButton>
              <button
                onClick={() => { setShowTopUp(false); setTopUpAmount(""); }}
                style={{ padding: "12px 20px", borderRadius: 12, border: "1.5px solid #e5e7eb", background: "#fff", color: "#64748b", fontWeight: 600, cursor: "pointer" }}
              >Hủy</button>
            </div>
          </div>
        )}

        {/* Withdraw form */}
        {showWithdraw && (
          <div style={{
            background: "#fff", borderRadius: 20, padding: 24,
            boxShadow: "0 2px 12px rgba(0,0,0,0.06)", marginBottom: 20,
            border: "2px solid #3b82f6",
          }}>
            <h3 style={{ fontSize: 16, fontWeight: 700, color: "#1e293b", marginBottom: 16 }}>🏦 Rút tiền</h3>
            <div style={{ display: "flex", flexDirection: "column", gap: 12 }}>
              <FormInput label="Số tiền rút" type="number" placeholder="VD: 100000"
                value={withdrawForm.amount} onChange={v => setWithdrawForm(f => ({ ...f, amount: v }))} />
              <BankCombobox 
                value={withdrawForm.bankName} 
                onChange={v => setWithdrawForm(f => ({ ...f, bankName: v }))} 
              />
              <FormInput label="Số tài khoản" placeholder="VD: 1234567890"
                value={withdrawForm.bankAccountNumber} onChange={v => setWithdrawForm(f => ({ ...f, bankAccountNumber: v }))} />
              <FormInput label="Chủ tài khoản" placeholder="VD: NGUYEN VAN A"
                value={withdrawForm.bankAccountHolder} onChange={v => setWithdrawForm(f => ({ ...f, bankAccountHolder: v }))} />
              <FormInput label="Ghi chú (tùy chọn)" placeholder="VD: Rút tiền tháng 3"
                value={withdrawForm.userNote} onChange={v => setWithdrawForm(f => ({ ...f, userNote: v }))} />
            </div>
            <div style={{ display: "flex", gap: 10, marginTop: 16 }}>
              <ActionButton onClick={handleWithdraw} disabled={withdrawLoading} bg="linear-gradient(135deg, #3b82f6, #2563eb)">
                {withdrawLoading ? "Đang xử lý..." : "Gửi yêu cầu rút tiền"}
              </ActionButton>
              <button
                onClick={() => { setShowWithdraw(false); setWithdrawForm({ amount: "", bankName: "", bankAccountNumber: "", bankAccountHolder: "", userNote: "" }); }}
                style={{ padding: "12px 20px", borderRadius: 12, border: "1.5px solid #e5e7eb", background: "#fff", color: "#64748b", fontWeight: 600, cursor: "pointer" }}
              >Hủy</button>
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
            { key: "withdraw-history", label: `Rút tiền (${withdrawRequests.length})` },
          ].map(t => (
            <button
              key={t.key}
              onClick={() => setActiveTab(t.key)}
              style={{
                flex: 1, padding: "10px 8px", borderRadius: 10, border: "none",
                background: activeTab === t.key ? "#fff" : "transparent",
                color: activeTab === t.key ? "#1e293b" : "#64748b",
                fontWeight: activeTab === t.key ? 700 : 500,
                fontSize: 13, cursor: "pointer",
                boxShadow: activeTab === t.key ? "0 1px 4px rgba(0,0,0,0.08)" : "none",
                transition: "all 0.2s",
              }}
            >{t.label}</button>
          ))}
        </div>

        {/* Transaction history */}
        {activeTab === "transactions" && (
          <>
            <h2 style={{ fontSize: 20, fontWeight: 800, color: "#1e293b", marginBottom: 16 }}>
              Lịch sử giao dịch
            </h2>
            {transactions.length === 0 ? (
              <EmptyState icon="📋" text="Chưa có giao dịch nào" />
            ) : (
              <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
                {transactions.filter(tx => {
                  const txType = tx.type || tx.transactionType || "";
                  if (txType === "WithdrawRequest") {
                    // Chỉ hiển thị giao dịch rút tiền nếu lệnh rút đã Hoàn tất (Completed)
                    const txTime = new Date(String(tx.createdAt).replace("Z", "")).getTime();
                    return withdrawRequests.some(wr => {
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
                      }}>
                        {typeInfo.icon}
                      </div>
                      <div style={{ flex: 1, minWidth: 0 }}>
                        <div style={{ fontWeight: 700, fontSize: 14, color: "#1e293b" }}>{typeInfo.label}</div>
                        <div style={{ fontSize: 12, color: "#94a3b8", marginTop: 2 }}>
                          {tx.memo || tx.description || tx.note || "—"}
                        </div>
                        <div style={{ fontSize: 11, color: "#cbd5e1", marginTop: 2 }}>
                          {toLocal(tx.createdAt)}
                        </div>
                      </div>
                      <div style={{
                        fontWeight: 800, fontSize: 15,
                        color: isIncome ? "#22c55e" : "#ef4444",
                      }}>
                        {isIncome ? "+" : "−"}{Math.abs(amount).toLocaleString("vi-VN")}đ
                      </div>
                    </div>
                  );
                })}
              </div>
            )}
          </>
        )}

        {/* Withdraw requests history */}
        {activeTab === "withdraw-history" && (
          <>
            <h2 style={{ fontSize: 20, fontWeight: 800, color: "#1e293b", marginBottom: 16 }}>
              Lịch sử rút tiền
            </h2>
            
            {withdrawRequests.length > 0 && (
              <div style={{ display: "flex", gap: 8, marginBottom: 16, overflowX: "auto", paddingBottom: 4, scrollbarWidth: "none" }}>
                {[
                  { key: "all", label: "Tất cả" },
                  { key: "Pending", label: "Chờ duyệt" },
                  { key: "Approved", label: "Chờ CK" },
                  { key: "TransferCompleted", label: "Đã CK" },
                  { key: "Completed", label: "Thành công" },
                  { key: "Failed", label: "Lỗi/Từ chối" },
                ].map(t => (
                  <button
                    key={t.key}
                    onClick={() => setWithdrawFilter(t.key)}
                    style={{
                      flexShrink: 0, padding: "6px 14px", borderRadius: 20, fontSize: 13, fontWeight: 600,
                      border: "none", cursor: "pointer", transition: "all 0.15s",
                      background: withdrawFilter === t.key ? "#f97316" : "#f1f5f9",
                      color: withdrawFilter === t.key ? "#fff" : "#64748b",
                      boxShadow: withdrawFilter === t.key ? "0 2px 8px rgba(249,115,22,0.3)" : "none",
                    }}
                  >
                    {t.label}
                  </button>
                ))}
              </div>
            )}

            {withdrawRequests.length === 0 ? (
              <EmptyState icon="🏦" text="Chưa có yêu cầu rút tiền nào" />
            ) : (
              <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
                {withdrawRequests.filter(wr => {
                  if (withdrawFilter === "all") return true;
                  if (withdrawFilter === "Failed") return wr.status === "Rejected" || wr.status === "IssueReported";
                  return wr.status === withdrawFilter;
                }).map((wr) => {
                  const st = withdrawStatusLabels[wr.status] || withdrawStatusLabels.Pending;
                  return (
                    <div key={wr.id} style={{
                      background: "#fff", borderRadius: 16, padding: "16px 20px",
                      boxShadow: "0 2px 8px rgba(0,0,0,0.04)",
                      borderLeft: `4px solid ${st.color}`,
                    }}>
                      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 8 }}>
                        <div style={{ fontWeight: 700, fontSize: 16, color: "#1e293b" }}>
                          {(wr.amount || 0).toLocaleString("vi-VN")}đ
                        </div>
                        <span style={{
                          fontSize: 11, fontWeight: 700, padding: "4px 12px", borderRadius: 20,
                          background: st.bg, color: st.color,
                        }}>{st.label}</span>
                      </div>
                      <div style={{ fontSize: 13, color: "#64748b", display: "flex", flexDirection: "column", gap: 2 }}>
                        <div>🏦 {wr.bankName} · {wr.bankAccountNumber}</div>
                        <div>👤 {wr.bankAccountHolder}</div>
                        {wr.userNote && <div style={{ marginTop: 4 }}>📝 Ghi chú: {wr.userNote}</div>}
                        {wr.adminNote && <div style={{ color: "#dc2626", marginTop: 4 }}>⚠️ Admin: {wr.adminNote}</div>}
                        {wr.issueReason && <div style={{ color: "#ef4444", marginTop: 4, fontWeight: 600 }}>🔴 Báo lỗi: {wr.issueReason}</div>}
                        <div style={{ fontSize: 11, color: "#cbd5e1", marginTop: 6 }}>{toLocal(wr.requestedAt)}</div>
                      </div>

                      {wr.transferReceiptUrl && (
                        <div style={{ marginTop: 12, padding: 12, background: "#f8fafc", borderRadius: 12 }}>
                          <p style={{ fontSize: 13, fontWeight: 700, color: "#374151", marginBottom: 8 }}>🧾 Biên lai giao dịch (từ Kế toán):</p>
                          <a href={wr.transferReceiptUrl} target="_blank" rel="noreferrer">
                            <img src={wr.transferReceiptUrl} alt="Biên lai" style={{ width: "100%", maxWidth: 300, objectFit: "contain", borderRadius: 8, border: "1px solid #e2e8f0", background: "#fff" }} />
                          </a>
                        </div>
                      )}

                      {wr.status === "TransferCompleted" && (
                        <div style={{ marginTop: 16, paddingTop: 16, borderTop: "1px dashed #e2e8f0" }}>
                          <p style={{ fontSize: 13, color: "#475569", marginBottom: 10, fontWeight: 500 }}>
                            Hệ thống đã chuyển khoản. Vui lòng xác nhận hoặc báo lỗi nếu chưa nhận được tiền:
                          </p>
                          {issueForm.id === wr.id ? (
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
                              <button onClick={() => handleConfirmWithdraw(wr.id)} style={{ flex: 1, padding: "10px", background: "linear-gradient(135deg, #22c55e, #16a34a)", color: "#fff", borderRadius: 10, border: "none", fontWeight: 700, cursor: "pointer", boxShadow: "0 4px 10px rgba(34,197,94,0.2)" }}>
                                🟢 Đã nhận tiền
                              </button>
                              <button onClick={() => setIssueForm({ id: wr.id, reason: "" })} style={{ flex: 1, padding: "10px", background: "#fff", color: "#ef4444", borderRadius: 10, border: "1.5px solid #fca5a5", fontWeight: 700, cursor: "pointer" }}>
                                🔴 Báo lỗi
                              </button>
                            </div>
                          )}
                        </div>
                      )}
                    </div>
                  );
                })}
              </div>
            )}
          </>
        )}
      </div>

      {/* QR Payment Modal */}
      <QRCodeModal
        isOpen={sepayOpen}
        onClose={() => setSepayOpen(false)}
        qrUrl={sepayUrl}
        title="Nạp tiền qua VietQR"
        amount={Number(topUpAmount)}
        description="Quét mã QR để nạp tiền vào ví ChargeSlot"
      />

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
    <button
      onClick={onClick}
      style={{
        padding: "10px 20px", borderRadius: 12, border: "2px solid rgba(255,255,255,0.4)",
        background: "rgba(255,255,255,0.15)", color: "#fff", fontWeight: 600,
        fontSize: 13, cursor: "pointer", backdropFilter: "blur(4px)",
        transition: "all 0.2s",
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
      <input
        type={type}
        value={value}
        onChange={e => onChange(e.target.value)}
        placeholder={placeholder}
        style={{
          width: "100%", padding: "10px 14px", borderRadius: 10,
          border: "1.5px solid #e5e7eb", fontSize: 14, outline: "none", boxSizing: "border-box",
          transition: "border-color 0.2s",
        }}
        onFocus={e => { e.target.style.borderColor = "#3b82f6"; }}
        onBlur={e => { e.target.style.borderColor = "#e5e7eb"; }}
      />
    </div>
  );
}

function ActionButton({ onClick, disabled, bg, children }) {
  return (
    <button
      onClick={onClick}
      disabled={disabled}
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
