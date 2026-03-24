import { useState, useEffect } from "react";
import { useNavigate } from "react-router-dom";
import { walletApi } from "@/services/api";

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

const toLocal = (dt) => {
  if (!dt) return "";
  const s = String(dt);
  return new Date(s.endsWith("Z") ? s : s + "Z").toLocaleString("vi-VN");
};

export default function OwnerWallet() {
  const navigate = useNavigate();
  const [wallet, setWallet] = useState(null);
  const [transactions, setTransactions] = useState([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    Promise.all([
      walletApi.getWallet().catch(() => null),
      walletApi.getTransactions().catch(() => []),
    ]).then(([w, txs]) => {
      setWallet(w);
      setTransactions(Array.isArray(txs) ? txs : []);
    }).finally(() => setLoading(false));
  }, []);

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
          <div style={{ display: "flex", gap: 10, marginTop: 20 }}>
            <button
              onClick={() => navigate("/owner/booking-requests")}
              style={{
                padding: "10px 20px", borderRadius: 12, border: "2px solid rgba(255,255,255,0.4)",
                background: "rgba(255,255,255,0.15)", color: "#fff", fontWeight: 600,
                fontSize: 13, cursor: "pointer", backdropFilter: "blur(4px)",
              }}
            >
              📋 Quản lý Booking
            </button>
            <button
              onClick={() => navigate("/stations")}
              style={{
                padding: "10px 20px", borderRadius: 12, border: "2px solid rgba(255,255,255,0.4)",
                background: "rgba(255,255,255,0.15)", color: "#fff", fontWeight: 600,
                fontSize: 13, cursor: "pointer", backdropFilter: "blur(4px)",
              }}
            >
              ⚡ Quản lý trạm
            </button>
          </div>
        </div>

        {/* Transaction history */}
        <h2 style={{ fontSize: 20, fontWeight: 800, color: "#1e293b", marginBottom: 16 }}>
          Lịch sử giao dịch
        </h2>
        {transactions.length === 0 ? (
          <div style={{
            textAlign: "center", padding: 40, background: "#fff", borderRadius: 16,
            boxShadow: "0 2px 8px rgba(0,0,0,0.04)",
          }}>
            <div style={{ fontSize: 48, marginBottom: 8 }}>📋</div>
            <p style={{ color: "#6b7280" }}>Chưa có giao dịch nào</p>
          </div>
        ) : (
          <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
            {transactions.map((tx) => {
              const txType = tx.type || tx.transactionType || "";
              const typeInfo = txTypeLabels[txType] || { label: txType, icon: "📄", color: "#6b7280" };
              const amount = tx.amount || 0;
              // Use Direction from BE: Credit = tiền vào, Debit = tiền ra
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
      </div>
    </div>
  );
}
