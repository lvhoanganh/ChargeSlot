import { useMemo, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { adminFinanceApi } from "@/services/api";
import Pagination from "@/components/Pagination";

// System wallets mapping
const SYSTEM_WALLETS = {
  1: { code: "ESCROW", label: "Giữ Tiền Escrow", desc: "Giữ tiền booking đang hoạt động", icon: <svg width="24" height="24" fill="none" stroke="currentColor" strokeWidth="2" viewBox="0 0 24 24"><path strokeLinecap="round" strokeLinejoin="round" d="M12 15v2m-6 4h12a2 2 0 002-2v-6a2 2 0 00-2-2H6a2 2 0 00-2 2v6a2 2 0 002 2zm10-10V7a4 4 0 00-8 0v4h8z" /></svg>, color: "#f97316", bg: "linear-gradient(135deg, #fff7ed 0%, #ffedd5 100%)", border: "#fed7aa", accent: "#ea580c" },
  2: { code: "PLATFORM_REVENUE", label: "Doanh Thu Sàn", desc: "Lợi nhuận sàn (5% phí giao dịch)", icon: <svg width="24" height="24" fill="none" stroke="currentColor" strokeWidth="2" viewBox="0 0 24 24"><path strokeLinecap="round" strokeLinejoin="round" d="M13 7h8m0 0v8m0-8l-8 8-4-4-6 6" /></svg>, color: "#8b5cf6", bg: "linear-gradient(135deg, #faf5ff 0%, #ede9fe 100%)", border: "#ddd6fe", accent: "#7c3aed" },
  3: { code: "CLEARING", label: "Cổng Thanh Toán", desc: "Gateway SePay", icon: <svg width="24" height="24" fill="none" stroke="currentColor" strokeWidth="2" viewBox="0 0 24 24"><path strokeLinecap="round" strokeLinejoin="round" d="M3 10h18M7 15h1m4 0h1m-7 4h12a3 3 0 003-3V8a3 3 0 00-3-3H6a3 3 0 00-3 3v8a3 3 0 003 3z" /></svg>, color: "#0ea5e9", bg: "linear-gradient(135deg, #f0f9ff 0%, #e0f2fe 100%)", border: "#bae6fd", accent: "#0284c7" },
  99: { code: "TAX_HOLD", label: "Giữ Thuế GTGT", desc: "Thuế 8% giữ hộ cơ quan thuế", icon: <svg width="24" height="24" fill="none" stroke="currentColor" strokeWidth="2" viewBox="0 0 24 24"><path strokeLinecap="round" strokeLinejoin="round" d="M9 14l6-6m-4 0h.01M15 14h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" /></svg>, color: "#10b981", bg: "linear-gradient(135deg, #f0fdf4 0%, #dcfce7 100%)", border: "#bbf7d0", accent: "#059669" },
};

const SYSTEM_WALLET_BY_CODE = {
  "ESCROW": SYSTEM_WALLETS[1],
  "PLATFORM_REVENUE": SYSTEM_WALLETS[2],
  "CLEARING": SYSTEM_WALLETS[3],
  "TAX_HOLD": SYSTEM_WALLETS[99],
};

function getWalletInfo(wallet) {
  if (wallet.walletType === "System") {
    if (wallet.systemCode) return SYSTEM_WALLET_BY_CODE[wallet.systemCode] || SYSTEM_WALLETS[wallet.id];
    return SYSTEM_WALLETS[wallet.id];
  }
  return null;
}

function formatDate(dateStr) {
  if (!dateStr) return "—";
  const d = new Date(String(dateStr).replace("Z", ""));
  return `${String(d.getDate()).padStart(2, "0")}/${String(d.getMonth() + 1).padStart(2, "0")}/${d.getFullYear()} ${String(d.getHours()).padStart(2, "0")}:${String(d.getMinutes()).padStart(2, "0")}`;
}

function formatCurrency(value) {
  if (!value && value !== 0) return "0 đ";
  return `${(value || 0).toLocaleString("vi-VN")} đ`;
}

// Dịch loại giao dịch từ BE sang tiếng Việt
function formatTxType(type) {
  const map = {
    BookingPayment: "Thanh toán đặt lịch",
    BookingRefund: "Hoàn tiền đặt lịch",
    BookingCancel: "Hoàn tiền hủy lịch",
    BookingCancelCompensation: "Bồi thường hủy lịch",
    BookingFallbackDeposit: "Nạp ví tự động (SePay Fallback)",
    BookingPenalty: "Phí phạt hủy lịch",
    PlatformFeeDeduction: "Phí nền tảng (5%)",
    PlatformFee: "Phí nền tảng",
    OwnerPayout: "Thanh toán cho chủ trạm",
    Settlement: "Quyết toán cho chủ trạm",
    AutoSettlement: "Tự động quyết toán",
    AutoComplete: "Tự động hoàn thành phiên",
    WithdrawRequest: "Yêu cầu rút tiền",
    WithdrawalRequest: "Yêu cầu rút tiền",
    WithdrawApproved: "Duyệt rút tiền",
    WithdrawalApproved: "Duyệt rút tiền",
    WithdrawCompleted: "Rút tiền thành công",
    WithdrawalCompleted: "Rút tiền thành công",
    WithdrawRejected: "Từ chối rút tiền",
    WithdrawalRejected: "Từ chối rút tiền",
    WithdrawRefund: "Hoàn tiền rút thất bại",
    WithdrawalRefund: "Hoàn tiền rút thất bại",
    VatCollection: "Thu thuế VAT (8%)",
    VatHold: "Giữ thuế VAT",
    TaxHold: "Giữ thuế VAT",
    EscrowRelease: "Giải phóng tiền giữ",
    EscrowLock: "Khóa tiền Escrow",
    Deposit: "Nạp tiền",
    TopUp: "Nạp tiền vào ví",
    DisputeRefund: "Hoàn tiền khiếu nại",
    DisputePayout: "Thanh toán khiếu nại",
    TransferCompleted: "Chuyển khoản thành công",
    LoyaltyReward: "Thưởng điểm tích lũy",
    Credit: "Tiền vào",
    Debit: "Tiền ra",
  };
  return map[type] || type || "—";
}

//  Transaction Detail Modal 
function TransactionDetailModal({ transactionId, onClose }) {
  const { data: tx, isLoading, error } = useQuery({
    queryKey: ["admin-transaction-detail", transactionId],
    queryFn: () => adminFinanceApi.getTransactionDetail(transactionId),
    enabled: !!transactionId,
  });

  const debitTotal = tx?.entries?.filter((e) => e.direction === "Debit").reduce((s, e) => s + (e.amount || 0), 0) || 0;
  const creditTotal = tx?.entries?.filter((e) => e.direction === "Credit").reduce((s, e) => s + (e.amount || 0), 0) || 0;
  const isBalanced = Math.abs(debitTotal - creditTotal) < 0.01;

  return (
    <>
      <div className="csw-overlay" style={{ zIndex: 12000 }} onClick={onClose} />
      <div className="csw-modal" style={{ zIndex: 12001 }}>
        <div className="csw-modal__header">
          <div className="csw-modal__header-left">
            <span className="csw-modal__icon">
              <svg width="20" height="20" fill="none" stroke="currentColor" strokeWidth="2" viewBox="0 0 24 24"><path strokeLinecap="round" strokeLinejoin="round" d="M12 6.253v13m0-13C10.832 5.477 9.246 5 7.5 5S4.168 5.477 3 6.253v13C4.168 18.477 5.754 18 7.5 18s3.332.477 4.5 1.253m0-13C13.168 5.477 14.754 5 16.5 5c1.747 0 3.332.477 4.5 1.253v13C19.832 18.477 18.247 18 16.5 18c-1.746 0-3.332.477-4.5 1.253" /></svg>
            </span>
            <div>
              <h2 className="csw-modal__title">Chi Tiết Sổ Cái</h2>
              {tx && <p className="csw-modal__subtitle">#{tx.id} — {formatTxType(tx.referenceType)}</p>}
            </div>
          </div>
          <button onClick={onClose} className="csw-icon-btn">✕</button>
        </div>

        <div className="csw-modal__body">
          {isLoading ? (
            <div className="csw-center-state"><div className="csw-spinner" /><p>Đang tải chi tiết...</p></div>
          ) : error ? (
            <div className="csw-center-state csw-center-state--error"> Lỗi tải chi tiết giao dịch!</div>
          ) : !tx ? (
            <div className="csw-center-state">Không tìm thấy giao dịch.</div>
          ) : (
            <div>
              {/* Meta info */}
              <div className="csw-meta-grid">
                <div className="csw-meta-item">
                  <span className="csw-meta-label">Mã Giao Dịch</span>
                  <span className="csw-meta-value csw-mono">#{tx.id}</span>
                </div>
                <div className="csw-meta-item">
                  <span className="csw-meta-label">Loại</span>
                  <span className="csw-badge csw-badge--blue">{formatTxType(tx.referenceType)}</span>
                </div>
                <div className="csw-meta-item">
                  <span className="csw-meta-label">Tham chiếu</span>
                  <span className="csw-meta-value csw-mono">#{tx.referenceId}</span>
                </div>
                <div className="csw-meta-item">
                  <span className="csw-meta-label">Ngày / Giờ</span>
                  <span className="csw-meta-value">{formatDate(tx.createdAt)}</span>
                </div>
                {tx.memo && (
                  <div className="csw-meta-item csw-meta-item--full">
                    <span className="csw-meta-label">Mô tả</span>
                    <span className="csw-meta-value">{tx.memo}</span>
                  </div>
                )}
              </div>

              {/* Balance check */}
              {/* <div className={`csw-balance-check ${isBalanced ? "csw-balance-check--ok" : "csw-balance-check--err"}`}>
                <span className="csw-balance-check__icon">{isBalanced ? "✓" : "⚠️"}</span>
                <div>
                  <div className="csw-balance-check__title">{isBalanced ? "Sổ Cái Cân Bằng" : "SỔ CÁI LỆCH — Kiểm tra ngay!"}</div>
                  <div className="csw-balance-check__detail">Ghi Nợ: {formatCurrency(debitTotal)} &nbsp;|&nbsp; Ghi Có: {formatCurrency(creditTotal)}</div>
                </div>
              </div> */}

              {/* Double-entry */}
              <h3 className="csw-section-title"> Bút Toán Kép</h3>
              <div className="csw-entry-grid">
                <div className="csw-entry-col csw-entry-col--debit">
                  <div className="csw-entry-col__header"> Ghi Nợ (Chi Ra)</div>
                  {tx.entries?.filter((e) => e.direction === "Debit").map((entry, idx) => (
                    <div key={idx} className="csw-entry-card csw-entry-card--debit">
                      <div className="csw-entry-card__name">
                        {entry.walletType === "System" ? ` ${entry.ownerName}` : ` ${entry.ownerName}`}
                      </div>
                      <div className="csw-entry-card__amount csw-entry-card__amount--debit">
                        −{formatCurrency(entry.amount)}
                      </div>
                    </div>
                  ))}
                  <div className="csw-entry-total csw-entry-total--debit">Tổng: {formatCurrency(debitTotal)}</div>
                </div>

                <div className="csw-entry-divider">
                  <span>⇄</span>
                </div>

                <div className="csw-entry-col csw-entry-col--credit">
                  <div className="csw-entry-col__header"> Ghi Có (Tiền Vào)</div>
                  {tx.entries?.filter((e) => e.direction === "Credit").map((entry, idx) => (
                    <div key={idx} className="csw-entry-card csw-entry-card--credit">
                      <div className="csw-entry-card__name">
                        {entry.walletType === "System" ? ` ${entry.ownerName}` : ` ${entry.ownerName}`}
                      </div>
                      <div className="csw-entry-card__amount csw-entry-card__amount--credit">
                        +{formatCurrency(entry.amount)}
                      </div>
                    </div>
                  ))}
                  <div className="csw-entry-total csw-entry-total--credit">Tổng: {formatCurrency(creditTotal)}</div>
                </div>
              </div>
            </div>
          )}
        </div>
      </div>
    </>
  );
}

//  Wallet Transactions Drawer 
function WalletTransactionsDrawer({ walletId, walletLabel, onClose }) {
  const [page, setPage] = useState(1);
  const [txTypeFilter, setTxTypeFilter] = useState("ALL");
  const [fromDate, setFromDate] = useState("");
  const [toDate, setToDate] = useState("");
  const [detailTxId, setDetailTxId] = useState(null);
  const pageSize = 20;

  const { data: rawData, isLoading, error } = useQuery({
    queryKey: ["admin-wallet-transactions", walletId, page, txTypeFilter, fromDate, toDate],
    queryFn: () => {
      const filter = { page, pageSize };
      if (txTypeFilter !== "ALL") filter.transactionType = txTypeFilter;
      if (fromDate) filter.fromDate = fromDate;
      if (toDate) filter.toDate = toDate;
      return adminFinanceApi.getWalletTransactions(walletId, filter);
    },
    enabled: !!walletId,
  });

  const txs = rawData?.items ?? [];
  const totalCount = rawData?.totalCount ?? 0;

  return (
    <>
      <div className="csw-overlay" onClick={onClose} />
      <div className="csw-drawer">
        {/* Header */}
        <div className="csw-drawer__header">
          <div className="csw-drawer__header-left">
            <div className="csw-drawer__icon-wrap">
              <svg width="20" height="20" fill="none" stroke="#ea580c" strokeWidth="2" viewBox="0 0 24 24"><path strokeLinecap="round" strokeLinejoin="round" d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" /></svg>
            </div>
            <div>
              <h2 className="csw-drawer__title">Lịch Sử Giao Dịch</h2>
              <p className="csw-drawer__subtitle">{walletLabel || `Ví #${walletId}`}</p>
            </div>
          </div>
          <button onClick={onClose} className="csw-icon-btn">✕</button>
        </div>

        {/* Filters */}
        <div className="csw-drawer__filters">
          <div className="csw-filter-item">
            <label className="csw-filter-label">Chiều GD</label>
            <select
              value={txTypeFilter}
              onChange={(e) => { setTxTypeFilter(e.target.value); setPage(1); }}
              className="csw-select"
            >
              <option value="ALL">Tất cả</option>
              <option value="Credit"> Tiền vào (Credit)</option>
              <option value="Debit"> Tiền ra (Debit)</option>
            </select>
          </div>
          <div className="csw-filter-item">
            <label className="csw-filter-label">Từ ngày</label>
            <input type="date" value={fromDate} onChange={(e) => { setFromDate(e.target.value); setPage(1); }} className="csw-input" />
          </div>
          <div className="csw-filter-item">
            <label className="csw-filter-label">Đến ngày</label>
            <input type="date" value={toDate} onChange={(e) => { setToDate(e.target.value); setPage(1); }} className="csw-input" />
          </div>
          <button
            onClick={() => { setTxTypeFilter("ALL"); setFromDate(""); setToDate(""); setPage(1); }}
            className="csw-reset-btn"
          >
            ↺ Đặt lại
          </button>
        </div>

        {/* Content */}
        <div className="csw-drawer__body">
          {isLoading ? (
            <div className="csw-center-state"><div className="csw-spinner" /><p>Đang tải giao dịch...</p></div>
          ) : error ? (
            <div className="csw-center-state csw-center-state--error"> Lỗi tải giao dịch!</div>
          ) : txs.length === 0 ? (
            <div className="csw-center-state">
              <span style={{ fontSize: 36 }}>📄</span>
              <p>Ví này chưa có giao dịch nào.</p>
            </div>
          ) : (
            <>
              <div className="csw-tx-list">
                {txs.map((tx) => (
                  <div key={tx.id} className={`csw-tx-item ${tx.direction === "Credit" ? "csw-tx-item--credit" : "csw-tx-item--debit"}`}>
                    <div className={`csw-tx-item__icon ${tx.direction === "Credit" ? "csw-tx-item__icon--credit" : "csw-tx-item__icon--debit"}`}>
                      {tx.direction === "Credit" ? "↓" : "↑"}
                    </div>
                    <div className="csw-tx-item__info">
                      <div className="csw-tx-item__top">
                        <span className="csw-tx-item__type">{formatTxType(tx.type || tx.transactionType)}</span>
                        <span className={`csw-tx-item__amount ${tx.direction === "Credit" ? "csw-tx-item__amount--credit" : "csw-tx-item__amount--debit"}`}>
                          {tx.direction === "Credit" ? "+" : "−"}{formatCurrency(tx.amount)}
                        </span>
                      </div>
                      <div className="csw-tx-item__bottom">
                        <span className="csw-tx-item__memo">{tx.memo || "—"}</span>
                        <span className="csw-tx-item__date">{formatDate(tx.createdAt)}</span>
                      </div>
                    </div>
                    <button
                      onClick={() => setDetailTxId(tx.id)}
                      className="csw-detail-btn"
                      title="Xem sổ cái chi tiết"
                    >
                      👁
                    </button>
                  </div>
                ))}
              </div>
              <div className="csw-drawer__pagination">
                <Pagination page={page} totalCount={totalCount} pageSize={pageSize} onPageChange={(p) => setPage(p)} />
              </div>
            </>
          )}
        </div>
      </div>

      {detailTxId && (
        <TransactionDetailModal transactionId={detailTxId} onClose={() => setDetailTxId(null)} />
      )}
    </>
  );
}

//  System Wallet Card 
function SystemWalletCard({ wallet, onViewTransactions }) {
  const info = getWalletInfo(wallet);
  const totalBalance = (wallet.availableBalance || 0) + (wallet.frozenBalance || 0);

  return (
    <div
      className="csw-sys-card"
      style={{ background: info?.bg, borderColor: info?.border }}
    >
      <div className="csw-sys-card__top">
        <div className="csw-sys-card__icon" style={{ background: info?.color + "20", color: info?.color }}>
          {info?.icon || ""}
        </div>
        <div className="csw-sys-card__info">
          <h3 className="csw-sys-card__title" style={{ color: info?.accent }}>{info?.label || `Ví #${wallet.id}`}</h3>
          <p className="csw-sys-card__desc">{info?.desc}</p>
        </div>
      </div>

      <div className="csw-sys-card__balances">
        <div className="csw-sys-card__balance-row">
          <span className="csw-sys-card__bal-label">Khả dụng</span>
          <span className="csw-sys-card__bal-value" style={{ color: info?.accent }}>
            {formatCurrency(wallet.availableBalance || 0)}
          </span>
        </div>
        <div className="csw-sys-card__balance-row">
          <span className="csw-sys-card__bal-label">Đóng băng</span>
          <span className="csw-sys-card__bal-value csw-sys-card__bal-value--frozen">
            {formatCurrency(wallet.frozenBalance || 0)}
          </span>
        </div>
        <div className="csw-sys-card__divider" />
        <div className="csw-sys-card__balance-row">
          <span className="csw-sys-card__bal-label csw-sys-card__bal-label--total">Tổng cộng</span>
          <span className="csw-sys-card__bal-total" style={{ color: info?.color }}>
            {formatCurrency(totalBalance)}
          </span>
        </div>
      </div>

      <button
        onClick={() => onViewTransactions(wallet.id, info?.label)}
        className="csw-sys-card__btn"
        style={{ background: info?.color, "--hover-color": info?.accent }}
      >
        Xem Giao Dịch →
      </button>
    </div>
  );
}

//  Main Page 
export default function AdminWallets() {
  const [search, setSearch] = useState("");
  const [typeFilter, setTypeFilter] = useState("ALL");
  const [page, setPage] = useState(1);
  const [fromDate, setFromDate] = useState("");
  const [toDate, setToDate] = useState("");
  const pageSize = 20;
  const [selectedWallet, setSelectedWallet] = useState(null); // { id, label }

  const { data: rawData, isLoading, error } = useQuery({
    queryKey: ["admin-fin-wallets", typeFilter, page, fromDate, toDate],
    queryFn: () => {
      const filter = { page, pageSize };
      if (typeFilter !== "ALL") filter.walletType = typeFilter;
      if (fromDate) filter.fromDate = fromDate;
      if (toDate) filter.toDate = toDate;
      return adminFinanceApi.getWallets(filter);
    },
    refetchInterval: 30000,
  });

  const { data: systemWalletsData } = useQuery({
    queryKey: ["admin-system-wallets"],
    queryFn: () => adminFinanceApi.getWallets({ walletType: "System", pageSize: 100 }),
    refetchInterval: 30000,
  });

  const wallets = rawData?.items ?? [];
  const totalCount = rawData?.totalCount ?? 0;

  const systemWallets = useMemo(() => {
    // Merge từ cả 2 nguồn: query riêng system + query chính (phòng trường hợp 1 trong 2 chưa load)
    const sysItems = systemWalletsData?.items ?? [];
    const mainItems = (rawData?.items ?? []).filter(w => w.walletType === "System");
    const all = [...sysItems, ...mainItems];
    // Deduplicate theo id
    const map = new Map();
    all.forEach(w => { if (w && !map.has(w.id)) map.set(w.id, w); });
    const unique = [...map.values()];
    return [
      unique.find((w) => w.systemCode === "ESCROW"),
      unique.find((w) => w.systemCode === "PLATFORM_REVENUE"),
      unique.find((w) => w.systemCode === "CLEARING"),
      unique.find((w) => w.systemCode === "TAX_HOLD"),
    ].filter(Boolean);
  }, [systemWalletsData, rawData]);

  const filtered = useMemo(() => {
    const normalize = (s) => !s ? "" : String(s).normalize("NFD").replace(/[\u0300-\u036f]/g, "").replace(/đ/g, "d").replace(/Đ/g, "D").toLowerCase();
    const kw = normalize(search.trim());
    if (!kw) return wallets;
    return wallets.filter((w) => {
      const typeLabel = w.walletType === "System" ? "he thong" : w.walletType === "Owner" ? "chu tram" : "tai xe";
      return String(w.id).includes(kw) || normalize(typeLabel).includes(kw) || normalize(w.ownerName || "").includes(kw);
    });
  }, [wallets, search]);

  const handleViewTransactions = (id, label) => setSelectedWallet({ id, label });

  if (isLoading) return (
    <div className="csw-page">
      <div className="csw-center-state" style={{ paddingTop: 120 }}>
        <div className="csw-spinner" />
        <p>Đang tải danh sách ví...</p>
      </div>
      <style>{pageStyles}</style>
    </div>
  );

  if (error) return (
    <div className="csw-page">
      <div className="csw-center-state csw-center-state--error" style={{ paddingTop: 120 }}>
        Lỗi tải dữ liệu: {error.message}
      </div>
      <style>{pageStyles}</style>
    </div>
  );

  return (
    <div className="csw-page">
      {/* Header */}
      <div className="csw-page__header">
        <div className="csw-page__header-left">
          <h1 className="csw-page__title"> Giám Sát Vốn & Ví Hệ Thống</h1>
          <p className="csw-page__subtitle">Theo dõi dòng tiền toàn hệ thống — Giữ Tiền · Doanh Thu Sàn · Cổng Thanh Toán · Giữ Thuế</p>
        </div>
        <div className="csw-page__header-badge">
          <span className="csw-live-dot" />
          Trực tiếp · cập nhật mỗi 30s
        </div>
      </div>

      {/* System Wallet Cards */}
      {systemWallets.length > 0 && (
        <div className="csw-sys-cards-grid">
          {systemWallets.map((w) => (
            <SystemWalletCard key={w.id} wallet={w} onViewTransactions={handleViewTransactions} />
          ))}
        </div>
      )}

      {/* Filter Bar */}
      <div className="csw-filterbar">
        <div className="csw-filterbar__search">
          <svg width="16" height="16" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
          </svg>
          <input
            type="text"
            value={search}
            onChange={(e) => { setSearch(e.target.value); setPage(1); }}
            placeholder="Tìm mã ví, tên người dùng..."
            className="csw-filterbar__input"
          />
        </div>
        <select
          value={typeFilter}
          onChange={(e) => { setTypeFilter(e.target.value); setPage(1); }}
          className="csw-select"
        >
          <option value="ALL">Tất cả loại Ví</option>
          <option value="System"> Ví Hệ Thống</option>
          <option value="Owner"> Ví Chủ Trạm</option>
          <option value="Driver"> Ví Tài Xế</option>
        </select>
        <div className="csw-date-range">
          <div className="csw-filter-item">
            <label className="csw-filter-label">Từ ngày</label>
            <input type="date" value={fromDate} onChange={(e) => { setFromDate(e.target.value); setPage(1); }} className="csw-input" />
          </div>
          <div className="csw-filter-item">
            <label className="csw-filter-label">Đến ngày</label>
            <input type="date" value={toDate} onChange={(e) => { setToDate(e.target.value); setPage(1); }} className="csw-input" />
          </div>
        </div>
        <button onClick={() => { setSearch(""); setTypeFilter("ALL"); setFromDate(""); setToDate(""); setPage(1); }} className="csw-reset-btn">
          ↺ Đặt lại
        </button>
      </div>

      {/* Wallets Table */}
      <div className="csw-table-wrap">
        <table className="csw-table">
          <thead>
            <tr>
              <th>STT</th>
              <th>Chủ sở hữu</th>
              <th>Loại Ví</th>
              <th>Số Dư Khả Dụng</th>
              <th>Đông Lạnh</th>
              <th>Ngày Tạo</th>
              <th style={{ textAlign: "center" }}>Hành Động</th>
            </tr>
          </thead>
          <tbody>
            {filtered.length === 0 ? (
              <tr>
                <td colSpan={7} className="csw-table__empty">
                  <span>📄</span>
                  <p>Không tìm thấy ví nào khớp.</p>
                </td>
              </tr>
            ) : (
              filtered.map((w, idx) => {
                const info = getWalletInfo(w);
                return (
                  <tr key={w.id}>
                    <td>
                      <span className="csw-table__id">{(page - 1) * pageSize + idx + 1}</span>
                    </td>
                    <td className="csw-table__owner">
                      {w.walletType === "System" ? (
                        <><span className="csw-table__avatar csw-table__avatar--system">⚙️</span><span className="csw-table__owner-name">Hệ Thống {w.systemCode ? `· ${w.systemCode}` : ""}</span></>
                      ) : w.walletType === "Owner" ? (
                        <><span className="csw-table__avatar csw-table__avatar--owner">🏢</span><span className="csw-table__owner-name">{w.ownerName || "Chủ Trạm"}</span></>
                      ) : (
                        <><span className="csw-table__avatar csw-table__avatar--driver">🧑</span><span className="csw-table__owner-name">{w.ownerName || "Tài Xế"}</span></>
                      )}
                    </td>
                    <td>
                      {w.walletType === "System" ? (
                        <span className="csw-badge csw-badge--purple">Hệ Thống</span>
                      ) : w.walletType === "Owner" ? (
                        <span className="csw-badge csw-badge--orange">Chủ Trạm</span>
                      ) : (
                        <span className="csw-badge csw-badge--blue">Tài Xế</span>
                      )}
                    </td>
                    <td className="csw-table__balance csw-table__balance--available">
                      {formatCurrency(w.availableBalance || 0)}
                    </td>
                    <td className="csw-table__balance csw-table__balance--frozen">
                      {formatCurrency(w.frozenBalance || 0)}
                    </td>
                    <td className="csw-table__date">{formatDate(w.createdAt)}</td>
                    <td style={{ textAlign: "center" }}>
                      <button
                        onClick={() => handleViewTransactions(w.id, info?.label || (w.walletType === "Owner" ? `Ví chủ trạm — ${w.ownerName}` : w.walletType === "Driver" ? `Ví tài xế — ${w.ownerName}` : `Ví #${w.id}`))}
                        className="csw-view-btn"
                      >
                        Xem giao dịch
                      </button>
                    </td>
                  </tr>
                );
              })
            )}
          </tbody>
        </table>

        <div className="csw-table__pagination">
          <Pagination
            page={page}
            totalCount={search ? filtered.length : totalCount}
            pageSize={pageSize}
            onPageChange={(p) => setPage(p)}
          />
        </div>
      </div>

      {selectedWallet && (
        <WalletTransactionsDrawer
          walletId={selectedWallet.id}
          walletLabel={selectedWallet.label}
          onClose={() => setSelectedWallet(null)}
        />
      )}

      <style>{pageStyles}</style>
    </div>
  );
}

const pageStyles = `
  /*  Page  */
  .csw-page {
    max-width: 1400px; width: 95%; margin: 0 auto; padding: 88px 0 60px;
    font-family: 'Inter', -apple-system, sans-serif;
  }
  @media (max-width: 768px) { .csw-page { width: 100%; padding: 80px 16px 40px; } }

  /*  Page Header  */
  .csw-page__header {
    display: flex; align-items: flex-start; justify-content: space-between;
    margin-bottom: 32px; flex-wrap: wrap; gap: 12px;
  }
  .csw-page__title { font-size: 28px; font-weight: 800; color: #0f172a; letter-spacing: -0.5px; margin: 0 0 6px; }
  .csw-page__subtitle { font-size: 14px; color: #64748b; margin: 0; }
  .csw-page__header-badge {
    display: inline-flex; align-items: center; gap: 8px;
    background: #f0fdf4; border: 1px solid #bbf7d0;
    color: #15803d; font-size: 13px; font-weight: 600;
    padding: 6px 14px; border-radius: 50px;
  }
  .csw-live-dot {
    width: 8px; height: 8px; border-radius: 50%; background: #22c55e;
    animation: csw-pulse 1.5s infinite;
  }
  @keyframes csw-pulse {
    0%, 100% { opacity: 1; transform: scale(1); }
    50% { opacity: 0.5; transform: scale(0.8); }
  }

  /*  System Wallet Cards  */
  .csw-sys-cards-grid {
    display: grid; grid-template-columns: repeat(auto-fit, minmax(260px, 1fr));
    gap: 20px; margin-bottom: 32px;
  }
  .csw-sys-card {
    border: 1.5px solid; border-radius: 20px; padding: 24px;
    transition: transform 0.2s, box-shadow 0.2s;
  }
  .csw-sys-card:hover { transform: translateY(-3px); box-shadow: 0 12px 32px rgba(0,0,0,0.1); }
  .csw-sys-card__top { display: flex; align-items: center; gap: 14px; margin-bottom: 20px; }
  .csw-sys-card__icon {
    width: 48px; height: 48px; border-radius: 14px;
    display: flex; align-items: center; justify-content: center; font-size: 22px; flex-shrink: 0;
  }
  .csw-sys-card__title { font-size: 15px; font-weight: 700; margin: 0 0 4px; }
  .csw-sys-card__desc { font-size: 12px; color: #64748b; margin: 0; line-height: 1.4; }
  .csw-sys-card__balances { background: rgba(255,255,255,0.6); border-radius: 12px; padding: 14px; margin-bottom: 16px; }
  .csw-sys-card__balance-row { display: flex; justify-content: space-between; align-items: center; padding: 5px 0; }
  .csw-sys-card__bal-label { font-size: 12px; color: #64748b; }
  .csw-sys-card__bal-label--total { font-size: 13px; font-weight: 700; color: #1e293b; }
  .csw-sys-card__bal-value { font-size: 14px; font-weight: 700; }
  .csw-sys-card__bal-value--frozen { color: #f97316; }
  .csw-sys-card__divider { height: 1px; background: rgba(0,0,0,0.08); margin: 8px 0; }
  .csw-sys-card__bal-total { font-size: 18px; font-weight: 800; }
  .csw-sys-card__btn {
    width: 100%; padding: 11px; border: none; border-radius: 12px; color: white;
    font-size: 13px; font-weight: 700; cursor: pointer; transition: all 0.2s;
    letter-spacing: 0.2px;
  }
  .csw-sys-card__btn:hover { opacity: 0.88; transform: translateY(-1px); box-shadow: 0 4px 12px rgba(0,0,0,0.15); }

  /*  Filter Bar  */
  .csw-filterbar {
    background: white; border: 1px solid #e2e8f0; border-radius: 16px;
    padding: 16px 20px; margin-bottom: 20px;
    display: flex; gap: 12px; align-items: flex-end; flex-wrap: wrap;
    box-shadow: 0 1px 4px rgba(0,0,0,0.04);
  }
  .csw-filterbar__search {
    flex: 1; min-width: 220px; position: relative; display: flex; align-items: center;
  }
  .csw-filterbar__search svg { position: absolute; left: 13px; color: #94a3b8; }
  .csw-filterbar__input {
    width: 100%; height: 42px; border: 1.5px solid #e2e8f0; border-radius: 12px;
    padding: 0 14px 0 38px; font-size: 14px; outline: none; background: #f8fafc;
    transition: all 0.2s; box-sizing: border-box;
  }
  .csw-filterbar__input:focus { border-color: #f97316; background: white; box-shadow: 0 0 0 3px rgba(249,115,22,0.08); }
  .csw-date-range { display: flex; gap: 10px; flex-wrap: wrap; }

  /*  Shared Form Controls  */
  .csw-select {
    height: 42px; border: 1.5px solid #e2e8f0; border-radius: 12px;
    padding: 0 14px; font-size: 14px; background: #f8fafc; cursor: pointer;
    outline: none; transition: border-color 0.2s; color: #374151;
  }
  .csw-select:focus { border-color: #f97316; }
  .csw-input {
    height: 42px; border: 1.5px solid #e2e8f0; border-radius: 12px;
    padding: 0 12px; font-size: 13px; background: #f8fafc; outline: none;
    transition: border-color 0.2s; color: #374151; box-sizing: border-box;
  }
  .csw-input:focus { border-color: #f97316; }
  .csw-filter-item { display: flex; flex-direction: column; gap: 4px; }
  .csw-filter-label { font-size: 11px; font-weight: 700; color: #94a3b8; text-transform: uppercase; letter-spacing: 0.5px; }
  .csw-reset-btn {
    height: 42px; padding: 0 18px; border: 1.5px solid #e2e8f0; border-radius: 12px;
    background: white; font-size: 13px; font-weight: 600; color: #64748b;
    cursor: pointer; transition: all 0.2s; white-space: nowrap;
  }
  .csw-reset-btn:hover { background: #f1f5f9; border-color: #cbd5e1; color: #475569; }

  /*  Table  */
  .csw-table-wrap {
    background: white; border: 1px solid #e2e8f0; border-radius: 20px;
    overflow-x: auto; box-shadow: 0 2px 8px rgba(0,0,0,0.04);
  }
  .csw-table { width: 100%; min-width: 900px; border-collapse: collapse; }
  .csw-table thead { background: linear-gradient(180deg, #f8fafc 0%, #f1f5f9 100%); }
  .csw-table th {
    padding: 14px 20px; font-size: 11px; font-weight: 700; color: #94a3b8;
    text-transform: uppercase; letter-spacing: 0.6px; text-align: left;
    border-bottom: 1px solid #e2e8f0;
  }
  .csw-table td {
    padding: 16px 20px; font-size: 14px; color: #374151; border-bottom: 1px solid #f1f5f9;
    vertical-align: middle;
  }
  .csw-table tbody tr:last-child td { border-bottom: none; }
  .csw-table tbody tr { transition: background 0.15s; }
  .csw-table tbody tr:hover { background: #fefce8; }
  .csw-table__id { font-family: monospace; font-size: 13px; color: #94a3b8; font-weight: 600; }
  .csw-table__owner { display: flex; align-items: center; gap: 10px; }
  .csw-table__avatar {
    width: 34px; height: 34px; border-radius: 10px;
    display: flex; align-items: center; justify-content: center;
    font-size: 16px; flex-shrink: 0;
  }
  .csw-table__avatar--system { background: #f5f3ff; }
  .csw-table__avatar--owner { background: #fffbeb; }
  .csw-table__avatar--driver { background: #eff6ff; }
  .csw-table__owner-name { font-weight: 600; color: #1e293b; font-size: 14px; }
  .csw-table__balance { font-weight: 700; font-size: 14px; }
  .csw-table__balance--available { color: #16a34a; }
  .csw-table__balance--frozen { color: #f97316; }
  .csw-table__date { font-size: 13px; color: #94a3b8; }
  .csw-table__empty { text-align: center; padding: 64px 0 !important; color: #94a3b8; }
  .csw-table__empty span { font-size: 36px; display: block; margin-bottom: 12px; }
  .csw-table__empty p { font-size: 14px; margin: 0; }
  .csw-table__pagination { padding: 20px 24px; border-top: 1px solid #f1f5f9; }

  /*  Badges  */
  .csw-badge {
    display: inline-flex; align-items: center; padding: 4px 12px;
    border-radius: 50px; font-size: 12px; font-weight: 700; white-space: nowrap;
  }
  .csw-badge--purple { background: #f5f3ff; color: #7c3aed; }
  .csw-badge--orange { background: #fffbeb; color: #d97706; }
  .csw-badge--blue { background: #eff6ff; color: #2563eb; }
  .csw-badge--green { background: #f0fdf4; color: #15803d; }

  /*  Buttons  */
  .csw-view-btn {
    padding: 7px 16px; border-radius: 10px;
    background: linear-gradient(135deg, #f97316, #ea580c);
    border: none; color: white; font-size: 12px; font-weight: 700;
    cursor: pointer; transition: all 0.2s;
  }
  .csw-view-btn:hover { opacity: 0.88; transform: translateY(-1px); box-shadow: 0 4px 10px rgba(249,115,22,0.3); }
  .csw-icon-btn {
    width: 36px; height: 36px; border-radius: 10px; border: 1px solid #e2e8f0;
    background: #f8fafc; color: #64748b; font-size: 16px; cursor: pointer;
    display: flex; align-items: center; justify-content: center; transition: all 0.2s;
    flex-shrink: 0;
  }
  .csw-icon-btn:hover { background: #f1f5f9; color: #1e293b; }
  .csw-detail-btn {
    width: 36px; height: 36px; border-radius: 10px; border: 1px solid #e2e8f0;
    background: #f8fafc; cursor: pointer; font-size: 16px; flex-shrink: 0;
    display: flex; align-items: center; justify-content: center; transition: all 0.2s;
  }
  .csw-detail-btn:hover { background: #eff6ff; border-color: #bfdbfe; }

  /*  Overlay  */
  .csw-overlay {
    position: fixed; inset: 0; background: rgba(15, 23, 42, 0.5);
    z-index: 5000; backdrop-filter: blur(3px);
  }

  /*  Drawer  */
  .csw-drawer {
    position: fixed; top: 0; right: 0; bottom: 0; width: 520px;
    background: #f8fafc; z-index: 5001;
    box-shadow: -8px 0 40px rgba(0,0,0,0.12);
    display: flex; flex-direction: column;
    animation: csw-slide-in 0.3s cubic-bezier(0.16, 1, 0.3, 1) forwards;
  }
  @media (max-width: 600px) { .csw-drawer { width: 100%; } }
  @keyframes csw-slide-in { from { transform: translateX(100%); } to { transform: translateX(0); } }

  .csw-drawer__header {
    padding: 20px 24px; border-bottom: 1px solid #e2e8f0;
    display: flex; align-items: center; justify-content: space-between;
    background: white; flex-shrink: 0;
  }
  .csw-drawer__header-left { display: flex; align-items: center; gap: 14px; }
  .csw-drawer__icon-wrap {
    width: 44px; height: 44px; border-radius: 14px; background: linear-gradient(135deg, #fff7ed, #ffedd5);
    display: flex; align-items: center; justify-content: center; font-size: 22px;
  }
  .csw-drawer__title { font-size: 17px; font-weight: 800; color: #0f172a; margin: 0 0 3px; }
  .csw-drawer__subtitle { font-size: 12px; color: #94a3b8; margin: 0; }

  .csw-drawer__filters {
    padding: 16px 20px; background: white; border-bottom: 1px solid #e2e8f0;
    display: flex; gap: 10px; align-items: flex-end; flex-wrap: wrap; flex-shrink: 0;
  }
  .csw-drawer__body { flex: 1; overflow-y: auto; padding: 16px 20px; }
  .csw-drawer__pagination { padding-top: 16px; }

  /*  Transaction List  */
  .csw-tx-list { display: flex; flex-direction: column; gap: 10px; }
  .csw-tx-item {
    display: flex; align-items: center; gap: 14px; padding: 14px 16px;
    background: white; border-radius: 14px; border: 1px solid #e2e8f0;
    transition: all 0.15s;
  }
  .csw-tx-item:hover { border-color: #cbd5e1; box-shadow: 0 2px 8px rgba(0,0,0,0.06); }
  .csw-tx-item--credit { border-left: 3px solid #22c55e; }
  .csw-tx-item--debit { border-left: 3px solid #f43f5e; }
  .csw-tx-item__icon {
    width: 38px; height: 38px; border-radius: 10px;
    display: flex; align-items: center; justify-content: center;
    font-size: 18px; font-weight: 800; flex-shrink: 0;
  }
  .csw-tx-item__icon--credit { background: #f0fdf4; color: #16a34a; }
  .csw-tx-item__icon--debit { background: #fff1f2; color: #e11d48; }
  .csw-tx-item__info { flex: 1; min-width: 0; }
  .csw-tx-item__top { display: flex; justify-content: space-between; align-items: center; margin-bottom: 5px; }
  .csw-tx-item__type { font-size: 13px; font-weight: 700; color: #1e293b; }
  .csw-tx-item__amount { font-size: 15px; font-weight: 800; }
  .csw-tx-item__amount--credit { color: #16a34a; }
  .csw-tx-item__amount--debit { color: #e11d48; }
  .csw-tx-item__bottom { display: flex; gap: 8px; align-items: center; flex-wrap: wrap; }
  .csw-tx-item__id { font-family: monospace; font-size: 11px; color: #94a3b8; background: #f1f5f9; padding: 2px 6px; border-radius: 4px; }
  .csw-tx-item__memo { font-size: 12px; color: #64748b; flex: 1; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; min-width: 0; }
  .csw-tx-item__date { font-size: 11px; color: #94a3b8; white-space: nowrap; flex-shrink: 0; }

  /*  Modal  */
  .csw-modal {
    position: fixed; top: 50%; left: 50%; transform: translate(-50%, -50%);
    width: 90%; max-width: 680px; background: white;
    border-radius: 24px; box-shadow: 0 24px 64px rgba(0,0,0,0.2);
    overflow: hidden; max-height: 92vh; display: flex; flex-direction: column;
    animation: csw-modal-in 0.25s cubic-bezier(0.16, 1, 0.3, 1) forwards;
  }
  @keyframes csw-modal-in {
    from { opacity: 0; transform: translate(-50%, -52%) scale(0.97); }
    to { opacity: 1; transform: translate(-50%, -50%) scale(1); }
  }
  .csw-modal__header {
    padding: 20px 24px; border-bottom: 1px solid #f1f5f9;
    display: flex; align-items: center; justify-content: space-between;
    background: #f8fafc; flex-shrink: 0;
  }
  .csw-modal__header-left { display: flex; align-items: center; gap: 14px; }
  .csw-modal__icon { font-size: 26px; }
  .csw-modal__title { font-size: 17px; font-weight: 800; color: #0f172a; margin: 0 0 2px; }
  .csw-modal__subtitle { font-size: 12px; color: #94a3b8; margin: 0; font-family: monospace; }
  .csw-modal__body { overflow-y: auto; padding: 24px; }

  /*  Modal Meta Grid  */
  .csw-meta-grid {
    display: grid; grid-template-columns: 1fr 1fr; gap: 12px; margin-bottom: 20px;
  }
  .csw-meta-item {
    background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 12px; padding: 12px 14px;
    display: flex; flex-direction: column; gap: 5px;
  }
  .csw-meta-item--full { grid-column: 1 / -1; }
  .csw-meta-label { font-size: 11px; font-weight: 700; color: #94a3b8; text-transform: uppercase; letter-spacing: 0.5px; }
  .csw-meta-value { font-size: 14px; font-weight: 600; color: #1e293b; }
  .csw-mono { font-family: monospace; }

  /*  Balance Check  */
  .csw-balance-check {
    display: flex; align-items: center; gap: 14px;
    padding: 14px 18px; border-radius: 14px; margin-bottom: 24px; border: 1.5px solid;
  }
  .csw-balance-check--ok { background: #f0fdf4; border-color: #bbf7d0; }
  .csw-balance-check--err { background: #fff1f2; border-color: #fecdd3; }
  .csw-balance-check__icon {
    width: 36px; height: 36px; border-radius: 50%; flex-shrink: 0;
    display: flex; align-items: center; justify-content: center;
    font-size: 18px; font-weight: 800;
  }
  .csw-balance-check--ok .csw-balance-check__icon { background: #dcfce7; color: #16a34a; }
  .csw-balance-check--err .csw-balance-check__icon { background: #fecdd3; color: #e11d48; }
  .csw-balance-check__title { font-size: 14px; font-weight: 700; margin-bottom: 4px; }
  .csw-balance-check--ok .csw-balance-check__title { color: #166534; }
  .csw-balance-check--err .csw-balance-check__title { color: #9f1239; }
  .csw-balance-check__detail { font-size: 12px; }
  .csw-balance-check--ok .csw-balance-check__detail { color: #166534; }
  .csw-balance-check--err .csw-balance-check__detail { color: #9f1239; }

  /*  Entry Grid (double-entry)  */
  .csw-section-title { font-size: 14px; font-weight: 700; color: #374151; margin-bottom: 16px; }
  .csw-entry-grid { display: flex; gap: 12px; align-items: flex-start; }
  .csw-entry-col { flex: 1; }
  .csw-entry-col__header {
    font-size: 12px; font-weight: 700; text-transform: uppercase; letter-spacing: 0.5px;
    padding-bottom: 10px; margin-bottom: 10px; border-bottom: 2px solid;
  }
  .csw-entry-col--debit .csw-entry-col__header { color: #9f1239; border-color: #fecdd3; }
  .csw-entry-col--credit .csw-entry-col__header { color: #166534; border-color: #bbf7d0; }
  .csw-entry-card {
    padding: 12px 14px; border-radius: 12px; border: 1px solid; margin-bottom: 8px;
  }
  .csw-entry-card--debit { background: #fff1f2; border-color: #fecdd3; }
  .csw-entry-card--credit { background: #f0fdf4; border-color: #bbf7d0; }
  .csw-entry-card__name { font-size: 12px; font-weight: 600; marginBottom: 4px; }
  .csw-entry-card--debit .csw-entry-card__name { color: #9f1239; }
  .csw-entry-card--credit .csw-entry-card__name { color: #166534; }
  .csw-entry-card__amount { font-size: 16px; font-weight: 800; margin-top: 4px; }
  .csw-entry-card__amount--debit { color: #e11d48; }
  .csw-entry-card__amount--credit { color: #16a34a; }
  .csw-entry-total {
    padding: 10px 14px; border-radius: 12px; font-size: 13px; font-weight: 800;
    text-align: center; letter-spacing: 0.2px;
  }
  .csw-entry-total--debit { background: #fecdd3; color: #9f1239; }
  .csw-entry-total--credit { background: #bbf7d0; color: #166534; }
  .csw-entry-divider {
    align-self: center; font-size: 20px; color: #94a3b8; padding: 0 4px;
    flex-shrink: 0;
  }

  /*  Shared states  */
  .csw-center-state {
    display: flex; flex-direction: column; align-items: center; justify-content: center;
    padding: 40px 20px; gap: 12px; color: #94a3b8;
  }
  .csw-center-state p { margin: 0; font-size: 14px; }
  .csw-center-state--error { color: #e11d48; }

  .csw-spinner {
    width: 28px; height: 28px; border: 3px solid #f1f5f9;
    border-top-color: #f97316; border-radius: 50%;
    animation: csw-spin 0.8s linear infinite;
  }
  @keyframes csw-spin { to { transform: rotate(360deg); } }
`;
