import { useMemo, useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { instance } from "@/lib/httpRequest";
import { showToast } from "@/components/Toast";

/* ─── API helpers ─── */
const adminStationApi = {
  getPending: async () => {
    const { data } = await instance.get("/admin/stations/pending");
    return data;
  },
  review: async (stationId, body) => {
    const { data } = await instance.post(`/admin/stations/${stationId}/review`, body);
    return data;
  },
};

function formatDate(dateStr) {
  if (!dateStr) return "—";
  const d = new Date(dateStr);
  return `${String(d.getDate()).padStart(2, "0")}/${String(d.getMonth() + 1).padStart(2, "0")}/${d.getFullYear()}`;
}

function getStatusLabel(status) {
  switch (status) {
    case "PendingApproval": return "Chờ duyệt";
    case "Approved": return "Đã duyệt";
    case "Rejected": return "Từ chối";
    case "Draft": return "Bản nháp";
    default: return status;
  }
}

export default function ApproveStation() {
  const queryClient = useQueryClient();

  const [search, setSearch] = useState("");
  const [statusFilter, setStatusFilter] = useState("ALL");
  const [confirmAction, setConfirmAction] = useState(null);
  const [adminNote, setAdminNote] = useState("");

  const { data: stations = [], isLoading, error } = useQuery({
    queryKey: ["admin-stations-pending"],
    queryFn: adminStationApi.getPending,
  });

  const reviewMutation = useMutation({
    mutationFn: ({ stationId, isApproved, adminNote }) =>
      adminStationApi.review(stationId, { isApproved, adminNote: adminNote || null }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["admin-stations-pending"] });
      setConfirmAction(null);
      setAdminNote("");
    },
    onError: (err) => {
      const msg = err?.response?.data?.error || err?.message || "Lỗi không xác định";
      showToast.error("Lỗi: " + msg);
    },
  });

  const filtered = useMemo(() => {
    const keyword = search.trim().toLowerCase();
    return stations.filter((s) => {
      const matchSearch =
        !keyword ||
        s.name?.toLowerCase().includes(keyword) ||
        s.address?.toLowerCase().includes(keyword);
      const matchStatus =
        statusFilter === "ALL" || s.approvalStatus === statusFilter;
      return matchSearch && matchStatus;
    });
  }, [stations, search, statusFilter]);

  const summary = useMemo(() => {
    return stations.reduce(
      (acc, s) => {
        acc.total += 1;
        if (s.approvalStatus === "PendingApproval") acc.pending += 1;
        else if (s.approvalStatus === "Approved") acc.approved += 1;
        else if (s.approvalStatus === "Rejected") acc.rejected += 1;
        return acc;
      },
      { total: 0, pending: 0, approved: 0, rejected: 0 }
    );
  }, [stations]);

  function askReview(station, isApproved) {
    setConfirmAction({ station, isApproved });
    setAdminNote("");
  }

  function confirmReview() {
    if (!confirmAction) return;
    reviewMutation.mutate({
      stationId: confirmAction.station.id,
      isApproved: confirmAction.isApproved,
      adminNote,
    });
  }

  function resetFilter() {
    setSearch("");
    setStatusFilter("ALL");
  }

  if (isLoading) {
    return (
      <div className="cs-admin-page">
        <div style={{ textAlign: "center", paddingTop: 120 }}>
          <div className="cs-admin-table__spinner" style={{ margin: "0 auto 16px" }} />
          <p style={{ color: "#64748b", fontSize: 14 }}>Đang tải danh sách trạm...</p>
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
          <button
            onClick={() => queryClient.invalidateQueries({ queryKey: ["admin-stations-pending"] })}
            className="cs-admin-action-btn cs-admin-action-btn--activate"
          >
            Thử lại
          </button>
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
          <h1 className="cs-admin-page__title">Duyệt trạm sạc</h1>
          <p className="cs-admin-page__subtitle">
            Quản trị viên có thể phê duyệt hoặc từ chối các yêu cầu đăng ký trạm sạc
          </p>
        </div>
      </div>

      {/* Stats Cards */}
      <div className="cs-admin-stats cs-admin-stats--4">
        <div className="cs-admin-stat-card">
          <div className="cs-admin-stat-card__icon cs-admin-stat-card__icon--total">
            <svg width="22" height="22" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 21V5a2 2 0 00-2-2H7a2 2 0 00-2 2v16m14 0h2m-2 0h-5m-9 0H3m2 0h5M9 7h1m-1 4h1m4-4h1m-1 4h1m-5 10v-5a1 1 0 011-1h2a1 1 0 011 1v5m-4 0h4" />
            </svg>
          </div>
          <div>
            <p className="cs-admin-stat-card__label">Tổng yêu cầu</p>
            <p className="cs-admin-stat-card__value">{summary.total}</p>
          </div>
        </div>
        <div className="cs-admin-stat-card">
          <div className="cs-admin-stat-card__icon cs-admin-stat-card__icon--pending">
            <svg width="22" height="22" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z" />
            </svg>
          </div>
          <div>
            <p className="cs-admin-stat-card__label">Chờ duyệt</p>
            <p className="cs-admin-stat-card__value" style={{ color: "#f59e0b" }}>{summary.pending}</p>
          </div>
        </div>
        <div className="cs-admin-stat-card">
          <div className="cs-admin-stat-card__icon cs-admin-stat-card__icon--active">
            <svg width="22" height="22" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z" />
            </svg>
          </div>
          <div>
            <p className="cs-admin-stat-card__label">Đã duyệt</p>
            <p className="cs-admin-stat-card__value" style={{ color: "#16a34a" }}>{summary.approved}</p>
          </div>
        </div>
        <div className="cs-admin-stat-card">
          <div className="cs-admin-stat-card__icon cs-admin-stat-card__icon--banned">
            <svg width="22" height="22" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M10 14l2-2m0 0l2-2m-2 2l-2-2m2 2l2 2m7-2a9 9 0 11-18 0 9 9 0 0118 0z" />
            </svg>
          </div>
          <div>
            <p className="cs-admin-stat-card__label">Từ chối</p>
            <p className="cs-admin-stat-card__value" style={{ color: "#dc2626" }}>{summary.rejected}</p>
          </div>
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
            placeholder="Tìm theo tên trạm hoặc địa chỉ..."
            className="cs-admin-filter__input"
          />
        </div>
        <select
          value={statusFilter}
          onChange={(e) => setStatusFilter(e.target.value)}
          className="cs-admin-filter__select"
        >
          <option value="ALL">Tất cả</option>
          <option value="PendingApproval">Chờ duyệt</option>
          <option value="Approved">Đã duyệt</option>
          <option value="Rejected">Từ chối</option>
        </select>
        <button onClick={resetFilter} className="cs-admin-filter__reset">
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
              <th>Tên trạm</th>
              <th>Địa chỉ</th>
              <th>Số ổ sạc</th>
              <th>Ngày tạo</th>
              <th>Trạng thái</th>
              <th style={{ textAlign: "right" }}>Hành động</th>
            </tr>
          </thead>
          <tbody>
            {filtered.length === 0 ? (
              <tr>
                <td colSpan={7} className="cs-admin-table__empty">
                  <p>Không tìm thấy yêu cầu nào</p>
                </td>
              </tr>
            ) : (
              filtered.map((s) => {
                const isPending = s.approvalStatus === "PendingApproval";
                return (
                  <tr key={s.id}>
                    <td className="cs-admin-table__id">{s.id}</td>
                    <td className="cs-admin-table__name">{s.name}</td>
                    <td>{s.address}</td>
                    <td>{s.chargingSlots?.length || 0}</td>
                    <td>{formatDate(s.createdAt)}</td>
                    <td>
                      <span className={`cs-admin-status-badge cs-admin-status-badge--${s.approvalStatus === "PendingApproval" ? "pending" : s.approvalStatus === "Approved" ? "active" : s.approvalStatus === "Rejected" ? "banned" : "draft"}`}>
                        <span className="cs-admin-status-badge__dot" />
                        {getStatusLabel(s.approvalStatus)}
                      </span>
                    </td>
                    <td style={{ textAlign: "right" }}>
                      <div style={{ display: "flex", justifyContent: "flex-end", gap: 8 }}>
                        <button
                          disabled={!isPending}
                          onClick={() => askReview(s, true)}
                          className={`cs-admin-action-btn ${isPending ? "cs-admin-action-btn--activate" : "cs-admin-action-btn--disabled"}`}
                        >
                          Phê duyệt
                        </button>
                        <button
                          disabled={!isPending}
                          onClick={() => askReview(s, false)}
                          className={`cs-admin-action-btn ${isPending ? "cs-admin-action-btn--ban" : "cs-admin-action-btn--disabled"}`}
                        >
                          Từ chối
                        </button>
                      </div>
                    </td>
                  </tr>
                );
              })
            )}
          </tbody>
        </table>
      </div>

      {/* Confirm Modal */}
      {confirmAction && (
        <div className="cs-admin-modal-overlay">
          <div className="cs-admin-modal">
            <div className="cs-admin-modal__icon">
              {confirmAction.isApproved ? "✅" : "🚫"}
            </div>
            <h2 className="cs-admin-modal__title">Xác nhận thao tác</h2>
            <p className="cs-admin-modal__desc">
              Bạn có chắc chắn muốn{" "}
              <strong>{confirmAction.isApproved ? "phê duyệt" : "từ chối"}</strong>{" "}
              trạm "<strong>{confirmAction.station.name}</strong>" không?
            </p>

            <div style={{ textAlign: "left", marginBottom: 20 }}>
              <label style={{ display: "block", fontSize: 13, fontWeight: 600, color: "#374151", marginBottom: 6 }}>
                Ghi chú {!confirmAction.isApproved && <span style={{ color: "#ef4444" }}>*</span>}
              </label>
              <textarea
                value={adminNote}
                onChange={(e) => setAdminNote(e.target.value)}
                placeholder={confirmAction.isApproved ? "Ghi chú (tùy chọn)" : "Lý do từ chối..."}
                rows={3}
                style={{
                  width: "100%", padding: "10px 14px", borderRadius: 12,
                  border: "1.5px solid #e5e7eb", fontSize: 14, outline: "none",
                  resize: "vertical", boxSizing: "border-box",
                  transition: "border-color 0.2s",
                }}
                onFocus={(e) => e.target.style.borderColor = "#f97316"}
                onBlur={(e) => e.target.style.borderColor = "#e5e7eb"}
              />
            </div>

            <div className="cs-admin-modal__actions">
              <button
                onClick={() => { setConfirmAction(null); setAdminNote(""); }}
                disabled={reviewMutation.isPending}
                className="cs-admin-modal__btn cs-admin-modal__btn--cancel"
              >
                Hủy
              </button>
              <button
                onClick={confirmReview}
                disabled={reviewMutation.isPending || (!confirmAction.isApproved && !adminNote.trim())}
                className={`cs-admin-modal__btn ${confirmAction.isApproved ? "cs-admin-modal__btn--success" : "cs-admin-modal__btn--danger"}`}
              >
                {reviewMutation.isPending ? "Đang xử lý..." : "Xác nhận"}
              </button>
            </div>
          </div>
        </div>
      )}

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
    grid-template-columns: repeat(3, 1fr);
    gap: 20px;
    margin-bottom: 24px;
  }
  .cs-admin-stats--4 {
    grid-template-columns: repeat(4, 1fr);
  }
  @media (max-width: 900px) {
    .cs-admin-stats--4 { grid-template-columns: repeat(2, 1fr); }
  }
  @media (max-width: 640px) {
    .cs-admin-stats, .cs-admin-stats--4 { grid-template-columns: 1fr; }
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
  .cs-admin-stat-card__icon--pending { background: #fffbeb; color: #f59e0b; }
  .cs-admin-stat-card__icon--active { background: #f0fdf4; color: #16a34a; }
  .cs-admin-stat-card__icon--banned { background: #fef2f2; color: #dc2626; }
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
    overflow: hidden;
    overflow-x: auto;
    position: relative;
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
  .cs-admin-table tbody tr {
    transition: background 0.15s;
  }
  .cs-admin-table tbody tr:hover {
    background: #fefce8;
  }
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
  }
  .cs-admin-status-badge__dot {
    width: 7px; height: 7px; border-radius: 50%;
  }
  .cs-admin-status-badge--pending {
    background: #fffbeb; color: #f59e0b;
  }
  .cs-admin-status-badge--pending .cs-admin-status-badge__dot { background: #f59e0b; }
  .cs-admin-status-badge--active {
    background: #f0fdf4; color: #16a34a;
  }
  .cs-admin-status-badge--active .cs-admin-status-badge__dot { background: #16a34a; }
  .cs-admin-status-badge--banned {
    background: #fef2f2; color: #dc2626;
  }
  .cs-admin-status-badge--banned .cs-admin-status-badge__dot { background: #dc2626; }
  .cs-admin-status-badge--draft {
    background: #f1f5f9; color: #64748b;
  }
  .cs-admin-status-badge--draft .cs-admin-status-badge__dot { background: #94a3b8; }

  /* Action Buttons */
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
  .cs-admin-action-btn--ban { background: #ef4444; }
  .cs-admin-action-btn--ban:hover { background: #dc2626; transform: translateY(-1px); }
  .cs-admin-action-btn--activate { background: #22c55e; }
  .cs-admin-action-btn--activate:hover { background: #16a34a; transform: translateY(-1px); }
  .cs-admin-action-btn--disabled { background: #d1d5db; cursor: not-allowed; color: #9ca3af; }

  /* Modal */
  .cs-admin-modal-overlay {
    position: fixed;
    inset: 0;
    z-index: 50;
    background: rgba(0,0,0,0.5);
    backdrop-filter: blur(4px);
    display: flex;
    align-items: center;
    justify-content: center;
    padding: 24px;
  }
  .cs-admin-modal {
    width: 100%;
    max-width: 460px;
    background: white;
    border-radius: 20px;
    padding: 32px;
    text-align: center;
    animation: cs-fadeInUp 0.3s ease-out;
    box-shadow: 0 20px 60px rgba(0,0,0,0.2);
  }
  @keyframes cs-fadeInUp {
    from { opacity: 0; transform: translateY(20px); }
    to { opacity: 1; transform: translateY(0); }
  }
  .cs-admin-modal__icon { font-size: 40px; margin-bottom: 16px; }
  .cs-admin-modal__title {
    font-size: 20px;
    font-weight: 700;
    color: #1e293b;
    margin-bottom: 8px;
  }
  .cs-admin-modal__desc { font-size: 14px; color: #64748b; margin-bottom: 24px; line-height: 1.6; }
  .cs-admin-modal__actions { display: flex; gap: 12px; }
  .cs-admin-modal__btn {
    flex: 1;
    height: 44px;
    border-radius: 12px;
    font-size: 14px;
    font-weight: 600;
    cursor: pointer;
    transition: all 0.2s;
    border: none;
  }
  .cs-admin-modal__btn--cancel {
    background: #f1f5f9;
    color: #374151;
  }
  .cs-admin-modal__btn--cancel:hover { background: #e2e8f0; }
  .cs-admin-modal__btn--danger { background: #ef4444; color: white; }
  .cs-admin-modal__btn--danger:hover { background: #dc2626; }
  .cs-admin-modal__btn--success { background: #22c55e; color: white; }
  .cs-admin-modal__btn--success:hover { background: #16a34a; }
  .cs-admin-modal__btn:disabled { opacity: 0.6; cursor: not-allowed; }
`;