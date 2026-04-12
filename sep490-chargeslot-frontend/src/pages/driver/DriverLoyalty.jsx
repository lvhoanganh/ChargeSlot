import { useState, useEffect } from "react";
import { useNavigate } from "react-router-dom";
import { loyaltyApi, bookingApi } from "@/services/api";

// Trích HH:MM từ chuỗi thời gian dạng "HH:mm:ss" hoặc "2026-04-10T10:32:27..."
function extractTime(raw) {
  if (!raw) return "";
  const s = String(raw);
  const tIdx = s.indexOf("T");
  if (tIdx !== -1) return s.substring(tIdx + 1, tIdx + 6); // lấy HH:MM sau "T"
  return s.substring(0, 5); // dạng "HH:mm:ss" → lấy HH:MM
}

const BOOKING_STATUS_MAP = {
  Pending:                  { label: "Chờ xác nhận",        color: "#ca8a04", bg: "#fefce8" },
  Confirmed:                { label: "Đã xác nhận",         color: "#2563eb", bg: "#eff6ff" },
  CheckedIn:                { label: "Đã check-in",         color: "#7c3aed", bg: "#f5f3ff" },
  Charging:                 { label: "Đang sạc",            color: "#0891b2", bg: "#ecfeff" },
  Completed:                { label: "Hoàn thành",          color: "#16a34a", bg: "#f0fdf4" },
  CompletedPendingInvoice:  { label: "Chờ thanh toán",      color: "#d97706", bg: "#fffbeb" },
  CancelledByDriver:        { label: "Driver hủy",          color: "#dc2626", bg: "#fef2f2" },
  CancelledByOwner:         { label: "Chủ trạm hủy",       color: "#dc2626", bg: "#fef2f2" },
  CancelledByAdmin:         { label: "Admin hủy",           color: "#dc2626", bg: "#fef2f2" },
  NoShow:                   { label: "Không đến",           color: "#9ca3af", bg: "#f3f4f6" },
};

