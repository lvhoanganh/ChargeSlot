import React, { useEffect, useState } from "react";
import { adminStationApi } from "@/services/api";

const dayNames = [
  "Chủ nhật", "Thứ hai", "Thứ ba", "Thứ tư", "Thứ năm", "Thứ sáu", "Thứ bảy"
];

const statusConfig = {
  Open: { label: "Đang mở", color: "#22c55e", bg: "#f0fdf4" },
  Closed: { label: "Đã đóng", color: "#ef4444", bg: "#fef2f2" },
  Maintenance: { label: "Bảo trì", color: "#f97316", bg: "#fff7ed" },
};

const slotStatusConfig = {
  Available:  { label: "Trống", color: "#22c55e", bg: "#f0fdf4" },
  Active:     { label: "Trống", color: "#22c55e", bg: "#f0fdf4" },
  Occupied:   { label: "Đang dùng", color: "#ef4444", bg: "#fef2f2" },
  CheckedIn:  { label: "Đã check-in", color: "#06b6d4", bg: "#ecfeff" },
  Booked:     { label: "Đã đặt chỗ", color: "#f59e0b", bg: "#fffbeb" },
  Reserved:   { label: "Giữ chỗ", color: "#3b82f6", bg: "#eff6ff" },
  Maintenance:{ label: "Bảo trì", color: "#f97316", bg: "#fff7ed" },
  Inactive:   { label: "Ngưng", color: "#6b7280", bg: "#f3f4f6" },
};

