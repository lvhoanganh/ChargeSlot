import { useMemo, useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { adminKycApi } from "@/services/api";
import { showToast } from "@/components/Toast";
import { formatDateVN } from "@/utils/dateVN";

function formatDate(dateStr) {
  return formatDateVN(dateStr) || "—";
}

export default function AdminKycRequests() {
  const queryClient = useQueryClient();

  const [search, setSearch] = useState("");
  const [statusFilter, setStatusFilter] = useState("ALL");
  const [dateFrom, setDateFrom] = useState("");
  const [dateTo, setDateTo] = useState("");
  const [selectedKyc, setSelectedKyc] = useState(null);
  const [adminNote, setAdminNote] = useState("");
  const [reviewAction, setReviewAction] = useState(null); // true = approve, false = reject

  const { data: kycs = [], isLoading, error } = useQuery({
    queryKey: ["admin-kyc-pending", statusFilter],
    queryFn: async () => {
      const res = await adminKycApi.getAll(statusFilter);
      // BE trả PagedResultDto: { items, totalCount } hoặc array thảng (backward compat)
      return res?.items ?? (Array.isArray(res) ? res : []);
    },
  });

  const reviewMutation = useMutation({
    mutationFn: ({ ownerUserId, isApproved, rejectReason }) =>
      adminKycApi.review(ownerUserId, isApproved, rejectReason || null),
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: ["admin-kyc-pending"] });
      const isPendingUpdate = selectedKyc?.kycStatus === "PendingUpdate";
      if (variables.isApproved) {
        showToast.success(isPendingUpdate ? " Đã duyệt bản cập nhật hồ sơ!" : " Đã phê duyệt hồ sơ!");
      } else {
        showToast.success(isPendingUpdate
          ? "↩️ Đã từ chối cập nhật — thông tin cũ đã được khôi phục."
          : " Đã từ chối hồ sơ.");
      }
      setSelectedKyc(null);
      setReviewAction(null);
      setAdminNote("");
    },
    onError: (err) => {
      const msg = err?.response?.data?.error || err?.message || "Lỗi không xác định";
      showToast.error("Lỗi: " + msg);
      setReviewAction(null);
    },
  });


  const filtered = useMemo(() => {
    const keyword = search.trim().toLowerCase();
    return kycs.filter((k) => {
      const matchSearch = (
        !keyword ||
        k.businessName?.toLowerCase().includes(keyword) ||
        k.taxCode?.toLowerCase().includes(keyword) ||
        k.idCardNumber?.toLowerCase().includes(keyword)
      );
      const submittedDate = k.kycSubmittedAt ? k.kycSubmittedAt.slice(0, 10) : "";
      const matchFrom = !dateFrom || submittedDate >= dateFrom;
      const matchTo = !dateTo || submittedDate <= dateTo;
      return matchSearch && matchFrom && matchTo;
    });
  }, [kycs, search, dateFrom, dateTo]);

  function confirmReview() {
    if (!selectedKyc) return;
    reviewMutation.mutate({
      ownerUserId: selectedKyc.ownerUserId,
      isApproved: reviewAction,
      rejectReason: adminNote,
    });
  }

  if (isLoading) {
    return (
      <div className="cs-admin-page">
        <div style={{ textAlign: "center", paddingTop: 120 }}>
          <div className="cs-admin-table__spinner" style={{ margin: "0 auto 16px" }} />
          <p style={{ color: "#64748b", fontSize: 14 }}>Đang tải danh sách chờ duyệt...</p>
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
          <button
            onClick={() => queryClient.invalidateQueries({ queryKey: ["admin-kyc-pending"] })}
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
      <div className="cs-admin-page__header">
        <div>
          <h1 className="cs-admin-page__title">Xét duyệt danh tính chủ trạm</h1>
          <p className="cs-admin-page__subtitle">
            Quản lý và phê duyệt hồ sơ của các chủ kinh doanh.
          </p>
        </div>
      </div>

      <div className="cs-admin-filter" style={{ flexWrap: "wrap", gap: 10 }}>
        <div className="cs-admin-filter__search">
          <svg className="cs-admin-filter__search-icon" width="18" height="18" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
          </svg>
          <input
            type="text"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder="Tìm theo tên doanh nghiệp, CCCD, Mã số thuế..."
            className="cs-admin-filter__input"
          />
        </div>
        <select
          value={statusFilter}
          onChange={(e) => setStatusFilter(e.target.value)}
          className="cs-admin-filter__select"
        >
          <option value="ALL">Tất cả trạng thái</option>
          <option value="Pending"> Chờ duyệt lần đầu</option>
          <option value="PendingUpdate"> Chờ duyệt cập nhật</option>
          <option value="Approved"> Đã duyệt</option>
          <option value="Rejected"> Từ chối</option>
          <option value="Unverified">Chưa cập nhật</option>
        </select>
        {/* Date range */}
        <div style={{ display: "flex", alignItems: "center", gap: 6, flexShrink: 0 }}>
          <svg width="15" height="15" fill="none" stroke="#64748b" strokeWidth={2} viewBox="0 0 24 24">
            <rect x="3" y="4" width="18" height="18" rx="2" /><line x1="16" y1="2" x2="16" y2="6" /><line x1="8" y1="2" x2="8" y2="6" /><line x1="3" y1="10" x2="21" y2="10" />
          </svg>
          <input type="date" value={dateFrom} onChange={(e) => setDateFrom(e.target.value)}
            className="cs-admin-filter__select" style={{ width: 140, cursor: "pointer" }} title="Từ ngày gửi" />
          <span style={{ color: "#94a3b8", fontSize: 12 }}>—</span>
          <input type="date" value={dateTo} onChange={(e) => setDateTo(e.target.value)}
            className="cs-admin-filter__select" style={{ width: 140, cursor: "pointer" }} title="Đến ngày gửi" />
        </div>
        <button onClick={() => { setSearch(""); setStatusFilter("ALL"); setDateFrom(""); setDateTo(""); }} className="cs-admin-filter__reset">
          <svg width="16" height="16" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
          </svg>
          Xóa bộ lọc
        </button>
      </div>

      <div className="cs-admin-table-wrap">
        <table className="cs-admin-table">
          <thead>
            <tr>
              <th>STT</th>
              <th>Đơn vị / Cá nhân</th>
              <th>Mã số thuế</th>
              <th>CCCD / CMND</th>
              <th>Địa chỉ ĐKKD</th>
              <th>Ngày gửi</th>
              <th>Trạng thái</th>
              <th style={{ textAlign: "right" }}>Theo dõi / Xử lý</th>
            </tr>
          </thead>
          <tbody>
            {filtered.length === 0 ? (
              <tr>
                <td colSpan={8} className="cs-admin-table__empty">
                  <p>Kho hồ sơ không có dữ liệu </p>
                </td>
              </tr>
            ) : (
              filtered.map((k, idx) => (
                <tr key={k.ownerUserId}>
                  <td className="cs-admin-table__id" style={{ fontWeight: 700, color: "#64748b" }}>{idx + 1}</td>
                  <td className="cs-admin-table__name">{k.businessName}</td>
                  <td><span style={{ fontWeight: 600, color: "#f59e0b" }}>{k.taxCode}</span></td>
                  <td>{k.idCardNumber}</td>
                  <td>{k.address}</td>
                  <td>{formatDate(k.kycSubmittedAt)}</td>
                  <td>
                    {k.kycStatus === "Approved" && <span className="cs-admin-status-badge cs-admin-status-badge--active"><span className="cs-admin-status-badge__dot" />Đã duyệt</span>}
                    {k.kycStatus === "Pending" && <span className="cs-admin-status-badge cs-admin-status-badge--pending"><span className="cs-admin-status-badge__dot" />🆕 Đăng ký mới</span>}
                    {k.kycStatus === "Rejected" && <span className="cs-admin-status-badge cs-admin-status-badge--banned"><span className="cs-admin-status-badge__dot" />Từ chối</span>}
                    {k.kycStatus === "Unverified" && <span className="cs-admin-status-badge" style={{ background: "#f1f5f9", color: "#64748b" }}><span className="cs-admin-status-badge__dot" style={{ background: "#94a3b8" }} />Chưa cập nhật</span>}
                    {k.kycStatus === "PendingUpdate" && <span className="cs-admin-status-badge" style={{ background: "#eff6ff", color: "#2563eb" }}><span className="cs-admin-status-badge__dot" style={{ background: "#3b82f6" }} /> Cập nhật</span>}
                  </td>
                  <td style={{ textAlign: "right" }}>
                    {k.kycStatus === "Approved" ? (
                      <button
                        onClick={() => { setSelectedKyc(k); setReviewAction(null); setAdminNote(""); }}
                        className="cs-admin-action-btn"
                        style={{ background: "#f1f5f9", color: "#64748b", border: "1px solid #e2e8f0" }}
                      >
                        Xem
                      </button>
                    ) : (
                      <button
                        onClick={() => { setSelectedKyc(k); setReviewAction(null); setAdminNote(""); }}
                        className="cs-admin-action-btn" style={{ background: "#3b82f6", color: "#fff" }}
                      >
                        Xem & Xử lý
                      </button>
                    )}
                  </td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>

      {/* KYC Review Drawer / Modal */}
      {selectedKyc && (
        <div className="cs-admin-modal-overlay">
          <div className="cs-admin-modal" style={{ maxWidth: 900, textAlign: 'left' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 20 }}>
              <h2 className="cs-admin-modal__title" style={{ margin: 0, display: "flex", alignItems: "center", gap: 10 }}>
                <span style={{ fontSize: 30 }}>{selectedKyc.kycStatus === "PendingUpdate" ? "" : ""}</span>
                {selectedKyc.kycStatus === "PendingUpdate" ? "Yêu cầu cập nhật hồ sơ" : "Kiểm tra hồ sơ"}
                {selectedKyc.kycStatus === "PendingUpdate" && (
                  <span style={{ fontSize: 13, fontWeight: 600, padding: "3px 10px", borderRadius: 50, background: "#eff6ff", color: "#2563eb", border: "1px solid #bfdbfe" }}>
                    Bản cập nhật
                  </span>
                )}
              </h2>
              <button
                onClick={() => { setSelectedKyc(null); setReviewAction(null); setAdminNote(""); }}
                style={{ background: '#f1f5f9', border: 'none', cursor: 'pointer', width: 36, height: 36, borderRadius: '50%', fontSize: 18, color: '#64748b', display: 'flex', alignItems: 'center', justifyContent: 'center', flexShrink: 0, transition: 'background 0.15s' }}
                onMouseEnter={e => e.currentTarget.style.background = '#e2e8f0'}
                onMouseLeave={e => e.currentTarget.style.background = '#f1f5f9'}
                title="Đóng"
              >
                ✕
              </button>
            </div>

            {/* PendingUpdate info note */}
            {selectedKyc.kycStatus === "PendingUpdate" && (
              <div style={{ marginBottom: 20, padding: "10px 16px", background: "#eff6ff", border: "1px solid #bfdbfe", borderRadius: 10, fontSize: 13, color: "#2563eb", display: "flex", gap: 8, alignItems: "flex-start" }}>
                <span>ℹ️</span>
                <div>
                  <strong>Yêu cầu cập nhật hồ sơ.</strong> Nếu bạn từ chối, thông tin cũ sẽ được tự động khôi phục và chủ trạm vẫn giữ trạng thái Approved.
                </div>
              </div>
            )}

            <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 24, marginBottom: 24 }}>
              <div style={{ background: "#f8fafc", padding: 16, borderRadius: 12 }}>
                <h3 style={{ fontSize: 13, fontWeight: 700, color: "#64748b", textTransform: 'uppercase', marginBottom: 12 }}>Thông tin doanh nghiệp / cá nhân</h3>
                <div style={{ display: "grid", gridTemplateColumns: "100px 1fr", gap: "8px 12px", fontSize: 14 }}>
                  <span style={{ color: "#64748b" }}>Đơn vị:</span> <strong>{selectedKyc.businessName}</strong>
                  <span style={{ color: "#64748b" }}>Mã số thuế:</span> <strong style={{ color: "#eab308" }}>{selectedKyc.taxCode}</strong>
                  <span style={{ color: "#64748b" }}>Giấy phép KD:</span> <strong>{selectedKyc.businessLicenseNumber}</strong>
                  <span style={{ color: "#64748b" }}>Địa chỉ ĐK:</span> <strong>{selectedKyc.address}</strong>
                </div>
              </div>

              <div style={{ background: "#f8fafc", padding: 16, borderRadius: 12 }}>
                <h3 style={{ fontSize: 13, fontWeight: 700, color: "#64748b", textTransform: 'uppercase', marginBottom: 12 }}>Thông tin người đại diện</h3>
                <div style={{ display: "grid", gridTemplateColumns: "100px 1fr", gap: "8px 12px", fontSize: 14 }}>
                  <span style={{ color: "#64748b" }}>Số CCCD:</span> <strong>{selectedKyc.idCardNumber}</strong>
                  <span style={{ color: "#64748b" }}>Ngày cấp:</span> <strong>{selectedKyc.idCardDate}</strong>
                  <span style={{ color: "#64748b" }}>Trạng thái:</span>
                  {selectedKyc.kycStatus === "Approved" && <span className="cs-admin-status-badge cs-admin-status-badge--active"><span className="cs-admin-status-badge__dot" />Đã duyệt</span>}
                  {selectedKyc.kycStatus === "Pending" && <span className="cs-admin-status-badge cs-admin-status-badge--pending"><span className="cs-admin-status-badge__dot" />🆕 Đăng ký mới</span>}
                  {selectedKyc.kycStatus === "Rejected" && <span className="cs-admin-status-badge cs-admin-status-badge--banned"><span className="cs-admin-status-badge__dot" />Từ chối</span>}
                  {selectedKyc.kycStatus === "Unverified" && <span className="cs-admin-status-badge" style={{ background: "#f1f5f9", color: "#64748b" }}><span className="cs-admin-status-badge__dot" style={{ background: "#94a3b8" }} />Chưa cập nhật</span>}
                  {selectedKyc.kycStatus === "PendingUpdate" && <span className="cs-admin-status-badge" style={{ background: "#eff6ff", color: "#2563eb" }}><span className="cs-admin-status-badge__dot" style={{ background: "#3b82f6" }} /> Cập nhật</span>}
                  <span style={{ color: "#64748b" }}>Thời gian gửi:</span> <strong>{formatDate(selectedKyc.kycSubmittedAt)}</strong>
                </div>
              </div>
            </div>

            <h3 style={{ fontSize: 14, fontWeight: 700, color: "#1e293b", marginBottom: 12 }}>Hình ảnh xác thực</h3>
            <div style={{ display: "grid", gridTemplateColumns: "repeat(3, 1fr)", gap: 16, marginBottom: 24 }}>
              <a href={selectedKyc.businessLicenseUrl} target="_blank" rel="noreferrer" style={{ display: "block", borderRadius: 12, overflow: "hidden", border: "1px solid #e2e8f0" }}>
                <div style={{ background: "#f1f5f9", padding: "6px 12px", fontSize: 12, fontWeight: 600, color: "#64748b", borderBottom: "1px solid #e2e8f0" }}>Giấy phép KD</div>
                <div style={{ height: 160, background: `url(${selectedKyc.businessLicenseUrl}) center / cover no-repeat`, backgroundColor: "#f8fafc" }}></div>
              </a>
            </div>

            {(selectedKyc.kycStatus === "Pending" || selectedKyc.kycStatus === "PendingUpdate") ? (
              reviewAction !== null ? (
                <div style={{ background: reviewAction ? "#f0fdf4" : "#fef2f2", padding: 20, borderRadius: 16, border: `1px solid ${reviewAction ? "#bbf7d0" : "#fecaca"}` }}>
                  <h3 style={{ fontSize: 15, fontWeight: 600, color: reviewAction ? "#16a34a" : "#dc2626", marginBottom: 12 }}>
                    {reviewAction ? " Phê duyệt hồ sơ này?" : " Từ chối hồ sơ này?"}
                  </h3>

                  {!reviewAction && selectedKyc.kycStatus === "PendingUpdate" && (
                    <div style={{ marginBottom: 12, padding: "8px 12px", background: "#fef3c7", borderRadius: 8, fontSize: 13, color: "#b45309", border: "1px solid #fde68a" }}>
                      ️ Từ chối bản cập nhật sẽ khôi phục thông tin cũ. Chủ trạm vẫn giữ trạng thái Approved.
                    </div>
                  )}

                  {!reviewAction && (
                    <div style={{ marginBottom: 16 }}>
                      <label style={{ display: "block", fontSize: 13, fontWeight: 600, color: "#374151", marginBottom: 6 }}>
                        Lý do từ chối (Bắt buộc)
                      </label>
                      <textarea
                        value={adminNote}
                        onChange={(e) => setAdminNote(e.target.value)}
                        placeholder="Ví dụ: Giấy tờ bị mờ, không khớp mã số thuế..."
                        rows={3}
                        style={{ width: "100%", padding: "10px 14px", borderRadius: 10, border: "1px solid #e5e7eb", fontSize: 14, outline: "none" }}
                      />
                    </div>
                  )}

                  <div style={{ display: "flex", gap: 12 }}>
                    <button
                      onClick={() => setReviewAction(null)}
                      className="cs-admin-action-btn" style={{ flex: 1, background: "white", color: "#374151", border: "1px solid #d1d5db" }}
                    >Hủy</button>
                    <button
                      onClick={confirmReview}
                      disabled={reviewMutation.isPending || (!reviewAction && !adminNote.trim())}
                      className="cs-admin-action-btn" style={{ flex: 1, background: reviewAction ? "#22c55e" : "#ef4444", color: "#fff" }}
                    >
                      {reviewMutation.isPending ? "Đang xử lý..." : "Xác nhận gửi"}
                    </button>
                  </div>
                </div>
              ) : (
                <div style={{ display: "flex", gap: 12 }}>
                  <button
                    onClick={() => setReviewAction(true)}
                    className="cs-admin-action-btn" style={{ flex: 1, background: "#22c55e", color: "white", height: 48, fontSize: 15 }}
                  >
                    Phê duyệt
                  </button>
                  <button
                    onClick={() => setReviewAction(false)}
                    className="cs-admin-action-btn" style={{ flex: 1, background: "#ef4444", color: "white", height: 48, fontSize: 15 }}
                  >
                    Từ chối
                  </button>
                </div>
              )
            ) : (
              <div style={{ background: "#f8fafc", padding: 20, borderRadius: 16, border: "1px solid #e2e8f0", textAlign: "center" }}>
                <p style={{ color: "#64748b", fontWeight: 600, margin: 0 }}>
                  Hồ sơ này đã được kiểm duyệt vào {formatDate(selectedKyc.kycReviewedAt)} bởi Hệ thống.
                </p>
                {selectedKyc.kycRejectReason && (
                  <p style={{ color: "#dc2626", marginTop: 8, fontSize: 14 }}>
                    <strong>Lý do từ chối:</strong> {selectedKyc.kycRejectReason}
                  </p>
                )}
              </div>
            )}
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
  .cs-admin-table-wrap {
    background: white;
    border: 1px solid rgba(0,0,0,0.06);
    border-radius: 16px;
    overflow: hidden;
    overflow-x: auto;
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
  .cs-admin-table__id { color: #9ca3af; font-size: 13px; font-weight: bold; }
  .cs-admin-table__name { font-weight: 600; color: #1e293b; }
  .cs-admin-table__empty {
    text-align: center;
    padding: 48px 0 !important;
    color: #94a3b8;
  }
  .cs-admin-action-btn {
    display: inline-flex;
    align-items: center;
    justify-content: center;
    height: 34px;
    padding: 0 14px;
    border-radius: 10px;
    font-size: 12px;
    font-weight: 600;
    border: none;
    cursor: pointer;
    transition: all 0.2s;
  }
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
    background: white;
    border-radius: 20px;
    padding: 32px;
    animation: cs-fadeInUp 0.3s ease-out;
    box-shadow: 0 20px 60px rgba(0,0,0,0.2);
  }
  @keyframes cs-fadeInUp {
    from { opacity: 0; transform: translateY(20px); }
    to { opacity: 1; transform: translateY(0); }
  }
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
`;
