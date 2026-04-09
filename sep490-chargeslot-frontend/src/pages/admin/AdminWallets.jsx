import { useMemo, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { adminFinanceApi } from "@/services/api";
import Pagination from "@/components/Pagination";

function formatDate(dateStr) {
  if (!dateStr) return "—";
  const s = String(dateStr);
  const d = new Date(String(s).replace("Z", ""));
  return `${String(d.getDate()).padStart(2, "0")}/${String(d.getMonth() + 1).padStart(2, "0")}/${d.getFullYear()} ${String(d.getHours()).padStart(2, "0")}:${String(d.getMinutes()).padStart(2, "0")}`;
}

// TransactionDetailModal Component
function TransactionDetailModal({ transactionId, onClose }) {
  const { data: tx, isLoading, error } = useQuery({
    queryKey: ["admin-transaction-detail", transactionId],
    queryFn: () => adminFinanceApi.getTransactionDetail(transactionId),
    enabled: !!transactionId,
  });

  return (
    <>
      <div className="cs-modal-overlay" onClick={onClose} />
      <div className="cs-modal" style={{ maxWidth: 600 }}>
        <div className="cs-modal__header">
          <h2 className="cs-modal__title">Chi tiết dòng tiền (Sổ cái)</h2>
          <button onClick={onClose} className="cs-modal__close">&times;</button>
        </div>
        <div className="cs-modal__content" style={{ padding: 24 }}>
          {isLoading ? (
            <div style={{ textAlign: "center", padding: "40px 0" }}>Đang tải chi tiết...</div>
          ) : error ? (
            <div style={{ color: "red", textAlign: "center", padding: "40px 0" }}>Lỗi tải chi tiết giao dịch!</div>
          ) : !tx ? (
            <div style={{ textAlign: "center", padding: "40px 0", color: "#64748b" }}>Không tìm thấy giao dịch.</div>
          ) : (
            <div>
              <div style={{ display: "flex", justifyContent: "space-between", marginBottom: 12 }}>
                <span style={{ color: "#64748b", fontSize: 14 }}>Mã Giao dịch:</span>
                <strong style={{ color: "#1e293b", fontSize: 14 }}>TX_{tx.id}</strong>
              </div>
              <div style={{ display: "flex", justifyContent: "space-between", marginBottom: 12 }}>
                <span style={{ color: "#64748b", fontSize: 14 }}>Loại:</span>
                <span className="cs-admin-status-badge cs-admin-status-badge--info">
                  {tx.referenceType} (Tham chiếu #{tx.referenceId})
                </span>
              </div>
              <div style={{ display: "flex", justifyContent: "space-between", marginBottom: 12 }}>
                <span style={{ color: "#64748b", fontSize: 14 }}>Ngày giờ:</span>
                <strong style={{ color: "#1e293b", fontSize: 14 }}>{formatDate(tx.createdAt)}</strong>
              </div>
              <div style={{ display: "flex", justifyContent: "space-between", marginBottom: 24, paddingBottom: 16, borderBottom: "1px dashed #cbd5e1" }}>
                <span style={{ color: "#64748b", fontSize: 14 }}>Mô tả:</span>
                <strong style={{ color: "#1e293b", fontSize: 14, textAlign: "right", maxWidth: "60%" }}>{tx.memo || "—"}</strong>
              </div>

              <h3 style={{ fontSize: 15, fontWeight: 700, color: "#334155", marginBottom: 16 }}>💰 Các bút toán trung chuyển (Entries)</h3>
              <div style={{ display: "flex", flexDirection: "column", gap: 12 }}>
                {tx.entries && tx.entries.map((entry, idx) => (
                  <div key={idx} style={{
                    padding: 16, borderRadius: 12, border: "1px solid #e2e8f0",
                    background: entry.direction === "Credit" ? "#f0fdf4" : "#fef2f2"
                  }}>
                    <div style={{ display: "flex", justifyContent: "space-between", marginBottom: 8 }}>
                      <span style={{ fontSize: 14, fontWeight: 700, color: "#334155" }}>
                        Ví {entry.walletType} (#{entry.walletId})
                      </span>
                      <span style={{ fontSize: 15, fontWeight: 800, color: entry.direction === "Credit" ? "#16a34a" : "#dc2626" }}>
                        {entry.direction === "Credit" ? "+" : "-"}{entry.amount?.toLocaleString()} đ
                      </span>
                    </div>
                    <div style={{ fontSize: 13, color: "#64748b" }}>
                      Chủ sở hữu: <strong>{entry.ownerName}</strong>
                    </div>
                    <div style={{ fontSize: 12, color: entry.direction === "Credit" ? "#166534" : "#991b1b", marginTop: 4, fontWeight: 600 }}>
                      Chiều: {entry.direction}
                    </div>
                  </div>
                ))}
              </div>
            </div>
          )}
        </div>
      </div>
      <style>{`
        .cs-modal-overlay {
          position: fixed; top: 0; left: 0; right: 0; bottom: 0;
          background: rgba(0,0,0,0.5); z-index: 10000;
          backdrop-filter: blur(2px);
        }
        .cs-modal {
          position: fixed; top: 50%; left: 50%; transform: translate(-50%, -50%);
          width: 90%; background: white; z-index: 10001;
          border-radius: 20px; box-shadow: 0 20px 40px rgba(0,0,0,0.2);
          overflow: hidden; max-height: 90vh; display: flex; flex-direction: column;
        }
        .cs-modal__header {
          padding: 20px 24px; border-bottom: 1px solid #e5e7eb;
          display: flex; align-items: center; justify-content: space-between;
          background: #f8fafc;
        }
        .cs-modal__title { font-size: 18px; font-weight: 700; color: #1e293b; margin: 0; }
        .cs-modal__close {
          background: none; border: none; font-size: 28px; line-height: 1; cursor: pointer; color: #64748b;
        }
        .cs-modal__content { overflow-y: auto; }
      `}</style>
    </>
  );
}

// Drawer Component to show transactions
function WalletTransactionsDrawer({ walletId, onClose }) {
  const [page, setPage] = useState(1);
  const pageSize = 20;
  const [detailTxId, setDetailTxId] = useState(null);

  const { data: rawData, isLoading, error } = useQuery({
    queryKey: ["admin-wallet-transactions", walletId, page],
    queryFn: () => adminFinanceApi.getWalletTransactions(walletId, { page, pageSize }),
    enabled: !!walletId,
  });

  const txs = rawData?.items ?? [];
  const totalCount = rawData?.totalCount ?? 0;

  return (
    <>
      <div className="cs-drawer-overlay" onClick={onClose} />
      <div className="cs-drawer">
        <div className="cs-drawer__header">
          <h2 className="cs-drawer__title">Lịch sử giao dịch ví #{walletId}</h2>
          <button onClick={onClose} className="cs-drawer__close">&times;</button>
        </div>
        <div className="cs-drawer__content">
          {isLoading ? (
            <div style={{ textAlign: "center", padding: "40px 0" }}>Đang tải...</div>
          ) : error ? (
            <div style={{ color: "red", textAlign: "center", padding: "40px 0" }}>Lỗi tải giao dịch!</div>
          ) : txs.length === 0 ? (
            <div style={{ textAlign: "center", padding: "40px 0", color: "#64748b" }}>Ví này chưa có giao dịch nào.</div>
          ) : (
            <>
              <table className="cs-admin-table" style={{ minWidth: "100%" }}>
                <thead>
                  <tr>
                    <th>Mã GD</th>
                    <th>Loại</th>
                    <th>Số Tiền</th>
                    <th>Ngày/Giờ</th>
                    <th style={{ textAlign: "center" }}>Chi tiết</th>
                  </tr>
                </thead>
                <tbody>
                  {txs.map((tx) => (
                    <tr key={tx.id}>
                      <td className="cs-admin-table__id">TX_{tx.id}</td>
                      <td>{tx.type || tx.transactionType}</td>
                      <td style={{ color: tx.amount > 0 ? "#16a34a" : "#dc2626", fontWeight: "bold" }}>
                        {tx.amount > 0 ? "+" : ""}{tx.amount?.toLocaleString()} đ
                      </td>
                      <td style={{ color: "#64748b", fontSize: 13 }}>{formatDate(tx.createdAt)}</td>
                      <td style={{ textAlign: "center" }}>
                        <button
                          onClick={() => setDetailTxId(tx.id)}
                          className="cs-admin-btn"
                          style={{ padding: "4px 8px", fontSize: 13 }}
                        >
                          Soi
                        </button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
              <div style={{ marginTop: 20 }}>
                <Pagination page={page} totalCount={totalCount} pageSize={pageSize} onPageChange={(p) => setPage(p)} />
              </div>
            </>
          )}
        </div>
      </div>
      
      {detailTxId && (
        <TransactionDetailModal transactionId={detailTxId} onClose={() => setDetailTxId(null)} />
      )}

      <style>{`
        .cs-drawer-overlay {
          position: fixed; top: 0; left: 0; right: 0; bottom: 0;
          background: rgba(0,0,0,0.4); z-index: 1000;
          backdrop-filter: blur(2px);
        }
        .cs-drawer {
          position: fixed; top: 0; right: 0; bottom: 0; width: 500px;
          background: white; z-index: 1001; box-shadow: -4px 0 24px rgba(0,0,0,0.1);
          display: flex; flex-direction: column;
          animation: slideIn 0.3s forwards;
        }
        @media (max-width: 600px) { .cs-drawer { width: 100%; } }
        @keyframes slideIn { from { transform: translateX(100%); } to { transform: translateX(0); } }
        .cs-drawer__header {
          padding: 20px 24px; border-bottom: 1px solid #e5e7eb;
          display: flex; align-items: center; justify-content: space-between;
        }
        .cs-drawer__title { font-size: 18px; font-weight: 700; color: #1e293b; margin: 0; }
        .cs-drawer__close {
          background: none; border: none; font-size: 28px; line-height: 1; cursor: pointer; color: #64748b;
        }
        .cs-drawer__content { flex: 1; overflow-y: auto; padding: 24px; background: #fafafa; }
      `}</style>
    </>
  );
}

export default function AdminWallets() {
  const [search, setSearch] = useState("");
  const [typeFilter, setTypeFilter] = useState("ALL");
  const [page, setPage] = useState(1);
  const pageSize = 20;
  
  const [selectedWalletId, setSelectedWalletId] = useState(null);

  const { data: rawData, isLoading, error } = useQuery({
    queryKey: ["admin-fin-wallets", typeFilter, page],
    queryFn: () => {
        const filter = { page, pageSize };
        if (typeFilter !== "ALL") filter.walletType = typeFilter;
        return adminFinanceApi.getWallets(filter);
    },
    refetchInterval: 30000,
  });

  const wallets = rawData?.items ?? [];
  const totalCount = rawData?.totalCount ?? 0;

  const filtered = useMemo(() => {
    const normalize = (str) => {
      if (!str) return "";
      return String(str).normalize("NFD").replace(/[\u0300-\u036f]/g, "").replace(/đ/g, "d").replace(/Đ/g, "D").toLowerCase();
    };
    const keyword = normalize(search.trim());
    return wallets.filter((w) => {
      if (!keyword) return true;
      const typeLabel = w.walletType === "System" ? "Vi He Thong" : 
                        w.walletType === "Owner" ? "Vi Chu Tram" : "Vi Tai Xe";
      return (
        String(w.id).includes(keyword) ||
        normalize(typeLabel).includes(keyword)
      );
    });
  }, [wallets, search]);

  if (isLoading) {
    return (
      <div className="cs-admin-page">
        <div style={{ textAlign: "center", paddingTop: 120 }}>
          <div className="cs-admin-table__spinner" style={{ margin: "0 auto 16px" }} />
          <p style={{ color: "#64748b", fontSize: 14 }}>Đang tải danh sách Ví...</p>
        </div>
        <style>{styles}</style>
      </div>
    );
  }

  if (error) {
    return (
      <div className="cs-admin-page">
        <div style={{ textAlign: "center", paddingTop: 120 }}>
          <p style={{ color: "#ef4444", fontSize: 16, marginBottom: 16 }}>❌ Lỗi tải dữ liệu: {error.message}</p>
        </div>
        <style>{styles}</style>
      </div>
    );
  }

  return (
    <div className="cs-admin-page">
      <div className="cs-admin-page__header">
        <div>
          <h1 className="cs-admin-page__title">Giám sát Vốn & Ví (Tổng: {totalCount})</h1>
          <p className="cs-admin-page__subtitle">Công cụ theo dõi dòng tiền Hệ thống</p>
        </div>
      </div>

      <div className="cs-admin-filter">
        <div className="cs-admin-filter__search">
          <svg className="cs-admin-filter__search-icon" width="18" height="18" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
          </svg>
          <input
            type="text"
            value={search}
            onChange={(e) => {
              setSearch(e.target.value);
              setPage(1);
            }}
            placeholder="Tìm mã Ví, theo loại Ví..."
            className="cs-admin-filter__input"
          />
        </div>
        <select
          value={typeFilter}
          onChange={(e) => {
            setTypeFilter(e.target.value);
            setPage(1);
          }}
          className="cs-admin-filter__select"
        >
          <option value="ALL">Tất cả loại Ví</option>
          <option value="System">Ví Tổng Hệ Thống (System)</option>
          <option value="Owner">Ví Chủ Trạm (Owner)</option>
          <option value="Driver">Ví Tài Xế (Driver)</option>
        </select>
        <button onClick={() => { setSearch(""); setTypeFilter("ALL"); setPage(1); }} className="cs-admin-filter__reset">
          <svg width="16" height="16" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
          </svg>
          Đặt lại
        </button>
      </div>

      <div className="cs-admin-table-wrap">
        <table className="cs-admin-table">
          <thead>
            <tr>
              <th>Mã Ví</th>
              <th>Người Dùng (Owner/Driver)</th>
              <th>Loại Ví</th>
              <th>Số Dư (Kế toán)</th>
              <th style={{ textAlign: "center" }}>Hành động</th>
            </tr>
          </thead>
          <tbody>
            {filtered.length === 0 ? (
              <tr>
                <td colSpan={5} className="cs-admin-table__empty">
                  <p>Không tìm thấy ví nào khớp.</p>
                </td>
              </tr>
            ) : (
              filtered.map((w) => {
                return (
                  <tr key={w.id}>
                    <td className="cs-admin-table__id">WA_{w.id}</td>
                    <td className="cs-admin-table__name">
                      {w.walletType === "System" ? "Hệ Thống" : 
                       w.walletType === "Owner" ? "Chủ Trạm (Owner)" : "Tài Xế (Driver)"}
                    </td>
                    <td>
                      {w.walletType === "System" ? (
                        <span className="cs-admin-status-badge cs-admin-status-badge--purple">
                           <span className="cs-admin-status-badge__dot" /> Ví Hệ Thống
                        </span>
                      ) : w.walletType === "Owner" ? (
                        <span className="cs-admin-status-badge" style={{background: "#fffbeb", color: "#f59e0b"}}>
                           <span className="cs-admin-status-badge__dot" style={{background: "#f59e0b"}} /> Ví Chủ Trạm
                        </span>
                      ) : (
                        <span className="cs-admin-status-badge cs-admin-status-badge--info">
                           <span className="cs-admin-status-badge__dot" /> Ví Tài Xế
                        </span>
                      )}
                    </td>
                    <td style={{ fontWeight: "700", color: "#16a34a", fontSize: "16px" }}>{w.availableBalance?.toLocaleString() || "0"} đ <br/><span style={{fontSize: "12px", color: "#64748b"}}>(Băng: {w.frozenBalance?.toLocaleString() || "0"} đ)</span></td>
                    <td style={{ textAlign: "center" }}>
                      <button onClick={() => setSelectedWalletId(w.id)} className="cs-admin-btn">
                        Soi Giao Dịch
                      </button>
                    </td>
                  </tr>
                );
              })
            )}
          </tbody>
        </table>
        
        <div style={{ marginTop: 20 }}>
          <Pagination 
            page={page} 
            totalCount={search ? filtered.length : totalCount} 
            pageSize={pageSize} 
            onPageChange={(p) => setPage(p)} 
          />
        </div>
      </div>

      {selectedWalletId && (
        <WalletTransactionsDrawer walletId={selectedWalletId} onClose={() => setSelectedWalletId(null)} />
      )}

      <style>{styles}</style>
    </div>
  );
}

const styles = `
  .cs-admin-page { max-width: 1400px; width: 95%; margin: 0 auto; padding: 88px 0 40px; }
  @media (max-width: 768px) { .cs-admin-page { width: 100%; padding: 80px 16px 40px; } }
  .cs-admin-page__header { margin-bottom: 28px; }
  .cs-admin-page__title { font-size: 26px; font-weight: 800; color: #1e293b; letter-spacing: -0.5px; }
  .cs-admin-page__subtitle { font-size: 14px; color: #64748b; margin-top: 4px; }
  .cs-admin-filter { background: white; border: 1px solid rgba(0,0,0,0.06); border-radius: 16px; padding: 16px 20px; margin-bottom: 20px; display: flex; gap: 12px; align-items: center; flex-wrap: wrap; }
  .cs-admin-filter__search { flex: 1; min-width: 200px; position: relative; }
  .cs-admin-filter__search-icon { position: absolute; left: 14px; top: 50%; transform: translateY(-50%); color: #9ca3af; }
  .cs-admin-filter__input { width: 100%; height: 42px; border: 1.5px solid #e5e7eb; border-radius: 12px; padding: 0 16px 0 40px; font-size: 14px; outline: none; transition: all 0.2s; background: #f9fafb; box-sizing: border-box; }
  .cs-admin-filter__input:focus { border-color: #f97316; background: white; box-shadow: 0 0 0 3px rgba(249,115,22,0.1); }
  .cs-admin-filter__select { height: 42px; border: 1.5px solid #e5e7eb; border-radius: 12px; padding: 0 14px; font-size: 14px; background: #f9fafb; cursor: pointer; outline: none; transition: border-color 0.2s; }
  .cs-admin-filter__select:focus { border-color: #f97316; }
  .cs-admin-filter__reset { height: 42px; padding: 0 18px; border: 1.5px solid #e5e7eb; border-radius: 12px; background: white; font-size: 13px; font-weight: 500; color: #64748b; cursor: pointer; display: flex; align-items: center; gap: 6px; transition: all 0.2s; }
  .cs-admin-filter__reset:hover { background: #f9fafb; border-color: #d1d5db; }
  .cs-admin-table-wrap { background: white; border: 1px solid rgba(0,0,0,0.06); border-radius: 16px; overflow-x: auto; -webkit-overflow-scrolling: touch; }
  .cs-admin-table__spinner { width: 28px; height: 28px; border: 3px solid #f3f4f6; border-top-color: #f97316; border-radius: 50%; animation: cs-spin 0.8s linear infinite; }
  @keyframes cs-spin { to { transform: rotate(360deg); } }
  .cs-admin-table { width: 100%; min-width: 900px; border-collapse: collapse; }
  .cs-admin-table thead { background: linear-gradient(180deg, #f8fafc, #f1f5f9); }
  .cs-admin-table th { padding: 14px 18px; font-size: 12px; font-weight: 700; color: #64748b; text-transform: uppercase; letter-spacing: 0.5px; text-align: left; border-bottom: 1px solid #e5e7eb; }
  .cs-admin-table td { padding: 14px 18px; font-size: 14px; color: #374151; border-bottom: 1px solid #f1f5f9; }
  .cs-admin-table tbody tr { transition: background 0.15s; }
  .cs-admin-table tbody tr:hover { background: #fefce8; }
  .cs-admin-table__id { color: #9ca3af; font-size: 13px; }
  .cs-admin-table__name { font-weight: 600; color: #1e293b; }
  .cs-admin-table__empty { text-align: center; padding: 48px 0 !important; color: #94a3b8; }
  .cs-admin-table__empty p { margin-top: 12px; font-size: 14px; }
  .cs-admin-status-badge { display: inline-flex; align-items: center; gap: 6px; padding: 4px 12px; border-radius: 50px; font-size: 12px; font-weight: 600; white-space: nowrap; }
  .cs-admin-status-badge__dot { width: 7px; height: 7px; border-radius: 50%; }
  .cs-admin-status-badge--purple { background: #f5f3ff; color: #7c3aed; }
  .cs-admin-status-badge--purple .cs-admin-status-badge__dot { background: #7c3aed; }
  .cs-admin-status-badge--info { background: #eff6ff; color: #3b82f6; }
  .cs-admin-status-badge--info .cs-admin-status-badge__dot { background: #3b82f6; }
  .cs-admin-btn { padding: 8px 16px; border-radius: 8px; background: #f9f9f9; border: 1px solid #e5e7eb; font-weight: 600; color: #475569; cursor: pointer; transition: 0.2s; }
  .cs-admin-btn:hover { background: #f1f5f9; border-color: #cbd5e1; color: #1e293b; }
`;
