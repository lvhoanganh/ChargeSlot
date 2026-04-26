import { useEffect, useRef, useState, Fragment } from "react";
import { useQuery } from "@tanstack/react-query";
import { useAdminAccountStore } from "@/stores/adminAccountStore";
import { showToast } from "@/components/Toast";
import { formatDateVN } from "@/utils/dateVN";
import { BanStatusBadge } from "@/components/BanStatusBadge";
import { adminAccountsDetailsApi, bookingApi } from "@/services/api";
import UserProfileModal from "@/components/UserProfileModal";

const ROLE_OPTIONS = [
  { label: "Tất cả", value: "ALL" },
  { label: "Tài xế", value: "Driver" },
  { label: "Chủ trạm", value: "Owner" },
  { label: "Quản trị viên", value: "Admin" },
];

const STATUS_OPTIONS = [
  { label: "Tất cả", value: "ALL" },
  { label: "Hoạt động", value: "ACTIVE" },
  { label: "Bị cấm", value: "BANNED" },
];

function formatDate(dateStr) {
  return formatDateVN(dateStr) || "—";
}

function maskPhone(phone) {
  if (!phone || phone.length < 6) return phone || "";
  return phone.slice(0, 3) + "***" + phone.slice(-3);
}

function getRoleLabel(role) {
  switch (role) {
    case "Driver": return "Tài xế";
    case "Owner": return "Chủ trạm";
    case "Admin": return "Quản trị viên";
    default: return role;
  }
}

function getStatusLabel(status) {
  switch (status) {
    case "ACTIVE": return "Hoạt động";
    case "BANNED": return "Bị cấm";
    default: return status;
  }
}

function getBookingStatusLabel(status) {
  switch (status) {
    case "WaitingOwner": return "Chờ chủ trạm duyệt";
    case "PendingPayment": return "Chờ thanh toán";
    case "Paid": return "Đã thanh toán";
    case "CheckedIn": return "Đang sạc";
    case "Completed": return "Hoàn thành";
    case "Cancelled": return "Đã hủy";
    case "Rejected": return "Bị từ chối";
    case "Expired": return "Hết hạn";
    default: return status;
  }
}

function getApprovalStatusLabel(status) {
  switch (status) {
    case "Draft": return "Bản nháp";
    case "PendingApproval": return "Chờ duyệt";
    case "Approved": return "Đã duyệt";
    case "Rejected": return "Bị từ chối";
    default: return status;
  }
}

function getOperationalStatusLabel(status) {
  switch (status) {
    case "Active": return "Đang hoạt động";
    case "Inactive": return "Phát hành/Tạm dừng";
    case "Maintenance": return "Bảo trì";
    default: return status;
  }
}