export default function AdminStationDetailModal({ stationId, onClose }) {
  const [station, setStation] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  useEffect(() => {
    if (!stationId) return;
    let cancelled = false;
    setLoading(true);

    adminStationApi.getById(stationId)
      .then((data) => {
        if (!cancelled) setStation(data);
      })
      .catch((err) => {
        if (!cancelled) setError(err.message || "Lỗi tải chi tiết trạm");
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    return () => { cancelled = true; };
  }, [stationId]);

  if (!stationId) return null;

  return (
    <div style={{
      position: "fixed", top: 0, left: 0, right: 0, bottom: 0, zIndex: 9999,
      background: "rgba(0,0,0,0.5)", display: "flex", alignItems: "center", justifyContent: "center", padding: "20px"
    }}>
      <div style={{
        background: "#f8fafc", width: "100%", maxWidth: "800px", maxHeight: "90vh", borderRadius: "16px",
        display: "flex", flexDirection: "column", boxShadow: "0 20px 25px -5px rgba(0,0,0,0.1), 0 10px 10px -5px rgba(0,0,0,0.04)"
      }}>
        
        {/* Header */}
        <div style={{ padding: "20px 24px", background: "white", borderBottom: "1px solid #e2e8f0", borderRadius: "16px 16px 0 0", display: "flex", justifyContent: "space-between", alignItems: "center" }}>
          <div>
            <h2 style={{ margin: 0, fontSize: "20px", fontWeight: 700, color: "#1e293b" }}>Chi tiết trạm sạc</h2>
            {station?.name && <p style={{ margin: "4px 0 0", fontSize: "14px", color: "#64748b" }}>{station.name}</p>}
          </div>
          <button onClick={onClose} style={{ background: "none", border: "none", cursor: "pointer", color: "#64748b", padding: "8px", borderRadius: "8px" }} onMouseEnter={e => e.currentTarget.style.background = "#f1f5f9"} onMouseLeave={e => e.currentTarget.style.background = "none"}>
            <svg width="24" height="24" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M6 18L18 6M6 6l12 12"/></svg>
          </button>
        </div>

        {/* Content Box */}
        <div style={{ flex: 1, overflowY: "auto", padding: "24px" }}>
          {loading ? (
            <div style={{ textAlign: "center", padding: "40px" }}>Đang tải dữ liệu...</div>
          ) : error ? (
            <div style={{ padding: "20px", background: "#fef2f2", color: "#ef4444", borderRadius: "12px", border: "1px solid #fecaca" }}>{error}</div>
          ) : !station ? (
            <div style={{ textAlign: "center", padding: "40px", color: "#64748b" }}>Không tìm thấy trạm.</div>
          ) : (
            <div style={{ display: "flex", flexDirection: "column", gap: "20px" }}>
              
              {/* Basic Info */}
              <div style={{ background: "white", padding: "20px", borderRadius: "16px", border: "1px solid #f1f5f9" }}>
                 <div style={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", marginBottom: "12px" }}>
                    <h3 style={{ margin: 0, fontSize: "18px", fontWeight: 700, color: "#0f172a" }}>{station.name}</h3>
                    <span style={{ fontSize: "12px", fontWeight: 600, padding: "4px 10px", borderRadius: "20px", background: statusConfig[station.operationalStatus]?.bg || "#f1f5f9", color: statusConfig[station.operationalStatus]?.color || "#64748b" }}>
                       {statusConfig[station.operationalStatus]?.label || station.operationalStatus || "Không rõ"}
                    </span>
                 </div>
                 <p style={{ margin: 0, fontSize: "14px", color: "#64748b", display: "flex", alignItems: "center", gap: "6px" }}>
                    Vị trí: {station.address}
                 </p>
                 {station.description && (
                   <p style={{ margin: "12px 0 0", fontSize: "14px", color: "#475569", lineHeight: 1.5, padding: "12px", background: "#f8fafc", borderRadius: "8px" }}>
                      {station.description}
                   </p>
                 )}
              </div>

              {/* Station Images */}
              {(() => {
                const rawImages = station.images || station.stationImages || [];
                if (!rawImages || rawImages.length === 0) return null;
                const BASE_URL = "https://chargeslot-api-f8b5brexe2b0ekhp.japaneast-01.azurewebsites.net";
                const buildUrl = (raw) => {
                  if (!raw) return null;
                  if (raw.startsWith("http://") || raw.startsWith("https://")) return raw;
                  return `${BASE_URL}${raw.startsWith("/") ? "" : "/"}${raw}`;
                };
                const images = rawImages.map((img, idx) => {
                  if (typeof img === "string") return { key: idx, src: buildUrl(img) };
                  const rawUrl = img.imageUrl || img.url || img.imagePath || img.path || "";
                  return { key: img.id ?? idx, src: buildUrl(rawUrl) };
                }).filter(i => i.src);

                if (images.length === 0) return null;

                return (
                  <div style={{ background: "white", padding: "20px", borderRadius: "16px", border: "1px solid #f1f5f9" }}>
                    <h4 style={{ margin: "0 0 16px", fontSize: "15px", fontWeight: 600, color: "#1e293b" }}>Hình ảnh trạm sạc</h4>
                    <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fill, minmax(180px, 1fr))", gap: "12px" }}>
                      {images.map(img => (
                        <div key={img.key} style={{ aspectRatio: "16/9", borderRadius: "8px", overflow: "hidden", background: "#e2e8f0" }}>
                          <img src={img.src} style={{ width: "100%", height: "100%", objectFit: "cover" }} alt="Station image" onError={(e) => e.target.style.display = 'none'} />
                        </div>
                      ))}
                    </div>
                  </div>
                );
              })()}

              <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "20px" }}>
                  {/* Slots */}
                  <div style={{ background: "white", padding: "20px", borderRadius: "16px", border: "1px solid #f1f5f9" }}>
                    <h4 style={{ margin: "0 0 16px", fontSize: "15px", fontWeight: 600, color: "#1e293b" }}>Các slot sạc ({(station.chargingSlots || []).length})</h4>
                    <div style={{ display: "flex", flexDirection: "column", gap: "8px" }}>
                      {(station.chargingSlots || []).map(slot => {
                        const ss = slotStatusConfig[slot.status] || slotStatusConfig.Available;
                        return (
                          <div key={slot.id} style={{ display: "flex", justifyContent: "space-between", alignItems: "center", padding: "10px 12px", background: ss.bg, borderRadius: "8px", border: "1px solid rgba(0,0,0,0.03)" }}>
                            <div style={{ display: "flex", alignItems: "center", gap: "10px" }}>
                              <div style={{ width: "8px", height: "8px", borderRadius: "50%", background: ss.color }}></div>
                              <span style={{ fontSize: "14px", fontWeight: 600, color: "#1e293b" }}>{slot.slotName}</span>
                            </div>
                            <span style={{ fontSize: "12px", fontWeight: 600, color: ss.color }}>{ss.label}</span>
                          </div>
                        )
                      })}
                      {!(station.chargingSlots?.length) && <p style={{ fontSize: "13px", color: "#94a3b8" }}>Chưa có slot nào.</p>}
                    </div>
                  </div>

                  <div style={{ display: "flex", flexDirection: "column", gap: "20px" }}>
                      {/* Price Tiers */}
                      <div style={{ background: "white", padding: "20px", borderRadius: "16px", border: "1px solid #f1f5f9" }}>
                        <h4 style={{ margin: "0 0 16px", fontSize: "15px", fontWeight: 600, color: "#1e293b" }}>Giá theo khung giờ</h4>
                        <div style={{ display: "flex", flexDirection: "column", gap: "8px" }}>
                          {(station.pricingTiers || []).filter(t => t.isActive !== false).map((tier, idx) => (
                            <div key={idx} style={{ display: "flex", justifyContent: "space-between", padding: "8px 12px", background: "#f8fafc", borderRadius: "8px" }}>
                              <span style={{ fontSize: "13px", color: "#64748b" }}>{String(tier.startTime).substring(0,5)} – {String(tier.endTime).substring(0,5)}</span>
                              <span style={{ fontSize: "13px", fontWeight: 700, color: "#d97706" }}>{tier.pricePerHour?.toLocaleString("vi-VN")}đ/h</span>
                            </div>
                          ))}
                          {!(station.pricingTiers?.length) && <p style={{ fontSize: "13px", color: "#94a3b8" }}>Không có thiết lập giá.</p>}
                        </div>
                      </div>

                      {/* Operating Hours */}
                      <div style={{ background: "white", padding: "20px", borderRadius: "16px", border: "1px solid #f1f5f9" }}>
                        <h4 style={{ margin: "0 0 16px", fontSize: "15px", fontWeight: 600, color: "#1e293b" }}>Giờ hoạt động</h4>
                        <div style={{ display: "flex", flexDirection: "column", gap: "8px" }}>
                          {[1, 2, 3, 4, 5, 6, 0].map(day => {
                            const hours = (station.operatingHours || []).find(h => h.dayOfWeek === day);
                            return (
                              <div key={day} style={{ display: "flex", justifyContent: "space-between", fontSize: "13px" }}>
                                <span style={{ color: "#64748b" }}>{dayNames[day]}</span>
                                <span style={{ fontWeight: 600, color: hours && !hours.isClosed ? "#1e293b" : "#ef4444" }}>
                                  {hours && !hours.isClosed ? `${hours.openTime} - ${hours.closeTime}` : "Đóng cửa"}
                                </span>
                              </div>
                            )
                          })}
                        </div>
                      </div>
                  </div>
              </div>

              {/* Extra Services */}
              {(station.extraServices || []).filter(es => es.isActive).length > 0 && (
                <div style={{ background: "white", padding: "20px", borderRadius: "16px", border: "1px solid #f1f5f9" }}>
                   <h4 style={{ margin: "0 0 16px", fontSize: "15px", fontWeight: 600, color: "#1e293b" }}>Dịch vụ bổ sung</h4>
                   <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "12px" }}>
                     {station.extraServices.filter(es => es.isActive).map(es => (
                       <div key={es.id || es.serviceName} style={{ padding: "12px", background: "#f8fafc", borderRadius: "8px", border: "1px solid #e2e8f0" }}>
                         <div style={{ fontWeight: 600, fontSize: "14px", color: "#1e293b" }}>{es.serviceName}</div>
                         {es.description && <div style={{ fontSize: "12px", color: "#64748b", marginTop: "4px" }}>{es.description}</div>}
                         <div style={{ marginTop: "8px", fontSize: "14px", fontWeight: 700, color: es.price > 0 ? "#7c3aed" : "#16a34a" }}>
                           {es.price > 0 ? `${es.price.toLocaleString("vi-VN")}đ` : "Miễn phí"}
                         </div>
                       </div>
                     ))}
                   </div>
                </div>
              )}

            </div>
          )}
        </div>

        {/* Footer */}
        <div style={{ padding: "16px 24px", background: "white", borderTop: "1px solid #e2e8f0", borderRadius: "0 0 16px 16px", display: "flex", justifyContent: "flex-end" }}>
          <button onClick={onClose} style={{ padding: "10px 24px", background: "#f1f5f9", color: "#334155", border: "none", borderRadius: "8px", fontWeight: 600, cursor: "pointer" }} onMouseEnter={e => e.currentTarget.style.background = "#e2e8f0"} onMouseLeave={e => e.currentTarget.style.background = "#f1f5f9"}>
            Đóng
          </button>
        </div>

      </div>
    </div>
  );
}