export default function DriverLoyalty() {
  const navigate = useNavigate();
  const [data, setData] = useState(null);
  const [loading, setLoading] = useState(true);
  const [selectedTx, setSelectedTx] = useState(null);
  const [bookingDetail, setBookingDetail] = useState(null);
  const [bookingLoading, setBookingLoading] = useState(false);

  useEffect(() => {
    loyaltyApi.getInfo()
      .then(setData)
      .catch(() => setData(null))
      .finally(() => setLoading(false));
  }, []);

  // Fetch booking detail when a transaction with bookingId is selected
  useEffect(() => {
    if (!selectedTx?.bookingId) {
      setBookingDetail(null);
      return;
    }
    setBookingLoading(true);
    setBookingDetail(null);
    bookingApi.getById(selectedTx.bookingId)
      .then(setBookingDetail)
      .catch(() => setBookingDetail(null))
      .finally(() => setBookingLoading(false));
  }, [selectedTx]);

  if (loading) {
    return (
      <div style={{ minHeight: "100vh", background: "#f8fafc", paddingTop: 100, textAlign: "center" }}>
        <div style={{ fontSize: 40 }}>🏆</div>
        <p style={{ color: "#6b7280" }}>Đang tải điểm thưởng...</p>
      </div>
    );
  }

  if (!data) {
    return (
      <div style={{ minHeight: "100vh", background: "#f8fafc", paddingTop: 100, textAlign: "center" }}>
        <div style={{ fontSize: 48, marginBottom: 8 }}>🏆</div>
        <h2 style={{ fontSize: 20, fontWeight: 700, color: "#1e293b" }}>Không thể tải thông tin điểm thưởng</h2>
        <button onClick={() => navigate(-1)} style={{ marginTop: 16, padding: "10px 20px", borderRadius: 10, border: "none", background: "#f97316", color: "#fff", fontWeight: 600, cursor: "pointer" }}>
          ← Quay lại
        </button>
      </div>
    );
  }

  const history = data.recentHistory || [];

  return (
    <div style={{ minHeight: "100vh", background: "#f8fafc", paddingTop: 90 }}>
      <div style={{ maxWidth: 700, margin: "0 auto", padding: "0 16px 40px" }}>
        {/* Points card */}
        <div style={{
          background: "linear-gradient(135deg, #7c3aed 0%, #6d28d9 50%, #4c1d95 100%)",
          borderRadius: 24, padding: "32px 28px", color: "#fff", marginBottom: 24,
          boxShadow: "0 8px 32px rgba(124,58,237,0.3)",
        }}>
          <p style={{ fontSize: 13, fontWeight: 600, opacity: 0.85, letterSpacing: 1, textTransform: "uppercase" }}>Điểm tích lũy</p>
          <div style={{ fontSize: 42, fontWeight: 800, marginTop: 4, display: "flex", alignItems: "baseline", gap: 8 }}>
            {(data.currentPoints || 0).toLocaleString("vi-VN")}
            <span style={{ fontSize: 16, fontWeight: 600, opacity: 0.7 }}>điểm</span>
          </div>
          <p style={{ fontSize: 12, opacity: 0.7, marginTop: 4 }}>1 điểm = 1 VND</p>

          <div style={{ display: "flex", gap: 10, marginTop: 20, flexWrap: "wrap" }}>
            <div style={{
              padding: "8px 16px", borderRadius: 12,
              background: "rgba(255,255,255,0.15)", backdropFilter: "blur(4px)",
              border: "1px solid rgba(255,255,255,0.2)",
            }}>
              <div style={{ fontSize: 10, opacity: 0.7, textTransform: "uppercase", letterSpacing: 0.5 }}>Tỷ lệ tích</div>
              <div style={{ fontSize: 18, fontWeight: 800 }}>{((data.earnRate || 0) * 100).toFixed(0)}%</div>
            </div>
            <div style={{
              padding: "8px 16px", borderRadius: 12,
              background: "rgba(255,255,255,0.15)", backdropFilter: "blur(4px)",
              border: "1px solid rgba(255,255,255,0.2)",
            }}>
              <div style={{ fontSize: 10, opacity: 0.7, textTransform: "uppercase", letterSpacing: 0.5 }}>Tối đa dùng</div>
              <div style={{ fontSize: 18, fontWeight: 800 }}>{((data.maxRedeemRate || 0) * 100).toFixed(0)}%</div>
            </div>
          </div>
        </div>

        {/* History */}
        <h2 style={{ fontSize: 20, fontWeight: 800, color: "#1e293b", marginBottom: 16 }}>
          Lịch sử điểm
        </h2>

        {history.length === 0 ? (
          <div style={{
            textAlign: "center", padding: 40, background: "#fff", borderRadius: 16,
            boxShadow: "0 2px 8px rgba(0,0,0,0.04)",
          }}>
            <div style={{ fontSize: 48, marginBottom: 8 }}>📋</div>
            <p style={{ color: "#6b7280" }}>Chưa có lịch sử điểm</p>
          </div>
        ) : (
          <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
            {history.map(h => {
              const isEarn = h.type === "Earn";
              return (
                <div key={h.id} onClick={() => setSelectedTx(h)} style={{
                  background: "#fff", borderRadius: 16, padding: "16px 20px",
                  boxShadow: "0 2px 8px rgba(0,0,0,0.04)", display: "flex",
                  alignItems: "center", gap: 12, cursor: "pointer",
                  transition: "transform 0.1s, box-shadow 0.1s"
                }}
                onMouseEnter={(e) => { e.currentTarget.style.transform = "scale(1.01)"; e.currentTarget.style.boxShadow = "0 4px 12px rgba(0,0,0,0.08)"; }}
                onMouseLeave={(e) => { e.currentTarget.style.transform = "none"; e.currentTarget.style.boxShadow = "0 2px 8px rgba(0,0,0,0.04)"; }}
                >
                  <div style={{
                    width: 42, height: 42, borderRadius: 12,
                    background: isEarn ? "#f0fdf415" : "#fef2f215",
                    display: "flex", alignItems: "center", justifyContent: "center",
                    fontSize: 20, flexShrink: 0,
                    backgroundColor: isEarn ? "#f0fdf4" : "#fef2f2",
                  }}>
                    {isEarn ? "📈" : "🎁"}
                  </div>
                  <div style={{ flex: 1, minWidth: 0 }}>
                    <div style={{ fontWeight: 700, fontSize: 14, color: "#1e293b" }}>
                      {isEarn ? "Tích điểm" : "Dùng điểm"}
                    </div>
                    <div style={{ fontSize: 12, color: "#94a3b8", marginTop: 2 }}>
                      {h.description || `Booking #${h.bookingId}`}
                    </div>
                    <div style={{ fontSize: 11, color: "#cbd5e1", marginTop: 2 }}>
                      {new Date(String(h.createdAt).replace("Z", "")).toLocaleString("vi-VN")}
                    </div>
                  </div>
                  <div style={{
                    fontWeight: 800, fontSize: 15,
                    color: isEarn ? "#22c55e" : "#ef4444",
                  }}>
                    {isEarn ? "+" : "−"}{Math.abs(h.points).toLocaleString("vi-VN")}
                  </div>
                </div>
              );
            })}
          </div>
        )}
      </div>

      {/* Detail Modal */}
      {selectedTx && (
        <div 
          onClick={() => setSelectedTx(null)}
          style={{
            position: "fixed", inset: 0, zIndex: 9999, background: "rgba(15,23,42,0.6)", backdropFilter: "blur(4px)",
            display: "flex", alignItems: "center", justifyContent: "center", padding: 20
          }}
        >
          <div 
            onClick={e => e.stopPropagation()}
            style={{
              background: "#fff", borderRadius: 24, width: "100%", maxWidth: 400, overflow: "hidden",
              boxShadow: "0 20px 40px rgba(0,0,0,0.2)", display: "flex", flexDirection: "column"
            }}
          >
            {/* Header */}
            <div style={{ padding: "20px 24px", borderBottom: "1px solid #f1f5f9", display: "flex", justifyContent: "space-between", alignItems: "center" }}>
              <h3 style={{ fontSize: 18, fontWeight: 700, color: "#1e293b", margin: 0 }}>Chi tiết điểm thưởng</h3>
              <button 
                onClick={() => setSelectedTx(null)} 
                style={{ background: "none", border: "none", fontSize: 24, color: "#94a3b8", cursor: "pointer", display: "flex", alignItems: "center", justifyContent: "center", width: 32, height: 32, borderRadius: 8 }}
                onMouseEnter={e => e.currentTarget.style.background = "#f1f5f9"}
                onMouseLeave={e => e.currentTarget.style.background = "none"}
              >&times;</button>
            </div>
            
            {/* Content */}
            <div style={{ padding: 24 }}>
              <div style={{ textAlign: "center", marginBottom: 24 }}>
                <div style={{ fontSize: 48, marginBottom: 12 }}>
                  {selectedTx.type === "Earn" ? "📈" : "🎁"}
                </div>
                <div style={{ fontSize: 24, fontWeight: 800, color: selectedTx.type === "Earn" ? "#22c55e" : "#ef4444" }}>
                  {selectedTx.type === "Earn" ? "+" : "−"}{Math.abs(selectedTx.points || 0).toLocaleString("vi-VN")} điểm
                </div>
                <div style={{ fontSize: 15, fontWeight: 600, color: "#475569", marginTop: 4 }}>
                  {selectedTx.type === "Earn" ? "Tích điểm" : "Dùng điểm"}
                </div>
              </div>
              
              <div style={{ background: "#f8fafc", borderRadius: 16, padding: 16, display: "flex", flexDirection: "column", gap: 12 }}>
                <div style={{ display: "flex", justifyContent: "space-between" }}>
                  <span style={{ color: "#64748b", fontSize: 13 }}>Mã giao dịch</span>
                  <span style={{ color: "#1e293b", fontSize: 13, fontWeight: 600 }}>#{selectedTx.id}</span>
                </div>
                <div style={{ display: "flex", justifyContent: "space-between" }}>
                  <span style={{ color: "#64748b", fontSize: 13 }}>Thời gian</span>
                  <span style={{ color: "#1e293b", fontSize: 13, fontWeight: 600 }}>{new Date(String(selectedTx.createdAt).replace("Z", "")).toLocaleString("vi-VN")}</span>
                </div>
                {selectedTx.bookingId && (
                  <div style={{ display: "flex", justifyContent: "space-between" }}>
                    <span style={{ color: "#64748b", fontSize: 13 }}>Mã Booking</span>
                    <span style={{ color: "#1e293b", fontSize: 13, fontWeight: 600 }}>#{selectedTx.bookingId}</span>
                  </div>
                )}
                {selectedTx.description && (
                  <div style={{ borderTop: "1px dashed #cbd5e1", paddingTop: 12, marginTop: 4 }}>
                    <div style={{ color: "#64748b", fontSize: 13, marginBottom: 4 }}>Chi tiết</div>
                    <div style={{ color: "#1e293b", fontSize: 14, fontWeight: 500, lineHeight: 1.5 }}>
                      {selectedTx.description}
                    </div>
                  </div>
                )}

                {/* Booking detail section */}
                {selectedTx.bookingId && (
                  <div style={{ borderTop: "1px dashed #cbd5e1", paddingTop: 12, marginTop: 4 }}>
                    <div style={{ color: "#64748b", fontSize: 12, fontWeight: 700, marginBottom: 8, textTransform: "uppercase", letterSpacing: 0.5 }}>📋 Thông tin booking</div>
                    {bookingLoading ? (
                      <div style={{ textAlign: "center", padding: "12px 0", color: "#94a3b8", fontSize: 13 }}>Đang tải...</div>
                    ) : bookingDetail ? (
                      <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
                        {bookingDetail.stationName && (
                          <div style={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", gap: 8 }}>
                            <span style={{ color: "#64748b", fontSize: 12, flexShrink: 0 }}>🏪 Trạm sạc</span>
                            <span style={{ color: "#1e293b", fontSize: 12, fontWeight: 600, textAlign: "right" }}>{bookingDetail.stationName}</span>
                          </div>
                        )}
                        {bookingDetail.slotName && (
                          <div style={{ display: "flex", justifyContent: "space-between" }}>
                            <span style={{ color: "#64748b", fontSize: 12 }}>⚡ Trụ sạc</span>
                            <span style={{ color: "#1e293b", fontSize: 12, fontWeight: 600 }}>{bookingDetail.slotName}</span>
                          </div>
                        )}
                        {(bookingDetail.bookingDate || bookingDetail.date) && (
                          <div style={{ display: "flex", justifyContent: "space-between" }}>
                            <span style={{ color: "#64748b", fontSize: 12 }}>📅 Ngày</span>
                            <span style={{ color: "#1e293b", fontSize: 12, fontWeight: 600 }}>
                              {new Date(String(bookingDetail.bookingDate || bookingDetail.date).replace("Z","")).toLocaleDateString("vi-VN")}
                            </span>
                          </div>
                        )}
                        {(bookingDetail.startTime || bookingDetail.checkInTime) && (
                          <div style={{ display: "flex", justifyContent: "space-between" }}>
                            <span style={{ color: "#64748b", fontSize: 12 }}>🕐 Giờ</span>
                            <span style={{ color: "#1e293b", fontSize: 12, fontWeight: 600 }}>
                              {extractTime(bookingDetail.startTime || bookingDetail.checkInTime)}
                              {bookingDetail.endTime ? ` – ${extractTime(bookingDetail.endTime)}` : ""}
                            </span>
                          </div>
                        )}
                        {(bookingDetail.totalAmount != null || bookingDetail.totalCost != null) && (
                          <div style={{ display: "flex", justifyContent: "space-between" }}>
                            <span style={{ color: "#64748b", fontSize: 12 }}>💰 Tổng tiền</span>
                            <span style={{ color: "#f97316", fontSize: 12, fontWeight: 700 }}>
                              {(bookingDetail.totalAmount ?? bookingDetail.totalCost ?? 0).toLocaleString("vi-VN")}đ
                            </span>
                          </div>
                        )}
                        {bookingDetail.status && (
                          <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
                            <span style={{ color: "#64748b", fontSize: 12 }}>Trạng thái</span>
                            <span style={{
                              fontSize: 11, fontWeight: 700, padding: "2px 8px", borderRadius: 20,
                              background: BOOKING_STATUS_MAP[bookingDetail.status]?.bg ?? "#fefce8",
                              color: BOOKING_STATUS_MAP[bookingDetail.status]?.color ?? "#ca8a04",
                            }}>
                              {BOOKING_STATUS_MAP[bookingDetail.status]?.label ?? bookingDetail.status}
                            </span>
                          </div>
                        )}
                      </div>
                    ) : (
                      <div style={{ textAlign: "center", padding: "8px 0", color: "#cbd5e1", fontSize: 12 }}>Không thể tải thông tin booking</div>
                    )}
                  </div>
                )}
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