export default function ManageUser() {
  const [search, setSearch] = useState("");
  const [role, setRole] = useState("ALL");
  const [status, setStatus] = useState("ALL");
  const [page, setPage] = useState(1);
  const pageSize = 10;
  const [confirmTarget, setConfirmTarget] = useState(null);
  const [toggling, setToggling] = useState(false);
  const [banReason, setBanReason] = useState("");
  const debounceRef = useRef(null);

  const [viewProfileTarget, setViewProfileTarget] = useState(null); // {id, role, fullName}

  const {
    users,
    totalItems,
    summary,
    loading,
    error,
    fetchUsers,
    fetchStatistics,
    toggleBan,
  } = useAdminAccountStore();

  useEffect(() => {
    fetchUsers(search, role, status, page, pageSize);
  }, [role, status, page, pageSize]);

  useEffect(() => {
    clearTimeout(debounceRef.current);
    debounceRef.current = setTimeout(() => {
      setPage(1);
      fetchUsers(search, role, status, 1, pageSize);
    }, 400);
    return () => clearTimeout(debounceRef.current);
  }, [search]);

  useEffect(() => {
    fetchStatistics();
  }, []);

  const totalPages = Math.max(1, Math.ceil(totalItems / pageSize));
  const currentPage = Math.min(page, totalPages);

  function resetFilter() {
    setSearch("");
    setRole("ALL");
    setStatus("ALL");
    setPage(1);
  }

  function askToggle(user) {
    if (user.role === "Admin") return;
    setBanReason("");
    setConfirmTarget(user);
  }

  async function confirmToggleBan() {
    if (!confirmTarget) return;
    setToggling(true);
    try {
      await toggleBan(confirmTarget.id, banReason);
      await Promise.all([
        fetchUsers(search, role, status, page, pageSize),
        fetchStatistics(),
      ]);
    } catch (err) {
      showToast.error(
        err?.response?.data?.message || err?.message || "Thao tác thất bại."
      );
    } finally {
      setToggling(false);
      setConfirmTarget(null);
    }
  }

  return (
    <div className="cs-admin-page">
      {/* Page Header */}
      <div className="cs-admin-page__header">
        <div>
          <h1 className="cs-admin-page__title">Quản lý người dùng</h1>
          <p className="cs-admin-page__subtitle">
            Quản trị viên có thể kích hoạt hoặc vô hiệu hóa tài khoản của người dùng
          </p>
        </div>
      </div>

      {/* Stats Cards */}
      <div className="cs-admin-stats">
        {/* Row 1 */}
        <div className="cs-admin-stat-card">
          <div className="cs-admin-stat-card__icon cs-admin-stat-card__icon--total">
            <svg width="22" height="22" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0z" />
            </svg>
          </div>
          <div>
            <p className="cs-admin-stat-card__label">Tổng số tài khoản</p>
            <p className="cs-admin-stat-card__value">{summary.total || 0}</p>
          </div>
        </div>
        <div className="cs-admin-stat-card">
          <div className="cs-admin-stat-card__icon cs-admin-stat-card__icon--active">
            <svg width="22" height="22" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z" />
            </svg>
          </div>
          <div>
            <p className="cs-admin-stat-card__label">Hoạt động</p>
            <p className="cs-admin-stat-card__value" style={{ color: "#16a34a" }}>{summary.active || 0}</p>
          </div>
        </div>
        <div className="cs-admin-stat-card">
          <div className="cs-admin-stat-card__icon cs-admin-stat-card__icon--banned">
            <svg width="22" height="22" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M18.364 18.364A9 9 0 005.636 5.636m12.728 12.728A9 9 0 015.636 5.636m12.728 12.728L5.636 5.636" />
            </svg>
          </div>
          <div>
            <p className="cs-admin-stat-card__label">Vô hiệu hóa</p>
            <p className="cs-admin-stat-card__value" style={{ color: "#dc2626" }}>{summary.banned || 0}</p>
          </div>
        </div>

        {/* Row 2: Roles */}
        <div className="cs-admin-stat-card">
          <div className="cs-admin-stat-card__icon" style={{ background: "#eff6ff", color: "#2563eb" }}>
            <svg width="22" height="22" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 21V5a2 2 0 00-2-2H7a2 2 0 00-2 2v16m14 0h2m-2 0h-5m-9 0H3m2 0h5M9 7h1m-1 4h1m4-4h1m-1 4h1m-5 10v-5a1 1 0 011-1h2a1 1 0 011 1v5m-4 0h4" />
            </svg>
          </div>
          <div>
            <p className="cs-admin-stat-card__label">Chủ trạm</p>
            <p className="cs-admin-stat-card__value" style={{ color: "#2563eb" }}>{summary.totalOwners || 0}</p>
          </div>
        </div>
        <div className="cs-admin-stat-card">
          <div className="cs-admin-stat-card__icon" style={{ background: "#fff7ed", color: "#ea580c" }}>
            <svg width="22" height="22" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M8 7h12m0 0l-4-4m4 4l-4 4m0 6H4m0 0l4 4m-4-4l4-4" />
            </svg>
          </div>
          <div>
            <p className="cs-admin-stat-card__label">Tài xế</p>
            <p className="cs-admin-stat-card__value" style={{ color: "#ea580c" }}>{summary.totalDrivers || 0}</p>
          </div>
        </div>
        <div className="cs-admin-stat-card">
          <div className="cs-admin-stat-card__icon" style={{ background: "#f5f3ff", color: "#7c3aed" }}>
            <svg width="22" height="22" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 12l2 2 4-4m5.618-4.016A11.955 11.955 0 0112 2.944a11.955 11.955 0 01-8.618 3.04A12.02 12.02 0 003 9c0 5.591 3.824 10.29 9 11.622 5.176-1.332 9-6.03 9-11.622 0-1.042-.133-2.052-.382-3.016z" />
            </svg>
          </div>
          <div>
            <p className="cs-admin-stat-card__label">Admin</p>
            <p className="cs-admin-stat-card__value" style={{ color: "#7c3aed" }}>{summary.totalAdmins || 0}</p>
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
            placeholder="Tìm theo tên hoặc số điện thoại..."
            className="cs-admin-filter__input"
          />
        </div>
        <select
          value={role}
          onChange={(e) => { setRole(e.target.value); setPage(1); }}
          className="cs-admin-filter__select"
        >
          {ROLE_OPTIONS.map((item) => (
            <option key={item.value} value={item.value}>{item.label}</option>
          ))}
        </select>
        <select
          value={status}
          onChange={(e) => { setStatus(e.target.value); setPage(1); }}
          className="cs-admin-filter__select"
        >
          {STATUS_OPTIONS.map((item) => (
            <option key={item.value} value={item.value}>{item.label}</option>
          ))}
        </select>
        <button onClick={resetFilter} className="cs-admin-filter__reset">
          <svg width="16" height="16" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
          </svg>
          Xóa bộ lọc
        </button>
      </div>

      {error && (
        <div className="cs-auth-error" style={{ marginBottom: 16 }}>{error}</div>
      )}

      {/* Table */}
      <div className="cs-admin-table-wrap">
        {loading && (
          <div className="cs-admin-table__loading">
            <div className="cs-admin-table__spinner" />
            <span>Đang tải...</span>
          </div>
        )}
        <table className="cs-admin-table">
          <thead>
            <tr>
              <th>STT</th>
              <th>Họ tên</th>
              <th>Số điện thoại</th>
              <th>Vai trò</th>
              <th>Trạng thái</th>
              <th>Vi phạm (AI)</th>
              <th>Ngày tạo</th>
              <th style={{ textAlign: "right" }}>Hành động</th>
            </tr>
          </thead>
          <tbody>
            {users.length === 0 && !loading ? (
              <tr>
                <td colSpan={8} className="cs-admin-table__empty">
                  <svg width="40" height="40" fill="none" stroke="#cbd5e1" viewBox="0 0 24 24">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.5} d="M20 13V6a2 2 0 00-2-2H6a2 2 0 00-2 2v7m16 0v5a2 2 0 01-2 2H6a2 2 0 01-2-2v-5m16 0h-2.586a1 1 0 00-.707.293l-2.414 2.414a1 1 0 01-.707.293h-3.172a1 1 0 01-.707-.293l-2.414-2.414A1 1 0 006.586 13H4" />
                  </svg>
                  <p>Không tìm thấy người dùng</p>
                </td>
              </tr>
            ) : (
              users.map((u, index) => {
                const isAdmin = u.role === "Admin";
                const isBanned = u.status === "BANNED" || !!u.bannedUntil;
                return (
                  <tr key={u.id}>
                    {/* <td className="cs-admin-table__id">{u.id}</td> */}
                    <td className="cs-admin-table__id">{index + 1}</td>
                    <td className="cs-admin-table__name">{u.fullName}</td>
                    <td>{maskPhone(u.phoneNumber)}</td>
                    <td>
                      <span className={`cs-admin-role-badge cs-admin-role-badge--${u.role.toLowerCase()}`}>
                        {getRoleLabel(u.role)}
                      </span>
                    </td>
                    <td>
                      <span className={`cs-admin-status-badge ${u.status === "ACTIVE" ? "cs-admin-status-badge--active" : "cs-admin-status-badge--banned"}`}>
                        <span className="cs-admin-status-badge__dot" />
                        {getStatusLabel(u.status)}
                      </span>
                    </td>
                    <td>
                      <BanStatusBadge banCount={u.banCount ?? 0} bannedUntil={u.bannedUntil ?? null} />
                    </td>
                    <td>{formatDate(u.createdAt)}</td>
                    <td style={{ textAlign: "right" }}>
                      <div style={{ display: "flex", flexDirection: "column", alignItems: "flex-end", gap: 6 }}>
                        {/* Biểu mẫu chi tiết */}
                        {(u.role === "Driver" || u.role === "Owner") && (
                          <button
                            onClick={() => setViewProfileTarget({ id: u.id, role: u.role, fullName: u.fullName })}
                            className="cs-admin-action-btn"
                            style={{ background: "#eff6ff", color: "#3b82f6", height: 28 }}
                          >
                            Chi Tiết
                          </button>
                        )}
                        {/* Toggle Switch */}
                        {isAdmin ? (
                          <span style={{ fontSize: 12, color: "#9ca3af", fontStyle: "italic", marginTop: 4 }}>Không khả dụng</span>
                        ) : (
                          <label
                            className="cs-toggle-switch"
                            title={!isBanned ? "Đang hoạt động — nhấn để vô hiệu hóa" : "Đang bị khoá — nhấn để kích hoạt (ân xá)"}
                          >
                            <input
                              type="checkbox"
                              checked={!isBanned}
                              onChange={() => askToggle(u)}
                            />
                            <span className="cs-toggle-switch__track">
                              <span className="cs-toggle-switch__thumb" />
                            </span>
                            <span className="cs-toggle-switch__label">
                              {!isBanned ? "Hoạt động" : "Bị khoá"}
                            </span>
                          </label>
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

      {/* Pagination */}
      <div className="cs-admin-pagination">
        <p className="cs-admin-pagination__info">
          Hiển thị {(currentPage - 1) * pageSize + (users.length ? 1 : 0)} -{" "}
          {(currentPage - 1) * pageSize + users.length} / {totalItems}
        </p>
        <div className="cs-admin-pagination__buttons">
          <button
            onClick={() => setPage((p) => Math.max(1, p - 1))}
            disabled={currentPage <= 1}
            className="cs-admin-pagination__btn"
          >
            ← Trước
          </button>
          {Array.from({ length: totalPages }, (_, i) => i + 1).map((p) => (
            <button
              key={p}
              onClick={() => setPage(p)}
              className={`cs-admin-pagination__btn ${p === currentPage ? "cs-admin-pagination__btn--active" : ""}`}
            >
              {p}
            </button>
          ))}
          <button
            onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
            disabled={currentPage >= totalPages}
            className="cs-admin-pagination__btn"
          >
            Sau →
          </button>
        </div>
      </div>

      {/* Confirm Modal */}
      {confirmTarget && (
        <div className="cs-admin-modal-overlay">
          <div className="cs-admin-modal">
            <div className="cs-admin-modal__icon">
              {!(confirmTarget.status === "BANNED" || !!confirmTarget.bannedUntil) ? "" : ""}
            </div>
            <h2 className="cs-admin-modal__title">Xác nhận thao tác</h2>
            <p className="cs-admin-modal__desc">
              Bạn có chắc chắn muốn{" "}
              <strong>{!(confirmTarget.status === "BANNED" || !!confirmTarget.bannedUntil) ? "tạm khoá" : "mở khoá (ân xá)"}</strong>{" "}
              tài khoản <strong>{confirmTarget.fullName}</strong> không?
            </p>
            {!(confirmTarget.status === "BANNED" || !!confirmTarget.bannedUntil) && (
              <textarea
                value={banReason}
                onChange={(e) => setBanReason(e.target.value)}
                placeholder="Nhập lý do khóa (bắt buộc)..."
                className="cs-admin-modal__textarea mb-6"
                rows={3}
              />
            )}
            <div className="cs-admin-modal__actions">
              <button
                onClick={() => setConfirmTarget(null)}
                disabled={toggling}
                className="cs-admin-modal__btn cs-admin-modal__btn--cancel"
              >
                Hủy
              </button>
              <button
                onClick={confirmToggleBan}
                disabled={toggling}
                className={`cs-admin-modal__btn ${!(confirmTarget.status === "BANNED" || !!confirmTarget.bannedUntil) ? "cs-admin-modal__btn--danger" : "cs-admin-modal__btn--success"}`}
              >
                {toggling ? "Đang xử lý..." : "Xác nhận"}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Profile Detail Modal */}
      {viewProfileTarget && (
        <UserProfileModal
          user={viewProfileTarget}
          onClose={() => setViewProfileTarget(null)}
        />
      )}

      <style>{`
        .cs-admin-page {
          max-width: 1400px;
          width: 95%;
          margin: 0 auto;
          padding: 88px 0 40px;
        }
        @media (max-width: 768px) {
          .cs-admin-page {
            width: 100%;
            padding: 80px 16px 40px;
          }
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
        @media (max-width: 640px) {
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
          .cs-admin-filter {
            flex-direction: column;
            align-items: stretch;
          }
          .cs-admin-filter__search { min-width: unset; }
          .cs-admin-filter__select,
          .cs-admin-filter__reset { width: 100%; }
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
          overflow-y: hidden;
          position: relative;
          -webkit-overflow-scrolling: touch;
        }
        .cs-admin-table__loading {
          position: absolute;
          inset: 0;
          background: rgba(255,255,255,0.8);
          display: flex;
          flex-direction: column;
          align-items: center;
          justify-content: center;
          gap: 10px;
          z-index: 10;
          color: #64748b;
          font-size: 14px;
        }
        .cs-admin-table__spinner {
          width: 28px; height: 28px;
          border: 3px solid #f3f4f6;
          border-top-color: #f97316;
          border-radius: 50%;
          animation: cs-spin-slow 0.8s linear infinite;
        }
        .cs-admin-table {
          width: 100%;
          min-width: 1000px;
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

        /* Role & Status Badges */
        .cs-admin-role-badge {
          display: inline-block;
          padding: 3px 10px;
          border-radius: 8px;
          font-size: 12px;
          font-weight: 600;
        }
        .cs-admin-role-badge--driver { background: #fff7ed; color: #ea580c; }
        .cs-admin-role-badge--owner { background: #eff6ff; color: #2563eb; }
        .cs-admin-role-badge--admin { background: #f5f3ff; color: #7c3aed; }

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
        .cs-admin-status-badge--active {
          background: #f0fdf4; color: #16a34a;
        }
        .cs-admin-status-badge--active .cs-admin-status-badge__dot { background: #16a34a; }
        .cs-admin-status-badge--banned {
          background: #fef2f2; color: #dc2626;
        }
        .cs-admin-status-badge--banned .cs-admin-status-badge__dot { background: #dc2626; }

        /* Action Button */
        .cs-admin-action-btn {
          display: inline-flex;
          align-items: center;
          justify-content: center;
          height: 34px;
          min-width: 110px;
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
        .cs-admin-action-btn--unban { background: #7c3aed; }
        .cs-admin-action-btn--unban:hover { background: #6d28d9; transform: translateY(-1px); }

        /* Toggle Switch */
        .cs-toggle-switch {
          display: inline-flex;
          align-items: center;
          gap: 8px;
          cursor: pointer;
          user-select: none;
        }
        .cs-toggle-switch input { display: none; }
        .cs-toggle-switch__track {
          position: relative;
          width: 44px;
          height: 24px;
          background: #e5e7eb;
          border-radius: 99px;
          transition: background 0.25s;
          flex-shrink: 0;
        }
        .cs-toggle-switch input:checked + .cs-toggle-switch__track {
          background: linear-gradient(135deg, #22c55e, #16a34a);
        }
        .cs-toggle-switch__thumb {
          position: absolute;
          top: 3px;
          left: 3px;
          width: 18px;
          height: 18px;
          background: white;
          border-radius: 50%;
          box-shadow: 0 1px 4px rgba(0,0,0,0.2);
          transition: transform 0.25s cubic-bezier(0.34,1.56,0.64,1);
        }
        .cs-toggle-switch input:checked ~ .cs-toggle-switch__track .cs-toggle-switch__thumb {
          transform: translateX(20px);
        }
        .cs-toggle-switch__label {
          font-size: 12px;
          font-weight: 600;
          color: #64748b;
          min-width: 62px;
        }
        .cs-toggle-switch input:checked ~ .cs-toggle-switch__label { color: #16a34a; }

        /* Pagination */
        .cs-admin-pagination {
          margin-top: 20px;
          display: flex;
          align-items: center;
          justify-content: space-between;
          flex-wrap: wrap;
          gap: 12px;
        }
        .cs-admin-pagination__info { font-size: 13px; color: #64748b; }
        .cs-admin-pagination__buttons { display: flex; gap: 6px; }
        .cs-admin-pagination__btn {
          height: 36px;
          min-width: 36px;
          padding: 0 10px;
          border: 1.5px solid #e5e7eb;
          border-radius: 10px;
          background: white;
          font-size: 13px;
          font-weight: 500;
          color: #374151;
          cursor: pointer;
          transition: all 0.2s;
        }
        .cs-admin-pagination__btn:hover:not(:disabled) { background: #f9fafb; border-color: #d1d5db; }
        .cs-admin-pagination__btn:disabled { opacity: 0.4; cursor: not-allowed; }
        .cs-admin-pagination__btn--active {
          background: linear-gradient(135deg, #f97316, #ea580c);
          color: white;
          border-color: #f97316;
        }

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
          max-width: 420px;
          background: white;
          border-radius: 20px;
          padding: 32px;
          text-align: center;
          animation: cs-fadeInUp 0.3s ease-out;
          box-shadow: 0 20px 60px rgba(0,0,0,0.2);
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
          font-weight: 600;
          font-size: 14px;
          cursor: pointer;
          border: none;
          display: flex;
          align-items: center;
          justify-content: center;
          transition: all 0.2s;
        }
        .cs-admin-modal__textarea {
          width: 100%;
          border: 1.5px solid #e5e7eb;
          border-radius: 12px;
          padding: 12px 16px;
          font-size: 14px;
          outline: none;
          resize: vertical;
          background: #f9fafb;
          transition: all 0.2s;
        }
        .cs-admin-modal__textarea:focus {
          border-color: #f97316;
          background: white;
          box-shadow: 0 0 0 3px rgba(249,115,22,0.1);
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
      `}</style>
    </div>
  );
}
