import { useState, useEffect } from "react";
import { useNavigate } from "react-router-dom";
import { walletApi } from "@/services/api";
import { showToast } from "@/components/Toast";
import QRCodeModal from "@/components/QRCodeModal";

const txTypeLabels = {
  TopUp: { label: "Nạp tiền", icon: "💰", color: "#22c55e" },
  BookingPayment: { label: "Thanh toán booking", icon: "💳", color: "#ef4444" },
  BookingCancel: { label: "Hoàn tiền hủy booking", icon: "↩️", color: "#3b82f6" },
  Payment: { label: "Thanh toán", icon: "💳", color: "#ef4444" },
  Refund: { label: "Hoàn tiền", icon: "↩️", color: "#3b82f6" },
  Withdraw: { label: "Rút tiền", icon: "🏦", color: "#f59e0b" },
  Earning: { label: "Thu nhập", icon: "📈", color: "#22c55e" },
  OwnerPayout: { label: "Nhận thanh toán", icon: "💰", color: "#22c55e" },
};

const withdrawStatusLabels = {
  Pending: { label: "Chờ duyệt", color: "#f59e0b", bg: "#fffbeb" },
  Approved: { label: "Đã duyệt", color: "#22c55e", bg: "#f0fdf4" },
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

  function fetchAll() {
    Promise.all([
      walletApi.getWallet().catch(() => null),
      walletApi.getTransactions().catch(() => []),
      walletApi.getWithdrawRequests().catch(() => []),
    ]).then(([w, txs, wrs]) => {
      setWallet(w);
      setTransactions(Array.isArray(txs) ? txs : []);
      setWithdrawRequests(Array.isArray(wrs) ? wrs : []);
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
    <div style={{ minHeight: "100vh", background: "#f8fafc", paddingTop: 90 }}>
      <div style={{ maxWidth: 700, margin: "0 auto", padding: "0 16px 40px" }}>
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
              <FormInput label="Tên ngân hàng" placeholder="VD: Vietcombank"
                value={withdrawForm.bankName} onChange={v => setWithdrawForm(f => ({ ...f, bankName: v }))} />
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
                {transactions.map((tx) => {
                  const txType = tx.type || tx.transactionType || "";
                  const typeInfo = txTypeLabels[txType] || { label: txType, icon: "📄", color: "#6b7280" };
                  const amount = tx.amount || 0;
                  const direction = (tx.direction || "").toLowerCase();
                  const isIncome = direction === "credit";
                  return (
                    <div key={tx.id} style={{
                      background: "#fff", borderRadius: 16, padding: "16px 20px",
                      boxShadow: "0 2px 8px rgba(0,0,0,0.04)", display: "flex",
                      alignItems: "center", gap: 12,
                    }}>
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
            {withdrawRequests.length === 0 ? (
              <EmptyState icon="🏦" text="Chưa có yêu cầu rút tiền nào" />
            ) : (
              <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
                {withdrawRequests.map((wr) => {
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
                        {wr.userNote && <div>📝 {wr.userNote}</div>}
                        {wr.adminNote && <div style={{ color: "#dc2626" }}>⚠️ Admin: {wr.adminNote}</div>}
                        <div style={{ fontSize: 11, color: "#cbd5e1", marginTop: 2 }}>{toLocal(wr.createdAt)}</div>
                      </div>
                    </div>
                  );
                })}
              </div>
            )}
          </>
        )}
      </div>
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
