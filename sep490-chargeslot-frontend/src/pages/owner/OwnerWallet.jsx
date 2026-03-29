import { useState, useEffect } from "react";
import { useNavigate } from "react-router-dom";
import { walletApi, payoutApi, bankAccountApi } from "@/services/api";
import { showToast } from "@/components/Toast";
import { showConfirm } from "@/components/ConfirmDialog";

const txTypeLabels = {
  TopUp: { label: "Nạp tiền", icon: "💰", color: "#22c55e" },
  BookingPayment: { label: "Thanh toán booking", icon: "💳", color: "#3b82f6" },
  BookingCancel: { label: "Hoàn tiền hủy booking", icon: "↩️", color: "#f59e0b" },
  Payment: { label: "Thanh toán", icon: "💳", color: "#3b82f6" },
  Refund: { label: "Hoàn tiền", icon: "↩️", color: "#f59e0b" },
  Withdraw: { label: "Rút tiền", icon: "🏦", color: "#ef4444" },
  Earning: { label: "Thu nhập", icon: "📈", color: "#22c55e" },
  OwnerPayout: { label: "Nhận thanh toán", icon: "💰", color: "#22c55e" },
  ChargingPayout: { label: "Thanh toán phiên sạc", icon: "⚡", color: "#22c55e" },
};

const payoutStatusLabels = {
  Pending: { label: "Chờ duyệt", color: "#f59e0b", bg: "#fffbeb" },
  Approved: { label: "Đã duyệt", color: "#22c55e", bg: "#f0fdf4" },
  Rejected: { label: "Từ chối", color: "#ef4444", bg: "#fef2f2" },
  Processing: { label: "Đang xử lý", color: "#3b82f6", bg: "#eff6ff" },
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
  const [payouts, setPayouts] = useState([]);
  const [bankAccounts, setBankAccounts] = useState([]);
  const [loading, setLoading] = useState(true);
  const [activeTab, setActiveTab] = useState("transactions");

  // Payout form
  const [showPayout, setShowPayout] = useState(false);
  const [payoutForm, setPayoutForm] = useState({ amount: "", bankAccountId: "", note: "" });
  const [payoutLoading, setPayoutLoading] = useState(false);

  // Add bank account form
  const [showAddBank, setShowAddBank] = useState(false);
  const [bankForm, setBankForm] = useState({ bankName: "", bankAccountNumber: "", bankAccountHolder: "", isDefault: false });
  const [bankLoading, setBankLoading] = useState(false);

  function fetchAll() {
    Promise.all([
      walletApi.getWallet().catch(() => null),
      walletApi.getTransactions().catch(() => []),
      payoutApi.getAll().catch(() => []),
      bankAccountApi.getAll().catch(() => []),
    ]).then(([w, txs, pts, bas]) => {
      setWallet(w);
      setTransactions(Array.isArray(txs) ? txs : []);
      setPayouts(Array.isArray(pts) ? pts : []);
      setBankAccounts(Array.isArray(bas) ? bas : []);
    }).finally(() => setLoading(false));
  }

  useEffect(() => { fetchAll(); }, []);

  async function handlePayout() {
    const amt = Number(payoutForm.amount);
    if (!amt || amt < 10000) {
      showToast.error("Số tiền rút tối thiểu là 10.000đ");
      return;
    }
    if (!payoutForm.bankAccountId) {
      showToast.error("Vui lòng chọn tài khoản ngân hàng");
      return;
    }
    setPayoutLoading(true);
    try {
      await payoutApi.create({
        amount: amt,
        bankAccountId: Number(payoutForm.bankAccountId),
        note: payoutForm.note,
      });
      showToast.success("Yêu cầu rút tiền đã gửi! Chờ admin duyệt.");
      setShowPayout(false);
      setPayoutForm({ amount: "", bankAccountId: "", note: "" });
      fetchAll();
    } catch (err) {
      showToast.error(err.message || "Lỗi tạo yêu cầu rút tiền");
    } finally {
      setPayoutLoading(false);
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
            <WalletBtn onClick={() => { setShowPayout(true); setShowAddBank(false); }}>🏦 Rút tiền</WalletBtn>
            <WalletBtn onClick={() => navigate("/owner/booking-requests")}>📋 Quản lý Booking</WalletBtn>
            <WalletBtn onClick={() => navigate("/stations")}>⚡ Quản lý trạm</WalletBtn>
          </div>
        </div>

        {/* Payout form */}
        {showPayout && (
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
                  onClick={() => { setShowAddBank(true); setShowPayout(false); }}
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
                      value={payoutForm.bankAccountId}
                      onChange={e => setPayoutForm(f => ({ ...f, bankAccountId: e.target.value }))}
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
                    value={payoutForm.amount} onChange={v => setPayoutForm(f => ({ ...f, amount: v }))} />
                  <FormInput label="Ghi chú (tùy chọn)" placeholder="VD: Rút tiền tháng 3"
                    value={payoutForm.note} onChange={v => setPayoutForm(f => ({ ...f, note: v }))} />
                </div>
                <div style={{ display: "flex", gap: 10, marginTop: 16 }}>
                  <ActionBtn onClick={handlePayout} disabled={payoutLoading} bg="linear-gradient(135deg, #22c55e, #16a34a)">
                    {payoutLoading ? "Đang xử lý..." : "Gửi yêu cầu rút tiền"}
                  </ActionBtn>
                  <button onClick={() => setShowPayout(false)}
                    style={{ padding: "12px 20px", borderRadius: 12, border: "1.5px solid #e5e7eb", background: "#fff", color: "#64748b", fontWeight: 600, cursor: "pointer" }}>
                    Hủy
                  </button>
                </div>
                <button
                  onClick={() => { setShowAddBank(true); setShowPayout(false); }}
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
              <FormInput label="Tên ngân hàng" placeholder="VD: Vietcombank"
                value={bankForm.bankName} onChange={v => setBankForm(f => ({ ...f, bankName: v }))} />
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
            { key: "payouts", label: `Rút tiền (${payouts.length})` },
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
              </div>
            )}
          </>
        )}

        {/* Payout history */}
        {activeTab === "payouts" && (
          <>
            <h2 style={{ fontSize: 20, fontWeight: 800, color: "#1e293b", marginBottom: 16 }}>Lịch sử rút tiền</h2>
            {payouts.length === 0 ? (
              <EmptyState icon="🏦" text="Chưa có yêu cầu rút tiền nào" />
            ) : (
              <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
                {payouts.map((p) => {
                  const st = payoutStatusLabels[p.status] || payoutStatusLabels.Pending;
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
                        {p.note && <div>📝 {p.note}</div>}
                        {p.adminNote && <div style={{ color: "#dc2626" }}>⚠️ Admin: {p.adminNote}</div>}
                        <div style={{ fontSize: 11, color: "#cbd5e1", marginTop: 4 }}>{toLocal(p.createdAt)}</div>
                      </div>
                    </div>
                  );
                })}
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
