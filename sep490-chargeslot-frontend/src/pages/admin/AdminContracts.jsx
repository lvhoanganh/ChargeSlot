import { useState, useEffect } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { adminContractApi } from "@/services/api";
import { showToast as toast } from "@/components/Toast";
import Pagination from "@/components/Pagination";
import UserProfileModal from "@/components/UserProfileModal";

const STATUS_LABELS = {
  Pending: { label: "Chờ ký", color: "#f59e0b", bg: "#fef3c7" },
  Signed: { label: "Đã ký", color: "#10b981", bg: "#d1fae5" },
  Terminated: { label: "Đã chấm dứt", color: "#ef4444", bg: "#fee2e2" },
  Expired: { label: "Hết hạn", color: "#64748b", bg: "#f1f5f9" },
};

export default function AdminContracts() {
  const [page, setPage] = useState(1);
  const [statusFilter, setStatusFilter] = useState("ALL");
  const [search, setSearch] = useState("");
  const [debouncedSearch, setDebouncedSearch] = useState("");
  const [fromDate, setFromDate] = useState("");
  const [toDate, setToDate] = useState("");
  const [terminateModal, setTerminateModal] = useState({ open: false, ownerUserId: null, reason: "" });
  const [viewProfileTarget, setViewProfileTarget] = useState(null);
  const queryClient = useQueryClient();

  useEffect(() => {
    const handler = setTimeout(() => {
      setDebouncedSearch(search);
      setPage(1);
    }, 500);
    return () => clearTimeout(handler);
  }, [search]);

  const { data, isLoading } = useQuery({
    queryKey: ["adminContracts", page, statusFilter, debouncedSearch, fromDate, toDate],
    queryFn: async () => {
      const res = await adminContractApi.getAll({
         status: statusFilter,
         search: debouncedSearch,
         fromDate,
         toDate,
         page,
         pageSize: 20
      });
      return {
        items: res.items || res.data || [],
        total: res.totalCount || res.totalItems || 0
      };
    },
    keepPreviousData: true,
  });

  const terminateMutation = useMutation({
    mutationFn: ({ ownerUserId, reason }) => adminContractApi.terminate(ownerUserId, reason),
    onSuccess: () => {
      toast.success("Đã chấm dứt hợp đồng thành công.");
      setTerminateModal({ open: false, ownerUserId: null, reason: "" });
      queryClient.invalidateQueries(["adminContracts"]);
    },
    onError: (err) => {
      toast.error(err.message || "Lỗi khi chấm dứt hợp đồng.");
    },
  });

  const handleDownload = async (ownerUserId) => {
    try {
      const response = await adminContractApi.download(ownerUserId);
      const url = window.URL.createObjectURL(response);
      const a = document.createElement("a");
      a.style.display = "none";
      a.href = url;
      a.download = `HopDong_Owner_${ownerUserId}.pdf`;
      document.body.appendChild(a);
      a.click();
      window.URL.revokeObjectURL(url);
    } catch (error) {
      toast.error(error.message || "Không tải được file hợp đồng.");
    }
  };

  const contracts = data?.items || [];
  const totalItems = data?.total || 0;
  const totalPages = Math.max(1, Math.ceil(totalItems / 20));

  return (
    <div className="cs-admin-page">
      {/* Page Header */}
      <div className="cs-admin-page__header">
        <div>
          <h1 className="cs-admin-page__title">Quản lý Hợp đồng</h1>
          <p className="cs-admin-page__subtitle">
            Theo dõi, tải xuống và quản lý các hợp đồng với chủ trạm
          </p>
        </div>
      </div>

      {/* Filter Bar */}
      <div className="cs-admin-filter">
        <div style={{ position: "relative", flex: 1, minWidth: "220px" }}>
          <svg style={{ position: "absolute", left: 14, top: 12, color: "#9ca3af" }} width="18" height="18" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
          </svg>
          <input
            type="text"
            className="cs-admin-filter__input"
            style={{ width: "100%", paddingLeft: 40 }}
            placeholder="Tìm theo chủ trạm, mã HĐ..."
            value={search}
            onChange={(e) => setSearch(e.target.value)}
          />
        </div>
        
        <input 
          type="date"
          className="cs-admin-filter__select"
          value={fromDate}
          onChange={(e) => { setFromDate(e.target.value); setPage(1); }}
          title="Từ ngày"
        />
        <input 
          type="date"
          className="cs-admin-filter__select"
          value={toDate}
          onChange={(e) => { setToDate(e.target.value); setPage(1); }}
          title="Đến ngày"
        />
        <select
          value={statusFilter}
          onChange={(e) => { setStatusFilter(e.target.value); setPage(1); }}
          className="cs-admin-filter__select"
          style={{ width: "160px" }}
        >
          <option value="ALL">Tất cả trạng thái</option>
          <option value="Pending">Chờ ký</option>
          <option value="Signed">Đã ký</option>
          <option value="Terminated">Đã chấm dứt</option>
          <option value="Expired">Hết hạn</option>
        </select>
        <button onClick={() => { 
          setStatusFilter("ALL"); 
          setSearch(""); 
          setFromDate(""); 
          setToDate(""); 
          setPage(1); 
        }} className="cs-admin-filter__reset">
          <svg width="16" height="16" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
          </svg>
          Xóa
        </button>
      </div>

      {/* Table Area */}
      <div className="cs-admin-table-wrap">
        {isLoading && (
          <div className="cs-admin-table__loading">
            <div className="cs-admin-table__spinner" />
            <span>Đang tải...</span>
          </div>
        )}
        <table className="cs-admin-table">
          <thead>
            <tr>
              <th>Mã Hợp Đồng</th>
              <th>Chủ Trạm</th>
              <th>Trạng thái</th>
              <th>Ngày thiết lập</th>
              <th>Ngày ký</th>
              <th style={{ textAlign: "right" }}>Thao tác</th>
            </tr>
          </thead>
          <tbody>
            {contracts.length === 0 && !isLoading ? (
              <tr>
                <td colSpan={7} className="cs-admin-table__empty">
                  <svg width="40" height="40" fill="none" stroke="#cbd5e1" viewBox="0 0 24 24" style={{ margin: "0 auto 12px" }}>
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.5} d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" />
                  </svg>
                  <p>Không có dữ liệu hợp đồng</p>
                </td>
              </tr>
            ) : (
              contracts.map(c => {
                const st = STATUS_LABELS[c.status] || { label: c.status, color: "gray", bg: "#f1f5f9" };
                return (
                  <tr key={c.ownerUserId}>
                    <td className="cs-admin-table__id">#{c.contractNumber}</td>
                    <td className="cs-admin-table__name">{c.ownerName} <span style={{ fontSize: 11, color: "#9ca3af", marginLeft: 4 }}>ID: {c.ownerUserId}</span></td>
                    <td>
                      <span className="cs-admin-status-badge" style={{ background: st.bg, color: st.color }}>
                        <span className="cs-admin-status-badge__dot" style={{ background: st.color }} />
                        {st.label}
                      </span>
                    </td>
                    <td>{c.createdAt ? new Date(c.createdAt).toLocaleDateString("vi-VN") : "—"}</td>
                    <td>{c.signedAt ? new Date(c.signedAt).toLocaleDateString("vi-VN") : "—"}</td>
                    <td style={{ textAlign: "right" }}>
                      <div style={{ display: "flex", justifyContent: "flex-end", gap: 8 }}>
                        <button
                          onClick={() => setViewProfileTarget({ id: c.ownerUserId, role: "Owner", fullName: c.ownerName })}
                          title="Chi tiết"
                          className="cs-admin-action-btn"
                          style={{ background: "#f8fafc", color: "#475569", border: "1px solid #cbd5e1", height: 32, minWidth: "unset", padding: "0 12px" }}
                        >
                          Chi tiết
                        </button>
                        <button
                          onClick={() => handleDownload(c.ownerUserId)}
                          title="Tải PDF"
                          className="cs-admin-action-btn"
                          style={{ background: "#eff6ff", color: "#3b82f6", height: 32, minWidth: "unset", padding: "0 12px" }}
                        >
                          Tải xuống
                        </button>
                        {c.status === "Signed" && (
                          <button
                            onClick={() => setTerminateModal({ open: true, ownerUserId: c.ownerUserId, reason: "" })}
                            title="Chấm dứt HĐ"
                            className="cs-admin-action-btn cs-admin-action-btn--ban"
                            style={{ height: 32, minWidth: "unset", padding: "0 12px" }}
                          >
                            Chấm dứt
                          </button>
                        )}
                      </div>
                    </td>
                  </tr>
                );
              })
            )}
          </tbody>
        </table>
      </div>

      <div className="cs-admin-pagination" style={{ margin: "20px 0" }}>
        <Pagination page={page} totalCount={totalItems} pageSize={20} onPageChange={setPage} />
      </div>

      {terminateModal.open && (
        <div className="cs-admin-modal-overlay">
          <div className="cs-admin-modal text-center" style={{ maxWidth: 450, textAlign: "left" }}>
            <div style={{ display: "flex", alignItems: "center", gap: 12, marginBottom: 16 }}>
              <div style={{ width: 44, height: 44, borderRadius: "50%", background: "#fef2f2", color: "#dc2626", display: "flex", alignItems: "center", justifyContent: "center" }}>
                <svg width="24" height="24" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" /></svg>
              </div>
              <div>
                <h3 className="cs-admin-modal__title" style={{ marginBottom: 0 }}>Chấm dứt Hợp đồng</h3>
                <p className="cs-admin-modal__desc" style={{ marginBottom: 0, color: "#dc2626" }}>Thao tác này là dứt khoát và không thể hoàn tác.</p>
              </div>
            </div>
            <div style={{ marginBottom: 24 }}>
              <label style={{ display: "block", fontSize: 13, fontWeight: 700, color: "#374151", marginBottom: 8 }}>Lý do chấm dứt (Bắt buộc):</label>
              <textarea
                style={{ width: "100%", border: "2px solid #e5e7eb", borderRadius: 12, padding: 12, fontSize: 14, outline: "none", resize: "none" }}
                rows="3"
                placeholder="Nhập lý do rõ ràng để thông báo cho Chủ trạm..."
                value={terminateModal.reason}
                onChange={e => setTerminateModal(prev => ({ ...prev, reason: e.target.value }))}
              />
            </div>
            <div className="cs-admin-modal__actions" style={{ justifyContent: "flex-end", marginTop: 0 }}>
              <button
                onClick={() => setTerminateModal({ open: false, ownerUserId: null, reason: "" })}
                className="cs-admin-modal__btn cs-admin-modal__btn--cancel"
              >
                Hủy bỏ
              </button>
              <button
                disabled={!terminateModal.reason.trim() || terminateMutation.isPending}
                onClick={() => terminateMutation.mutate({ ownerUserId: terminateModal.ownerUserId, reason: terminateModal.reason })}
                className="cs-admin-modal__btn cs-admin-modal__btn--danger"
              >
                {terminateMutation.isPending ? "Đang xử lý..." : "Xác nhận Chấm dứt"}
              </button>
            </div>
          </div>
        </div>
      )}

      {viewProfileTarget && (
        <UserProfileModal
          user={viewProfileTarget}
          onClose={() => setViewProfileTarget(null)}
        />
      )}

      {/* Reused CSS from other pages for styling consistency */}
      <style>{`
        .cs-admin-page { max-width: 1400px; width: 95%; margin: 0 auto; padding: 88px 0 40px; }
        .cs-admin-page__header { margin-bottom: 28px; }
        .cs-admin-page__title { font-size: 26px; font-weight: 800; color: #1e293b; letter-spacing: -0.5px; margin: 0; }
        .cs-admin-page__subtitle { font-size: 14px; color: #64748b; margin-top: 4px; }
        .cs-admin-filter { background: white; border: 1px solid rgba(0,0,0,0.06); border-radius: 16px; padding: 16px 20px; margin-bottom: 20px; display: flex; gap: 12px; align-items: center; flex-wrap: wrap; }
        .cs-admin-filter__input { height: 42px; border: 1.5px solid #e5e7eb; border-radius: 12px; padding: 0 14px; font-size: 14px; background: #f9fafb; outline: none; transition: border-color 0.2s; box-sizing: border-box; }
        .cs-admin-filter__input:focus { border-color: #f97316; background: white; box-shadow: 0 0 0 3px rgba(249,115,22,0.1); }
        .cs-admin-filter__select { height: 42px; border: 1.5px solid #e5e7eb; border-radius: 12px; padding: 0 14px; font-size: 14px; background: #f9fafb; cursor: pointer; outline: none; transition: border-color 0.2s; }
        .cs-admin-filter__select:focus { border-color: #f97316; }
        .cs-admin-filter__reset { height: 42px; padding: 0 18px; border: 1.5px solid #e5e7eb; border-radius: 12px; background: white; font-size: 13px; font-weight: 500; color: #64748b; cursor: pointer; display: flex; align-items: center; gap: 6px; transition: all 0.2s; }
        .cs-admin-filter__reset:hover { background: #f9fafb; border-color: #d1d5db; }
        .cs-admin-table-wrap { background: white; border: 1px solid rgba(0,0,0,0.06); border-radius: 16px; overflow-x: auto; overflow-y: hidden; position: relative; -webkit-overflow-scrolling: touch; }
        .cs-admin-table__loading { position: absolute; inset: 0; background: rgba(255,255,255,0.8); display: flex; flex-direction: column; align-items: center; justify-content: center; gap: 10px; z-index: 10; color: #64748b; font-size: 14px; }
        .cs-admin-table__spinner { width: 28px; height: 28px; border: 3px solid #f3f4f6; border-top-color: #f97316; border-radius: 50%; animation: cs-spin-slow 0.8s linear infinite; }
        .cs-admin-table { width: 100%; min-width: 1000px; border-collapse: collapse; }
        .cs-admin-table thead { background: linear-gradient(180deg, #f8fafc, #f1f5f9); }
        .cs-admin-table th { padding: 14px 18px; font-size: 12px; font-weight: 700; color: #64748b; text-transform: uppercase; letter-spacing: 0.5px; text-align: left; border-bottom: 1px solid #e5e7eb; }
        .cs-admin-table td { padding: 14px 18px; font-size: 14px; color: #374151; border-bottom: 1px solid #f1f5f9; }
        .cs-admin-table tbody tr { transition: background 0.15s; }
        .cs-admin-table tbody tr:hover { background: #fefce8; }
        .cs-admin-table__id { color: #9ca3af; font-size: 13px; font-weight: 600; }
        .cs-admin-table__name { font-weight: 600; color: #1e293b; }
        .cs-admin-table__empty { text-align: center; padding: 48px 0 !important; color: #94a3b8; }
        .cs-admin-table__empty p { margin-top: 12px; font-size: 14px; }
        .cs-admin-status-badge { display: inline-flex; align-items: center; gap: 6px; padding: 4px 12px; border-radius: 50px; font-size: 12px; font-weight: 600; }
        .cs-admin-status-badge__dot { width: 7px; height: 7px; border-radius: 50%; }
        .cs-admin-action-btn { display: inline-flex; align-items: center; justify-content: center; height: 34px; min-width: 110px; padding: 0 14px; border-radius: 10px; font-size: 12px; font-weight: 600; border: none; cursor: pointer; transition: all 0.2s; color: white; }
        .cs-admin-action-btn--ban { background: #ef4444; }
        .cs-admin-action-btn--ban:hover { background: #dc2626; transform: translateY(-1px); }

        .cs-admin-modal-overlay { position: fixed; inset: 0; z-index: 50; background: rgba(0,0,0,0.5); backdrop-filter: blur(4px); display: flex; align-items: center; justify-content: center; padding: 24px; }
        .cs-admin-modal { width: 100%; box-sizing: border-box; background: white; border-radius: 20px; padding: 32px; animation: cs-fadeInUp 0.3s ease-out; box-shadow: 0 20px 60px rgba(0,0,0,0.2); }
        .cs-admin-modal__title { font-size: 20px; font-weight: 700; color: #1e293b; margin: 0 0 8px 0; }
        .cs-admin-modal__desc { font-size: 14px; color: #64748b; margin-bottom: 24px; line-height: 1.6; }
        .cs-admin-modal__actions { display: flex; gap: 12px; }
        .cs-admin-modal__btn { flex: 1; height: 44px; border-radius: 12px; font-size: 14px; font-weight: 600; cursor: pointer; transition: all 0.2s; border: none; display: flex; align-items: center; justify-content: center; }
        .cs-admin-modal__btn--cancel { background: #f1f5f9; color: #475569; }
        .cs-admin-modal__btn--cancel:hover { background: #e2e8f0; }
        .cs-admin-modal__btn--danger { background: #ef4444; color: white; }
        .cs-admin-modal__btn--danger:hover:not(:disabled) { background: #dc2626; }
        .cs-admin-modal__btn:disabled { opacity: 0.5; cursor: not-allowed; }

        @keyframes cs-spin-slow { 100% { transform: rotate(360deg); } }
        @keyframes cs-fadeInUp { from { opacity: 0; transform: translateY(20px); } to { opacity: 1; transform: translateY(0); } }
      `}</style>
    </div>
  );
}
