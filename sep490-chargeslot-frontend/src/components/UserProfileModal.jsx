import { useState, Fragment } from "react";
import { useQuery } from "@tanstack/react-query";
import { adminAccountsDetailsApi, bookingApi } from "@/services/api";
import { formatDateVN } from "@/utils/dateVN";

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

export default function UserProfileModal({ user, onClose }) {
  const [expandedBookingId, setExpandedBookingId] = useState(null);

  const isOwner = user?.role === "Owner";

  const { data, isLoading, error } = useQuery({
    queryKey: ["admin-user-profile", user?.id, user?.role],
    queryFn: () => isOwner ? adminAccountsDetailsApi.getOwnerDetails(user.id) : adminAccountsDetailsApi.getDriverDetails(user.id),
    enabled: !!user,
  });

  const { data: bookingDetails, isLoading: loadingBooking } = useQuery({
    queryKey: ["admin-booking-detail", expandedBookingId],
    queryFn: () => bookingApi.getById(expandedBookingId),
    enabled: !!expandedBookingId,
  });

  if (!user) return null;

  return (
    <div className="cs-admin-modal-overlay">
      <div className="cs-admin-modal" style={{ maxWidth: 900, width: "100%", maxHeight: "90vh", overflowY: "auto", textAlign: "left" }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 20 }}>
          <h2 className="cs-admin-modal__title" style={{ margin: 0, display: "flex", alignItems: "center", gap: 10 }}>
            <span style={{ fontSize: 30 }}>{isOwner ? "🏢" : "🚗"}</span>
            Chi tiết hồ sơ {isOwner ? "Chủ trạm" : "Tài xế"}: {user.fullName}
          </h2>
          <button onClick={onClose} style={{ background: 'none', border: 'none', cursor: 'pointer', fontSize: 24 }}>&times;</button>
        </div>

        {isLoading ? (
          <div style={{ textAlign: "center", padding: 40 }}>
            <div className="cs-admin-table__spinner" style={{ margin: "0 auto 16px" }} />
            <p style={{ color: "#64748b" }}>Đang tải dữ liệu hồ sơ...</p>
          </div>
        ) : error ? (
          <p style={{ color: "#ef4444", textAlign: "center", padding: 40 }}> Lỗi tải dữ liệu: {error.message}</p>
        ) : data ? (
          <div style={{ display: "flex", flexDirection: "column", gap: 20 }}>
            <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 20 }}>
              {/* Thông tin cơ bản */}
              <div style={{ background: "#f8fafc", padding: 16, borderRadius: 12 }}>
                <h3 style={{ fontSize: 13, fontWeight: 700, color: "#64748b", textTransform: 'uppercase', marginBottom: 12 }}>Thông tin cơ bản</h3>
                <div style={{ display: "grid", gridTemplateColumns: "100px 1fr", gap: "8px 12px", fontSize: 14 }}>
                  <span style={{ color: "#64748b" }}>Email:</span> <strong>{data.email || "—"}</strong>
                  <span style={{ color: "#64748b" }}>Số đt:</span> <strong>{data.phoneNumber}</strong>
                  <span style={{ color: "#64748b" }}>Ngày tham gia:</span> <strong>{formatDateVN(data.createdAt)}</strong>
                  {data.loyaltyPoints !== undefined && (
                    <>
                      <span style={{ color: "#64748b" }}>Điểm thưởng:</span> <strong><span style={{ color: "#eab308" }}>{data.loyaltyPoints} điểm</span></strong>
                    </>
                  )}
                  {data.vehicleType && (
                    <>
                      <span style={{ color: "#64748b" }}>Phương tiện:</span> <strong>{data.vehicleType}</strong>
                      <span style={{ color: "#64748b" }}>Biển số:</span> <strong>{data.licensePlate}</strong>
                    </>
                  )}
                </div>
              </div>

              {/* Ví điện tử */}
              <div style={{ background: "#f8fafc", padding: 16, borderRadius: 12 }}>
                <h3 style={{ fontSize: 13, fontWeight: 700, color: "#64748b", textTransform: 'uppercase', marginBottom: 12 }}>Ví điện tử (ID: {data.wallet?.walletId})</h3>
                <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
                  <div style={{ display: "flex", justifyContent: "space-between" }}>
                    <span style={{ color: "#64748b", fontSize: 14 }}>Số dư khả dụng:</span>
                    <strong style={{ color: "#16a34a", fontSize: 16 }}>{data.wallet?.availableBalance?.toLocaleString() || 0} đ</strong>
                  </div>
                  <div style={{ display: "flex", justifyContent: "space-between" }}>
                    <span style={{ color: "#64748b", fontSize: 14 }}>Đóng băng (Escrow):</span>
                    <strong style={{ color: "#ef4444", fontSize: 16 }}>{data.wallet?.frozenBalance?.toLocaleString() || 0} đ</strong>
                  </div>
                </div>
              </div>
            </div>

            {/* KYC (nếu là Owner) */}
            {isOwner && data.kyc && (
              <div style={{ background: "#f8fafc", padding: 16, borderRadius: 12 }}>
                <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 12 }}>
                  <h3 style={{ fontSize: 13, fontWeight: 700, color: "#64748b", textTransform: 'uppercase', margin: 0 }}>Hồ sơ doanh nghiệp (KYC)</h3>
                  <span>
                    {data.kyc.kycStatus === "Approved" && <span className="cs-admin-status-badge cs-admin-status-badge--active"><span className="cs-admin-status-badge__dot" />Đã duyệt</span>}
                    {data.kyc.kycStatus === "Pending" && <span className="cs-admin-status-badge cs-admin-status-badge--pending"><span className="cs-admin-status-badge__dot" />Chờ duyệt</span>}
                    {data.kyc.kycStatus === "Rejected" && <span className="cs-admin-status-badge cs-admin-status-badge--banned"><span className="cs-admin-status-badge__dot" />Từ chối</span>}
                    {data.kyc.kycStatus === "Unverified" && <span className="cs-admin-status-badge" style={{background: "#e2e8f0", color: "#475569"}}><span className="cs-admin-status-badge__dot" style={{background: "#64748b"}}/>Chưa cập nhật</span>}
                  </span>
                </div>
                <div style={{ display: "grid", gridTemplateColumns: "100px 1fr 100px 1fr", gap: "8px 12px", fontSize: 14 }}>
                  <span style={{ color: "#64748b" }}>Đơn vị:</span> <strong>{data.kyc.businessName || "—"}</strong>
                  <span style={{ color: "#64748b" }}>Mã số thuế:</span> <strong>{data.kyc.taxCode || "—"}</strong>
                  <span style={{ color: "#64748b" }}>CCCD/CMND:</span> <strong>{data.kyc.idCardNumber || "—"}</strong>
                  <span style={{ color: "#64748b" }}>GPKD:</span> <strong>{data.kyc.businessLicenseNumber || "—"}</strong>
                </div>
              </div>
            )}

            {/* Danh sách trạm (nếu là Owner) */}
            {isOwner && data.stations && (
              <div>
                <h3 style={{ fontSize: 14, fontWeight: 700, color: "#1e293b", margin: "10px 0" }}>Danh sách Trạm sạc ({data.stations.length})</h3>
                <div className="cs-admin-table-wrap">
                  <table className="cs-admin-table" style={{ minWidth: "100%" }}>
                    <thead style={{ background: "#f8fafc" }}>
                      <tr>
                        <th>Tên trạm</th>
                        <th>Trạng thái duyệt</th>
                        <th>Hoạt động</th>
                        <th>Đánh giá</th>
                      </tr>
                    </thead>
                    <tbody>
                      {data.stations.length === 0 ? (
                        <tr><td colSpan={4} style={{ textAlign: "center", color: "#94a3b8" }}>Chưa có trạm sạc nào</td></tr>
                      ) : (
                        data.stations.map((s, i) => (
                          <tr key={i}>
                            <td style={{ fontWeight: 600 }}>{s.name}</td>
                            <td>
                              <span className="cs-admin-status-badge" style={{ background: "#f1f5f9", padding: "2px 8px" }}>{getApprovalStatusLabel(s.approvalStatus)}</span>
                            </td>
                            <td>{getOperationalStatusLabel(s.operationalStatus)}</td>
                            <td>{s.averageRating > 0 ? `${s.averageRating} ⭐` : "—"}</td>
                          </tr>
                        ))
                      )}
                    </tbody>
                  </table>
                </div>
              </div>
            )}

            {/* Recent Bookings (nếu là Driver) */}
            {!isOwner && data.recentBookings && (
              <div>
                <h3 style={{ fontSize: 14, fontWeight: 700, color: "#1e293b", margin: "10px 0" }}>10 Phiên giao dịch gần nhất</h3>
                <div className="cs-admin-table-wrap">
                  <table className="cs-admin-table" style={{ minWidth: "100%" }}>
                    <thead style={{ background: "#f8fafc" }}>
                      <tr>
                        <th>Mã Book</th>
                        <th>Trạm - Slot</th>
                        <th>Thời gian</th>
                        <th>Chi phí</th>
                        <th>Trạng thái</th>
                      </tr>
                    </thead>
                    <tbody>
                      {data.recentBookings.length === 0 ? (
                        <tr><td colSpan={5} style={{ textAlign: "center", color: "#94a3b8" }}>Chưa có phiên giao dịch nào</td></tr>
                      ) : (
                        data.recentBookings.map((b, i) => {
                          const isExpanded = expandedBookingId === b.bookingId;
                          return (
                            <Fragment key={i}>
                              <tr
                                onClick={() => setExpandedBookingId(isExpanded ? null : b.bookingId)}
                                style={{ cursor: "pointer", background: isExpanded ? "#fefce8" : "transparent" }}
                              >
                                <td style={{ fontWeight: 600, color: "#64748b" }}>#{b.bookingId}</td>
                                <td><strong>{b.stationName}</strong><br/><span style={{fontSize: 12, color: "#64748b"}}>{b.slotName}</span></td>
                                <td>{formatDateVN(b.startTime)}</td>
                                <td style={{ color: "#16a34a", fontWeight: 600 }}>{b.totalAmount?.toLocaleString()} đ</td>
                                <td>
                                  <span className="cs-admin-status-badge" style={{ background: "#f1f5f9", padding: "2px 8px" }}>{getBookingStatusLabel(b.status)}</span>
                                </td>
                              </tr>
                              {isExpanded && (
                                <tr>
                                  <td colSpan={5} style={{ background: "#f8fafc", padding: "16px 24px", borderBottom: "1px solid #e2e8f0" }}>
                                    {loadingBooking ? (
                                      <span style={{ color: "#64748b", fontSize: 13 }}>Đang tải thông tin chi tiết...</span>
                                    ) : !bookingDetails ? (
                                      <span style={{ color: "#ef4444", fontSize: 13 }}>Lỗi tải dữ liệu.</span>
                                    ) : (
                                      <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 12 }}>
                                        <div>
                                          <p style={{ margin: "0 0 6px", fontSize: 13, color: "#64748b" }}>Số giờ sạc: <strong style={{ color: "#000" }}>{bookingDetails.durationHours} giờ</strong></p>
                                          <p style={{ margin: "0 0 6px", fontSize: 13, color: "#64748b" }}>Bắt đầu: <strong style={{ color: "#000" }}>{formatDateVN(bookingDetails.startTime)}</strong></p>
                                          <p style={{ margin: "0", fontSize: 13, color: "#64748b" }}>Kết thúc: <strong style={{ color: "#000" }}>{formatDateVN(bookingDetails.endTime)}</strong></p>
                                        </div>
                                        <div>
                                          <p style={{ margin: "0 0 6px", fontSize: 13, color: "#64748b" }}>Tiền thanh toán: <strong style={{ color: "#16a34a" }}>{bookingDetails.totalAmount?.toLocaleString() || 0} đ</strong> {bookingDetails.totalAmount > 0 ? "(Ví)" : "(Miễn phí)"}</p>
                                          <p style={{ margin: "0 0 6px", fontSize: 13, color: "#64748b" }}>Đánh giá: <strong style={{ color: "#000" }}>{bookingDetails.score > 0 ? `${bookingDetails.score} ⭐` : "Chưa có"}</strong></p>
                                          <p style={{ margin: "0", fontSize: 13, color: "#64748b" }}>Giao dịch: <strong style={{ color: "#000" }}>#{bookingDetails.id}</strong></p>
                                        </div>
                                        {bookingDetails.note && (
                                          <div style={{ gridColumn: "span 2", marginTop: 4 }}>
                                            <p style={{ margin: "0", fontSize: 13, color: "#64748b" }}>Ghi chú Của Tài xế: <strong style={{ color: "#000", fontStyle: "italic" }}>{bookingDetails.note}</strong></p>
                                          </div>
                                        )}
                                        {bookingDetails.status === "Rejected" && bookingDetails.rejectionReason && (
                                          <div style={{ gridColumn: "span 2", marginTop: 4 }}>
                                            <p style={{ margin: "0", fontSize: 13, color: "#64748b" }}>Lý do từ chối: <strong style={{ color: "#ef4444" }}>{bookingDetails.rejectionReason}</strong></p>
                                          </div>
                                        )}
                                        {bookingDetails.status === "Cancelled" && bookingDetails.cancelReason && (
                                          <div style={{ gridColumn: "span 2", marginTop: 4 }}>
                                            <p style={{ margin: "0", fontSize: 13, color: "#64748b" }}>Lý do hủy: <strong style={{ color: "#ef4444" }}>{bookingDetails.cancelReason}</strong></p>
                                          </div>
                                        )}
                                      </div>
                                    )}
                                  </td>
                                </tr>
                              )}
                            </Fragment>
                          );
                        })
                      )}
                    </tbody>
                  </table>
                </div>
              </div>
            )}
          </div>
        ) : null}
      </div>
    </div>
  );
}
