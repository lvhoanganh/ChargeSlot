import { useMemo, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { adminOperationsApi } from "@/services/api";
import Pagination from "@/components/Pagination";

function formatDate(dateStr) {
  if (!dateStr) return "—";
  const s = String(dateStr);
  const d = new Date(String(s).replace("Z", ""));
  return `${String(d.getDate()).padStart(2, "0")}/${String(d.getMonth() + 1).padStart(2, "0")}/${d.getFullYear()} ${String(d.getHours()).padStart(2, "0")}:${String(d.getMinutes()).padStart(2, "0")}`;
}

function getStatusLabel(status) {
  switch (status) {
    case "Pending": return "Chờ sạc";
    case "Ongoing": return "Đang sạc";
    case "PaymentPending": return "Chờ thanh toán";
    case "Paid": return "Đã thanh toán";
    case "Completed": return "Hoàn tất";
    case "Cancelled": return "Đã hủy";
    default: return status;
  }
}

function getStatusType(status) {
  switch (status) {
    case "Pending": return "warning";
    case "Ongoing": return "info";
    case "PaymentPending": return "pending";
    case "Paid": return "active";
    case "Completed": return "purple";
    case "Cancelled": return "draft";
    default: return "draft";
  }
}

export default function AdminBookings() {
  const [search, setSearch] = useState("");
  const [statusFilter, setStatusFilter] = useState("ALL");
  const [page, setPage] = useState(1);
  const pageSize = 20;

  const { data: rawData, isLoading, error } = useQuery({
    queryKey: ["admin-ops-bookings", statusFilter, page],
    queryFn: () => {
        const filter = { page, pageSize };
        if (statusFilter !== "ALL") filter.status = statusFilter;
        return adminOperationsApi.getBookings(filter);
    },
    refetchInterval: 30000,
  });

  const bookings = rawData?.items ?? [];
  const totalCount = rawData?.totalCount ?? 0;

  const filtered = useMemo(() => {
    const keyword = search.trim().toLowerCase();
    return bookings.filter((b) => {
      if (!keyword) return true;
      return (
        b.driverName?.toLowerCase().includes(keyword) ||
        b.stationName?.toLowerCase().includes(keyword) ||
        String(b.id).includes(keyword)
      );
    });
  }, [bookings, search]);

  if (isLoading) {
    return (
      <div className="cs-admin-page">
        <div style={{ textAlign: "center", paddingTop: 120 }}>
          <div className="cs-admin-table__spinner" style={{ margin: "0 auto 16px" }} />
          <p style={{ color: "#64748b", fontSize: 14 }}>Đang tải danh sách Đặt chỗ...</p>
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
          <h1 className="cs-admin-page__title">Giám sát Đặt chỗ (Tổng: {totalCount})</h1>
          <p className="cs-admin-page__subtitle">Trạng thái thời gian thực toàn cầu</p>
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
            onChange={(e) => setSearch(e.target.value)}
            placeholder="Tìm mã Booking, Khách hàng, Trạm..."
            className="cs-admin-filter__input"
          />
        </div>
        <select
          value={statusFilter}
          onChange={(e) => setStatusFilter(e.target.value)}
          className="cs-admin-filter__select"
        >
          <option value="ALL">Tất cả trạng thái</option>
          <option value="Pending">Chờ sạc (Pending)</option>
          <option value="Ongoing">Đang sạc (Ongoing)</option>
          <option value="PaymentPending">Chờ trả tiền (PaymentPending)</option>
          <option value="Paid">Đã trả (Paid)</option>
          <option value="Completed">Hoàn tất (Completed)</option>
          <option value="Cancelled">Đã hủy (Cancelled)</option>
        </select>
        <button onClick={() => { setSearch(""); setStatusFilter("ALL"); setPage(1); }} className="cs-admin-filter__reset">
          <svg width="16" height="16" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
          </svg>
          Chờ lại
        </button>
      </div>

      <div className="cs-admin-table-wrap">
        <table className="cs-admin-table">
          <thead>
            <tr>
              <th>ID</th>
              <th>Khách Hàng (Driver)</th>
              <th>Trạm Sạc</th>
              <th>Ngày Sạc</th>
              <th>T.Gian Dự Kiến</th>
              <th>Tổng Tiền</th>
              <th>Trạng thái</th>
            </tr>
          </thead>
          <tbody>
            {filtered.length === 0 ? (
              <tr>
                <td colSpan={7} className="cs-admin-table__empty">
                  <p>Không tìm thấy bản ghi nào khớp.</p>
                </td>
              </tr>
            ) : (
              filtered.map((b) => {
                const statusType = getStatusType(b.status);
                return (
                  <tr key={b.id}>
                    <td className="cs-admin-table__id">#{b.id}</td>
                    <td className="cs-admin-table__name">{b.driverName || "N/A"}</td>
                    <td>{b.stationName || "N/A"}</td>
                    <td>{formatDate(b.bookingDate || b.startTime)}</td>
                    <td style={{ color: "#64748b", fontSize: 13 }}>{b.durationHours} giờ</td>
                    <td style={{ fontWeight: "600" }}>{b.totalPrice?.toLocaleString()} đ</td>
                    <td>
                      <span className={`cs-admin-status-badge cs-admin-status-badge--${statusType}`}>
                        <span className="cs-admin-status-badge__dot" />
                        {getStatusLabel(b.status)}
                      </span>
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
            pageSize={pageSize} 
            onPageChange={(p) => setPage(p)} 
          />
        </div>
      </div>

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
`;
