import { useMemo, useState } from "react";
import { useNavigate } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { disputeApi } from "@/services/api";
import Pagination from "@/components/Pagination";

function formatDate(dateStr) {
  if (!dateStr) return "—";
  const s = String(dateStr);
  const d = new Date(String(s).replace("Z", ""));
  return `${String(d.getDate()).padStart(2, "0")}/${String(d.getMonth() + 1).padStart(2, "0")}/${d.getFullYear()} ${String(d.getHours()).padStart(2, "0")}:${String(d.getMinutes()).padStart(2, "0")}`;
}

function getStatusLabel(status) {
  switch (status) {
    case "WaitingOwnerEvidence": return "Chờ Owner phản hồi";
    case "PendingReview": return "Chờ xem xét";
    case "ResolvedRefund": return "Hoàn tiền tài xế";
    case "ResolvedPayout": return "Thanh toán chủ trạm";
    default: return status;
  }
}

function getStatusType(status) {
  switch (status) {
    case "WaitingOwnerEvidence": return "warning";
    case "PendingReview": return "info";
    case "ResolvedRefund": return "active";
    case "ResolvedPayout": return "purple";
    default: return "draft";
  }
}

export default function DisputeList() {
  const navigate = useNavigate();
  const [search, setSearch] = useState("");
  const [statusFilter, setStatusFilter] = useState("ALL");
  const [page, setPage] = useState(1);

  const { data: rawData, isLoading, error } = useQuery({
    queryKey: ["admin-disputes-all", statusFilter, page],
    queryFn: () => disputeApi.getAll(statusFilter, page, 20),
    refetchInterval: 30000,
  });

  // BE trả { total, page, pageSize, items } — phải unpack .items
  const disputes = rawData?.items ?? (Array.isArray(rawData) ? rawData : []);
  const totalCount = rawData?.totalCount ?? rawData?.total ?? disputes.length;

  const filtered = useMemo(() => {
    const keyword = search.trim().toLowerCase();
    return disputes.filter((d) => {
      if (!keyword) return true;
      return (
        d.createdByName?.toLowerCase().includes(keyword) ||
        d.reason?.toLowerCase().includes(keyword)
      );
    });
  }, [disputes, search]);

  if (isLoading) {
    return (
      <div className="cs-admin-page">
        <div style={{ textAlign: "center", paddingTop: 120 }}>
          <div className="cs-admin-table__spinner" style={{ margin: "0 auto 16px" }} />
          <p style={{ color: "#64748b", fontSize: 14 }}>Đang tải danh sách khiếu nại...</p>
        </div>
        <style>{styles}</style>
      </div>
    );
  }

  if (error) {
    return (
      <div className="cs-admin-page">
        <div style={{ textAlign: "center", paddingTop: 120 }}>
          <p style={{ color: "#ef4444", fontSize: 16, marginBottom: 16 }}> Lỗi tải dữ liệu: {error.message}</p>
        </div>
        <style>{styles}</style>
      </div>
    );
  }

  return (
    <div className="cs-admin-page">
      {/* Page Header */}
      <div className="cs-admin-page__header">
        <div>
          <h1 className="cs-admin-page__title">Quản lý khiếu nại (Tổng: {totalCount})</h1>
          <p className="cs-admin-page__subtitle">Xem xét và xử lý các khiếu nại từ tài xế</p>
        </div>
      </div>

      {/* Filter Bar */}
      <div className="cs-admin-filter">
        <div className="cs-admin-filter__search">
          <svg className="cs-admin-filter__search-icon" width="18" height="18" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
          </svg>
          <input
            type="text"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder="Tìm theo tên tài xế, lý do..."
            className="cs-admin-filter__input"
          />
        </div>
        <select
          value={statusFilter}
          onChange={(e) => setStatusFilter(e.target.value)}
          className="cs-admin-filter__select"
        >
          <option value="ALL">Tất cả trạng thái</option>
          <option value="WaitingOwnerEvidence">Chờ chủ trạm phản hồi</option>
          <option value="PendingReview">Chờ xem xét</option>
          <option value="ResolvedRefund">Hoàn tiền tài xế</option>
          <option value="ResolvedPayout">Thanh toán chủ trạm</option>
        </select>
        <button onClick={() => { setSearch(""); setStatusFilter("ALL"); setPage(1); }} className="cs-admin-filter__reset">
          <svg width="16" height="16" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
          </svg>
          Xóa bộ lọc
        </button>
      </div>

      {/* Table */}
      <div className="cs-admin-table-wrap">
        <table className="cs-admin-table">
          <thead>
            <tr>
              <th>ID</th>
              <th>Tài xế</th>
              <th>Lý do</th>
              <th>Trạng thái</th>
              <th>Ngày tạo</th>
              <th style={{ textAlign: "right" }}>Hành động</th>
            </tr>
          </thead>
          <tbody>
            {filtered.length === 0 ? (
              <tr>
                <td colSpan={7} className="cs-admin-table__empty">
                  <p>Không tìm thấy khiếu nại nào</p>
                </td>
              </tr>
            ) : (
              filtered.map((d) => {
                const statusType = getStatusType(d.status);
                return (
                  <tr key={d.id}>
                    <td className="cs-admin-table__id">#{d.id}</td>
                    <td>{d.createdByName}</td>
                    <td style={{ maxWidth: 200 }}>
                      <span style={{ display: "block", overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>
                        {d.reason}
                      </span>
                    </td>
                    <td>
                      <span className={`cs-admin-status-badge cs-admin-status-badge--${statusType}`}>
                        <span className="cs-admin-status-badge__dot" />
                        {getStatusLabel(d.status)}
                      </span>
                    </td>
                    <td style={{ color: "#64748b", fontSize: 13 }}>{formatDate(d.createdAt)}</td>
                    <td style={{ textAlign: "right" }}>
                      <button
                        onClick={() => navigate(`/admin/disputes/${d.id}`)}
                        className={`cs-admin-action-btn ${["PendingReview", "WaitingOwnerEvidence"].includes(d.status) ? "cs-admin-action-btn--review" : "cs-admin-action-btn--view"}`}
                        style={{ display: "inline-flex", alignItems: "center", gap: "6px" }}
                      >
                        {["PendingReview", "WaitingOwnerEvidence"].includes(d.status) ? (
                          <>
                            <svg fill="none" stroke="currentColor" strokeWidth="2" viewBox="0 0 24 24" width="14" height="14"><path strokeLinecap="round" strokeLinejoin="round" d="M15.232 5.232l3.536 3.536m-2.036-5.036a2.5 2.5 0 113.536 3.536L6.5 21.036H3v-3.572L16.732 3.732z" /></svg>
                            Xem xét
                          </>
                        ) : (
                          <>
                            <svg fill="none" stroke="currentColor" strokeWidth="2" viewBox="0 0 24 24" width="14" height="14"><path strokeLinecap="round" strokeLinejoin="round" d="M15 12a3 3 0 11-6 0 3 3 0 016 0z" /><path strokeLinecap="round" strokeLinejoin="round" d="M2.458 12C3.732 7.943 7.523 5 12 5c4.478 0 8.268 2.943 9.542 7-1.274 4.057-5.064 7-9.542 7-4.477 0-8.268-2.943-9.542-7z" /></svg>
                            Xem
                          </>
                        )}
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
            totalCount={totalCount}
            pageSize={20}
            onPageChange={(p) => setPage(p)}
          />
        </div>
      </div>

      <style>{styles}</style>
    </div>
  );
}

const styles = `
  .cs-admin-page {
    max-width: 1400px;
    width: 95%;
    margin: 0 auto;
    padding: 88px 0 40px;
  }
  @media (max-width: 768px) {
    .cs-admin-page { width: 100%; padding: 80px 16px 40px; }
  }
  .cs-admin-page__header { margin-bottom: 28px; }
  .cs-admin-page__title {
    font-size: 26px;
    font-weight: 800;
    color: #1e293b;
    letter-spacing: -0.5px;
  }
  .cs-admin-page__subtitle {
    font-size: 14px;
    color: #64748b;
    margin-top: 4px;
  }

  /* Stats */
  .cs-admin-stats {
    display: grid;
    grid-template-columns: repeat(4, 1fr);
    gap: 20px;
    margin-bottom: 24px;
  }
  @media (max-width: 900px) {
    .cs-admin-stats { grid-template-columns: repeat(2, 1fr); }
  }
  @media (max-width: 400px) {
    .cs-admin-stats { grid-template-columns: 1fr; }
  }
  .cs-admin-stat-card {
    background: white;
    border: 1px solid rgba(0,0,0,0.06);
    border-radius: 16px;
    padding: 20px 24px;
    display: flex;
    align-items: center;
    gap: 16px;
    transition: all 0.3s;
  }
  .cs-admin-stat-card:hover {
    transform: translateY(-2px);
    box-shadow: 0 8px 24px rgba(0,0,0,0.08);
  }
  .cs-admin-stat-card__icon {
    width: 48px;
    height: 48px;
    border-radius: 14px;
    display: flex;
    align-items: center;
    justify-content: center;
    flex-shrink: 0;
  }
  .cs-admin-stat-card__icon--total { background: #eff6ff; color: #3b82f6; }
  .cs-admin-stat-card__icon--warning { background: #fff7ed; color: #f97316; }
  .cs-admin-stat-card__icon--info { background: #eff6ff; color: #3b82f6; }
  .cs-admin-stat-card__label { font-size: 13px; color: #64748b; }
  .cs-admin-stat-card__value { font-size: 28px; font-weight: 800; color: #1e293b; margin-top: 2px; }

  /* Filter */
  .cs-admin-filter {
    background: white;
    border: 1px solid rgba(0,0,0,0.06);
    border-radius: 16px;
    padding: 16px 20px;
    margin-bottom: 20px;
    display: flex;
    gap: 12px;
    align-items: center;
    flex-wrap: wrap;
  }
  @media (max-width: 600px) {
    .cs-admin-filter { flex-direction: column; align-items: stretch; }
    .cs-admin-filter__search { min-width: unset; }
    .cs-admin-filter__select, .cs-admin-filter__reset { width: 100%; }
  }
  .cs-admin-filter__search {
    flex: 1;
    min-width: 200px;
    position: relative;
  }
  .cs-admin-filter__search-icon {
    position: absolute;
    left: 14px;
    top: 50%;
    transform: translateY(-50%);
    color: #9ca3af;
  }
  .cs-admin-filter__input {
    width: 100%;
    height: 42px;
    border: 1.5px solid #e5e7eb;
    border-radius: 12px;
    padding: 0 16px 0 40px;
    font-size: 14px;
    outline: none;
    transition: all 0.2s;
    background: #f9fafb;
    box-sizing: border-box;
  }
  .cs-admin-filter__input:focus {
    border-color: #f97316;
    background: white;
    box-shadow: 0 0 0 3px rgba(249,115,22,0.1);
  }
  .cs-admin-filter__select {
    height: 42px;
    border: 1.5px solid #e5e7eb;
    border-radius: 12px;
    padding: 0 14px;
    font-size: 14px;
    background: #f9fafb;
    cursor: pointer;
    outline: none;
    transition: border-color 0.2s;
  }
  .cs-admin-filter__select:focus { border-color: #f97316; }
  .cs-admin-filter__reset {
    height: 42px;
    padding: 0 18px;
    border: 1.5px solid #e5e7eb;
    border-radius: 12px;
    background: white;
    font-size: 13px;
    font-weight: 500;
    color: #64748b;
    cursor: pointer;
    display: flex;
    align-items: center;
    gap: 6px;
    transition: all 0.2s;
  }
  .cs-admin-filter__reset:hover { background: #f9fafb; border-color: #d1d5db; }

  /* Table */
  .cs-admin-table-wrap {
    background: white;
    border: 1px solid rgba(0,0,0,0.06);
    border-radius: 16px;
    overflow-x: auto;
    -webkit-overflow-scrolling: touch;
  }
  .cs-admin-table__spinner {
    width: 28px; height: 28px;
    border: 3px solid #f3f4f6;
    border-top-color: #f97316;
    border-radius: 50%;
    animation: cs-spin 0.8s linear infinite;
  }
  @keyframes cs-spin { to { transform: rotate(360deg); } }
  .cs-admin-table {
    width: 100%;
    min-width: 900px;
    border-collapse: collapse;
  }
  .cs-admin-table thead {
    background: linear-gradient(180deg, #f8fafc, #f1f5f9);
  }
  .cs-admin-table th {
    padding: 14px 18px;
    font-size: 12px;
    font-weight: 700;
    color: #64748b;
    text-transform: uppercase;
    letter-spacing: 0.5px;
    text-align: left;
    border-bottom: 1px solid #e5e7eb;
  }
  .cs-admin-table td {
    padding: 14px 18px;
    font-size: 14px;
    color: #374151;
    border-bottom: 1px solid #f1f5f9;
  }
  .cs-admin-table tbody tr { transition: background 0.15s; }
  .cs-admin-table tbody tr:hover { background: #fefce8; }
  .cs-admin-table__id { color: #9ca3af; font-size: 13px; }
  .cs-admin-table__name { font-weight: 600; color: #1e293b; }
  .cs-admin-table__empty {
    text-align: center;
    padding: 48px 0 !important;
    color: #94a3b8;
  }
  .cs-admin-table__empty p { margin-top: 12px; font-size: 14px; }

  /* Status Badges */
  .cs-admin-status-badge {
    display: inline-flex;
    align-items: center;
    gap: 6px;
    padding: 4px 12px;
    border-radius: 50px;
    font-size: 12px;
    font-weight: 600;
    white-space: nowrap;
  }
  .cs-admin-status-badge__dot {
    width: 7px; height: 7px; border-radius: 50%;
  }
  .cs-admin-status-badge--pending { background: #fffbeb; color: #f59e0b; }
  .cs-admin-status-badge--pending .cs-admin-status-badge__dot { background: #f59e0b; }
  .cs-admin-status-badge--warning { background: #fff7ed; color: #f97316; }
  .cs-admin-status-badge--warning .cs-admin-status-badge__dot { background: #f97316; }
  .cs-admin-status-badge--info { background: #eff6ff; color: #3b82f6; }
  .cs-admin-status-badge--info .cs-admin-status-badge__dot { background: #3b82f6; }
  .cs-admin-status-badge--active { background: #f0fdf4; color: #16a34a; }
  .cs-admin-status-badge--active .cs-admin-status-badge__dot { background: #16a34a; }
  .cs-admin-status-badge--purple { background: #f5f3ff; color: #7c3aed; }
  .cs-admin-status-badge--purple .cs-admin-status-badge__dot { background: #7c3aed; }
  .cs-admin-status-badge--draft { background: #f1f5f9; color: #64748b; }
  .cs-admin-status-badge--draft .cs-admin-status-badge__dot { background: #94a3b8; }

  /* Action Button */
  .cs-admin-action-btn {
    display: inline-flex;
    align-items: center;
    justify-content: center;
    height: 34px;
    min-width: 90px;
    padding: 0 14px;
    border-radius: 10px;
    font-size: 12px;
    font-weight: 600;
    border: none;
    cursor: pointer;
    transition: all 0.2s;
    color: white;
  }
  .cs-admin-action-btn--view { background: #3b82f6; }
  .cs-admin-action-btn--view:hover { background: #2563eb; transform: translateY(-1px); }
  .cs-admin-action-btn--review { background: #f97316; }
  .cs-admin-action-btn--review:hover { background: #ea580c; transform: translateY(-1px); }
`;
