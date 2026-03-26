import { useState, useEffect } from "react";
import { useParams, useNavigate } from "react-router-dom";
import { publicStationApi, bookingApi, slotApi, loyaltyApi } from "@/services/api";

export default function BookingForm() {
  const { stationId } = useParams();
  const navigate = useNavigate();
  const [station, setStation] = useState(null);
  const [loading, setLoading] = useState(true);
  const [selectedSlot, setSelectedSlot] = useState(null);
  const [startTime, setStartTime] = useState("");
  const [duration, setDuration] = useState(1);
  const [note, setNote] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const [apiError, setApiError] = useState("");
  const [bookedRanges, setBookedRanges] = useState([]);
  const [selectedExtras, setSelectedExtras] = useState({});
  const [loyaltyInfo, setLoyaltyInfo] = useState(null);
  const [pointsToUse, setPointsToUse] = useState(0);

  useEffect(() => {
    publicStationApi.getById(Number(stationId))
      .then(setStation)
      .catch(() => setStation(null))
      .finally(() => setLoading(false));
  }, [stationId]);

  // Fetch loyalty info
  useEffect(() => {
    loyaltyApi.getInfo()
      .then(setLoyaltyInfo)
      .catch(() => setLoyaltyInfo(null));
  }, []);

  // Fetch booked ranges khi chọn slot hoặc đổi ngày
  useEffect(() => {
    if (!selectedSlot || !stationId) { setBookedRanges([]); return; }
    const dateStr = startTime ? new Date(startTime).toISOString().slice(0, 10) : undefined;
    slotApi.getAvailability(Number(stationId), selectedSlot, dateStr)
      .then(data => setBookedRanges(data.bookedRanges || []))
      .catch(() => setBookedRanges([]));
  }, [selectedSlot, stationId, startTime ? startTime.slice(0, 10) : ""]);

  // Tính tổng tiền giống logic BE: CalculateTotalPrice — chia booking theo từng khung giá
  const calculateChargingAmount = () => {
    if (!station || !selectedSlot || !startTime) return 0;
    const tiers = (station.pricingTiers || []).filter(t => t.isActive !== false);
    if (tiers.length === 0) return 0;

    const startObj = new Date(startTime);
    const endObj = new Date(startObj.getTime() + duration * 3600000);

    const toMin = (str) => {
      if (!str) return 0;
      const [h, m] = String(str).split(':');
      return parseInt(h) * 60 + parseInt(m);
    };

    let total = 0;
    let current = new Date(startObj);
    let maxIter = 200;

    while (current < endObj && maxIter-- > 0) {
      const currentMin = current.getHours() * 60 + current.getMinutes();

      let tier = tiers.find(t => {
        const tStart = toMin(t.startTime);
        const tEnd = toMin(t.endTime);
        return currentMin >= tStart && currentMin < tEnd;
      });

      if (!tier) {
        tier = tiers.find(t => {
          const tStart = toMin(t.startTime);
          const tEnd = toMin(t.endTime);
          return currentMin >= tStart && currentMin <= tEnd;
        });
      }

      if (!tier) tier = tiers[0];

      const tierEndMin = toMin(tier.endTime);
      const tierEndDate = new Date(current);
      tierEndDate.setHours(0, 0, 0, 0);
      if (tierEndMin === 23 * 60 + 59 || tierEndMin === 0) {
        tierEndDate.setDate(tierEndDate.getDate() + 1);
      } else {
        tierEndDate.setHours(Math.floor(tierEndMin / 60), tierEndMin % 60, 0, 0);
      }

      if (tierEndDate <= current) {
        const nextDay = new Date(current);
        nextDay.setHours(0, 0, 0, 0);
        nextDay.setDate(nextDay.getDate() + 1);
        const segmentEnd = endObj < nextDay ? endObj : nextDay;
        const hours = (segmentEnd - current) / 3600000;
        if (hours > 0) {
          total += hours * (tier.pricePerHour || 0);
        }
        current = segmentEnd;
        continue;
      }

      const segmentEnd = endObj < tierEndDate ? endObj : tierEndDate;
      const hours = (segmentEnd - current) / 3600000;

      if (hours > 0) {
        total += hours * (tier.pricePerHour || 0);
      }

      current = segmentEnd;
    }

    return Math.round(total);
  };

  // Tính tiền dịch vụ bổ sung
  const calculateServiceAmount = () => {
    if (!station) return 0;
    const extras = station.extraServices || [];
    let total = 0;
    for (const [id, qty] of Object.entries(selectedExtras)) {
      if (qty <= 0) continue;
      const svc = extras.find(e => e.id === Number(id));
      if (svc) total += svc.price * qty;
    }
    return total;
  };

  const calculateTotalAmount = () => calculateChargingAmount() + calculateServiceAmount();

  // Max points the driver can use
  const maxPoints = (() => {
    if (!loyaltyInfo) return 0;
    const total = calculateTotalAmount();
    const maxByRate = Math.floor(total * (loyaltyInfo.maxRedeemRate || 0));
    return Math.min(loyaltyInfo.currentPoints || 0, maxByRate);
  })();

  const finalAmount = Math.max(0, calculateTotalAmount() - pointsToUse);

  // Helper: thay đổi số lượng dịch vụ
  const updateExtraQty = (serviceId, delta) => {
    setSelectedExtras(prev => {
      const current = prev[serviceId] || 0;
      const svc = (station?.extraServices || []).find(e => e.id === serviceId);
      const maxQty = svc?.totalStock != null ? Math.min(10, svc.totalStock) : 10;
      const next = Math.max(0, Math.min(maxQty, current + delta));
      if (next === 0) {
        const { [serviceId]: _, ...rest } = prev;
        return rest;
      }
      return { ...prev, [serviceId]: next };
    });
  };

  // Default start time to nearest future hour
  useEffect(() => {
    const now = new Date();
    now.setMinutes(0, 0, 0);
    now.setHours(now.getHours() + 1);
    const iso = new Date(now.getTime() - now.getTimezoneOffset() * 60000).toISOString().slice(0, 16);
    setStartTime(iso);
  }, []);

  // Realtime validation: kiểm tra giờ có nằm trong khung giá không
  const timeError = (() => {
    if (!station || !startTime) return "";
    const tiers = (station.pricingTiers || []).filter(t => t.isActive !== false);
    if (tiers.length === 0) return "";

    const toMin = (str) => {
      if (!str) return 0;
      const [h, m] = String(str).split(':');
      return parseInt(h) * 60 + parseInt(m);
    };
    const tierStarts = tiers.map(t => toMin(t.startTime));
    const tierEnds = tiers.map(t => toMin(t.endTime));
    const minTier = Math.min(...tierStarts);
    const maxTier = Math.max(...tierEnds);
    const fmtMin = (m) => `${String(Math.floor(m / 60)).padStart(2, '0')}:${String(m % 60).padStart(2, '0')}`;

    const startObj = new Date(startTime);
    const endObj = new Date(startObj.getTime() + duration * 3600000);
    const bookStartMin = startObj.getHours() * 60 + startObj.getMinutes();
    const bookEndMin = endObj.getHours() * 60 + endObj.getMinutes() + (endObj.getDate() !== startObj.getDate() ? 24 * 60 : 0);

    if (bookStartMin < minTier || bookStartMin >= maxTier) {
      return `⚠️ Giờ bắt đầu phải trong khung ${fmtMin(minTier)} – ${fmtMin(maxTier)}`;
    }
    if (bookEndMin > maxTier && maxTier !== 0) {
      return `⚠️ Giờ kết thúc (${fmtMin(bookEndMin > 24 * 60 ? bookEndMin - 24 * 60 : bookEndMin)}) vượt quá khung giá (${fmtMin(maxTier)}). Giảm thời lượng!`;
    }
    return "";
  })();

  async function handleSubmit(e) {
    e.preventDefault();
    if (!selectedSlot) return setApiError("Vui lòng chọn slot sạc");
    if (!startTime) return setApiError("Vui lòng chọn thời gian bắt đầu");

    // Lấy thông tin ngày giờ
    const startObj = new Date(startTime);
    const endObj = new Date(startObj.getTime() + duration * 3600000);

    // Kiểm tra giờ trong khung giá
    const tiers = (station.pricingTiers || []).filter(t => t.isActive !== false);
    if (tiers.length > 0) {
      const toMin = (str) => {
        if (!str) return 0;
        const [h, m] = String(str).split(':');
        return parseInt(h) * 60 + parseInt(m);
      };
      const tierStarts = tiers.map(t => toMin(t.startTime));
      const tierEnds = tiers.map(t => toMin(t.endTime));
      const minTier = Math.min(...tierStarts);
      const maxTier = Math.max(...tierEnds);

      const bookStartMin = startObj.getHours() * 60 + startObj.getMinutes();
      const bookEndMin = endObj.getHours() * 60 + endObj.getMinutes() + (endObj.getDate() !== startObj.getDate() ? 24 * 60 : 0);

      const fmtMin = (m) => `${String(Math.floor(m / 60)).padStart(2, '0')}:${String(m % 60).padStart(2, '0')}`;

      if (bookStartMin < minTier || bookStartMin >= maxTier) {
        return setApiError(`Giờ bắt đầu phải nằm trong khung ${fmtMin(minTier)} – ${fmtMin(maxTier)}!`);
      }
      if (bookEndMin > maxTier && maxTier !== 0) {
        return setApiError(`Giờ kết thúc (${fmtMin(bookEndMin > 24 * 60 ? bookEndMin - 24 * 60 : bookEndMin)}) vượt quá khung giá (${fmtMin(maxTier)}). Vui lòng giảm thời lượng!`);
      }
    }

    // Kiểm tra giờ hoạt động của trạm
    const dayOfWeek = startObj.getDay();
    const opHours = station.operatingHours?.find(h => h.dayOfWeek === dayOfWeek);

    if (opHours) {
      if (opHours.isClosed) {
        return setApiError("Trạm sạc đóng cửa vào ngày bạn chọn!");
      }

      const timeToStrMin = (tStr) => {
        if (!tStr) return 0;
        const [h, m] = tStr.split(":");
        return parseInt(h) * 60 + parseInt(m);
      };

      const bookStart = startObj.getHours() * 60 + startObj.getMinutes();
      const bookEnd = endObj.getHours() * 60 + endObj.getMinutes() + (endObj.getDate() !== startObj.getDate() ? 24 * 60 : 0);

      const opStart = timeToStrMin(opHours.openTime);
      let opEnd = timeToStrMin(opHours.closeTime);

      // Mở 00:00 đóng 00:00 là cả ngày
      if (opStart === 0 && opEnd === 0) {
        opEnd = 24 * 60;
      } else if (opEnd <= opStart) {
        // Vd mở 18:00 đóng 06:00
        opEnd += 24 * 60;
      }

      if (bookStart < opStart || bookEnd > opEnd) {
        const fmtOpen = String(opHours.openTime).substring(0, 5);
        const fmtClose = String(opHours.closeTime).substring(0, 5);
        return setApiError(`Trạm chỉ hoạt động từ ${fmtOpen} đến ${fmtClose === "00:00" ? "24:00" : fmtClose} vào thứ ${dayOfWeek === 0 ? "Chủ Nhật" : dayOfWeek + 1}!`);
      }
    }

    setSubmitting(true);
    setApiError("");

    try {
      // Build extraServices array
      const extraServices = Object.entries(selectedExtras)
        .filter(([, qty]) => qty > 0)
        .map(([id, qty]) => ({ serviceId: Number(id), quantity: qty }));

      const result = await bookingApi.create({
        slotId: selectedSlot,
        startTime: startTime + ":00",
        durationHours: parseFloat(duration),
        note: note || undefined,
        extraServices: extraServices.length > 0 ? extraServices : undefined,
        pointsToUse: pointsToUse > 0 ? pointsToUse : 0,
      });
      navigate("/driver/my-bookings");
    } catch (err) {
      setApiError(err.message || "Đặt lịch thất bại");
    } finally {
      setSubmitting(false);
    }
  }

  if (loading) {
    return (
      <div style={{ minHeight: "100vh", background: "#f8fafc", paddingTop: 100, textAlign: "center" }}>
        <div style={{ fontSize: 40, marginBottom: 8 }}>⚡</div>
        <p style={{ color: "#6b7280" }}>Đang tải thông tin trạm sạc...</p>
      </div>
    );
  }

  if (!station) {
    return (
      <div style={{ minHeight: "100vh", background: "#f8fafc", paddingTop: 100, textAlign: "center" }}>
        <div style={{ fontSize: 48, marginBottom: 8 }}>🔌</div>
        <h2 style={{ fontSize: 20, fontWeight: 700, color: "#1e293b" }}>Không tìm thấy trạm sạc</h2>
        <button onClick={() => navigate("/driver/map")} style={btnBack}>← Quay lại bản đồ</button>
      </div>
    );
  }

  const slots = station.chargingSlots || [];

  return (
    <div style={{ minHeight: "100vh", background: "#f8fafc", paddingTop: 90 }}>
      <div style={{ maxWidth: 600, margin: "0 auto", padding: "0 16px 40px" }}>
        {/* Header */}
        <button onClick={() => navigate(-1)} style={{ background: "none", border: "none", cursor: "pointer", color: "#6b7280", fontSize: 14, marginBottom: 12, display: "flex", alignItems: "center", gap: 4 }}>
          <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><path d="M19 12H5M12 19l-7-7 7-7" /></svg>
          Quay lại
        </button>

        <div style={{ background: "#fff", borderRadius: 20, boxShadow: "0 2px 12px rgba(0,0,0,0.06)", padding: 24 }}>
          <h1 style={{ fontSize: 22, fontWeight: 800, color: "#1e293b", marginBottom: 4 }}>Đặt lịch sạc</h1>
          <p style={{ fontSize: 14, color: "#64748b", marginBottom: 24 }}>📍 {station.name} — {station.address}</p>

          <form onSubmit={handleSubmit}>
            {/* Slot selection */}
            <label style={labelStyle}>Chọn slot sạc</label>
            <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fill, minmax(140px, 1fr))", gap: 8, marginBottom: 20 }}>
              {slots.map((slot) => {
                const isActive = slot.status === "Active" || slot.status === "Available";
                const isSelected = selectedSlot === slot.id;
                const tiers = (station.pricingTiers || []).filter(t => t.isActive !== false);
                const canSelect = isActive || slot.status === "Booked";
                return (
                  <button
                    type="button"
                    key={slot.id}
                    disabled={!canSelect}
                    onClick={() => setSelectedSlot(slot.id)}
                    style={{
                      padding: "12px 10px",
                      borderRadius: 12,
                      border: isSelected ? "2px solid #f97316" : "2px solid #e5e7eb",
                      background: isSelected ? "#fff7ed" : !canSelect ? "#f3f4f6" : "#fff",
                      cursor: canSelect ? "pointer" : "not-allowed",
                      opacity: canSelect ? 1 : 0.5,
                      textAlign: "center",
                    }}
                  >
                    <div style={{ fontWeight: 700, fontSize: 14, color: "#1e293b" }}>{slot.slotName}</div>

                    <div style={{ fontSize: 11, color: slot.status === "Booked" ? "#f59e0b" : isActive ? "#22c55e" : "#ef4444", marginTop: 2 }}>
                      {isActive ? "Trống" : slot.status === "Booked" ? "Có lịch đặt" : slot.status === "Inactive" ? "Ngưng" : slot.status === "Maintenance" ? "Bảo trì" : slot.status}
                    </div>
                  </button>
                );
              })}
            </div>

            {/* Pricing tiers for selected slot */}
            {selectedSlot && (() => {
              const slot = slots.find(s => s.id === selectedSlot);
              const tiers = (station?.pricingTiers || []).filter(t => t.isActive !== false);
              if (tiers.length === 0) return null;
              return (
                <div style={{ background: "#fffbeb", borderRadius: 10, padding: "10px 14px", marginBottom: 20, border: "1px solid #fde68a" }}>
                  <div style={{ fontSize: 12, fontWeight: 700, color: "#92400e", marginBottom: 6 }}>⏰ Giá theo khung giờ — {station.name}</div>
                  {tiers.map((tier, idx) => (
                    <div key={idx} style={{ fontSize: 12, color: "#78350f", display: "flex", justifyContent: "space-between", marginBottom: 2 }}>
                      <span>{String(tier.startTime).substring(0, 5)} – {String(tier.endTime).substring(0, 5)}</span>
                      <span style={{ fontWeight: 700, color: "#d97706" }}>{tier.pricePerHour?.toLocaleString("vi-VN")}đ/h</span>
                    </div>
                  ))}
                </div>
              );
            })()}

            {/* Booked time ranges */}
            {selectedSlot && bookedRanges.length > 0 && (
              <div style={{ background: "#fef3c7", borderRadius: 10, padding: "10px 14px", marginBottom: 20, border: "1px solid #fde68a" }}>
                <div style={{ fontSize: 12, fontWeight: 700, color: "#92400e", marginBottom: 6 }}>📅 Khung giờ đã được đặt</div>
                {bookedRanges.map((r, idx) => {
                  const parseVN = (t) => new Date(String(t).replace("Z", ""));
                  const start = parseVN(r.startTime);
                  const end = parseVN(r.endTime);
                  const fmtTime = (d) => d.toLocaleTimeString("vi-VN", { hour: "2-digit", minute: "2-digit", hour12: false });
                  const fmtDate = (d) => d.toLocaleDateString("vi-VN", { day: "2-digit", month: "2-digit" });
                  return (
                    <div key={idx} style={{ fontSize: 12, color: "#78350f", display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 2 }}>
                      <span>🔴 {fmtTime(start)} – {fmtTime(end)} ({fmtDate(start)})</span>
                      <span style={{ fontSize: 10, color: "#b45309", background: "#fef9c3", padding: "1px 6px", borderRadius: 6 }}>
                        {r.status === "Confirmed" ? "Đã xác nhận" : r.status === "PendingPayment" ? "Chờ thanh toán" : r.status === "WaitingOwner" ? "Chờ duyệt" : r.status === "CheckedIn" ? "Đã check-in" : r.status === "Charging" ? "Đang sạc" : r.status}
                      </span>
                    </div>
                  );
                })}
              </div>
            )}

            {/* Start time */}
            <label style={labelStyle}>Thời gian bắt đầu</label>
            <input
              type="datetime-local"
              value={startTime}
              onChange={(e) => setStartTime(e.target.value)}
              style={inputStyle}
            />

            {/* Duration */}
            <label style={labelStyle}>Thời lượng (giờ)</label>
            <select value={duration} onChange={(e) => setDuration(e.target.value)} style={inputStyle}>
              {[0.5, 1, 1.5, 2, 3, 4, 5, 6, 8, 10, 12, 24].map((h) => (
                <option key={h} value={h}>{h} giờ</option>
              ))}
            </select>

            {timeError && (
              <div style={{ background: "#fef2f2", color: "#dc2626", padding: "10px 14px", borderRadius: 10, fontSize: 13, marginBottom: 16, marginTop: -8, border: "1px solid #fecaca" }}>
                {timeError}
              </div>
            )}

            {/* Note */}
            <label style={labelStyle}>Ghi chú (tùy chọn)</label>
            <textarea
              value={note}
              onChange={(e) => setNote(e.target.value)}
              placeholder="Ghi chú cho chủ trạm..."
              rows={3}
              style={{ ...inputStyle, resize: "vertical" }}
            />

            {/* Extra Services */}
            {station.extraServices && station.extraServices.filter(es => es.isActive).length > 0 && (
              <div style={{ marginBottom: 20 }}>
                <label style={labelStyle}>Dịch vụ bổ sung (tùy chọn)</label>
                <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
                  {station.extraServices.filter(es => es.isActive).map(es => {
                    const qty = selectedExtras[es.id] || 0;
                    const maxQty = es.totalStock != null ? Math.min(10, es.totalStock) : 10;
                    return (
                      <div key={es.id} style={{
                        display: "flex", alignItems: "center", justifyContent: "space-between",
                        padding: "10px 14px", borderRadius: 12,
                        border: qty > 0 ? "2px solid #a855f7" : "1.5px solid #e5e7eb",
                        background: qty > 0 ? "#faf5ff" : "#fff",
                      }}>
                        <div style={{ flex: 1 }}>
                          <div style={{ fontSize: 13, fontWeight: 600, color: "#1e293b" }}>{es.serviceName}</div>
                          {es.description && <div style={{ fontSize: 11, color: "#6b7280" }}>{es.description}</div>}
                          <div style={{ fontSize: 12, fontWeight: 700, color: "#7c3aed", marginTop: 2 }}>
                            {es.price.toLocaleString("vi-VN")}đ
                            {es.totalStock != null && <span style={{ fontWeight: 400, color: "#9ca3af", marginLeft: 6 }}>Còn {es.totalStock}</span>}
                          </div>
                        </div>
                        <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
                          <button type="button" onClick={() => updateExtraQty(es.id, -1)} disabled={qty <= 0}
                            style={{ width: 28, height: 28, borderRadius: 8, border: "1.5px solid #d1d5db", background: qty > 0 ? "#fff" : "#f3f4f6", cursor: qty > 0 ? "pointer" : "not-allowed", fontSize: 16, fontWeight: 700, color: "#374151", display: "flex", alignItems: "center", justifyContent: "center" }}>−</button>
                          <span style={{ fontSize: 14, fontWeight: 700, color: "#1e293b", minWidth: 20, textAlign: "center" }}>{qty}</span>
                          <button type="button" onClick={() => updateExtraQty(es.id, 1)} disabled={qty >= maxQty}
                            style={{ width: 28, height: 28, borderRadius: 8, border: "1.5px solid #d1d5db", background: qty < maxQty ? "#fff" : "#f3f4f6", cursor: qty < maxQty ? "pointer" : "not-allowed", fontSize: 16, fontWeight: 700, color: "#374151", display: "flex", alignItems: "center", justifyContent: "center" }}>+</button>
                        </div>
                      </div>
                    );
                  })}
                </div>
              </div>
            )}

            {/* Loyalty Points */}
            {loyaltyInfo && loyaltyInfo.currentPoints > 0 && selectedSlot && calculateTotalAmount() > 0 && (
              <div style={{ marginBottom: 20 }}>
                <label style={labelStyle}>🏆 Dùng điểm tích lũy ({loyaltyInfo.currentPoints.toLocaleString("vi-VN")} điểm khả dụng)</label>
                <div style={{ display: "flex", alignItems: "center", gap: 12 }}>
                  <input
                    type="range"
                    min={0}
                    max={maxPoints}
                    step={100}
                    value={pointsToUse}
                    onChange={e => setPointsToUse(Number(e.target.value))}
                    style={{ flex: 1, accentColor: "#7c3aed" }}
                  />
                  <input
                    type="number"
                    min={0}
                    max={maxPoints}
                    value={pointsToUse}
                    onChange={e => {
                      const v = Math.min(Math.max(0, Number(e.target.value) || 0), maxPoints);
                      setPointsToUse(v);
                    }}
                    style={{ width: 100, padding: "6px 10px", borderRadius: 8, border: "1.5px solid #e5e7eb", fontSize: 14, textAlign: "right", outline: "none" }}
                  />
                </div>
                {pointsToUse > 0 && (
                  <div style={{ fontSize: 12, color: "#7c3aed", marginTop: 4, fontWeight: 600 }}>
                    Giảm {pointsToUse.toLocaleString("vi-VN")}đ từ điểm tích lũy
                  </div>
                )}
                <div style={{ fontSize: 11, color: "#9ca3af", marginTop: 2 }}>
                  Tối đa dùng {((loyaltyInfo.maxRedeemRate || 0) * 100).toFixed(0)}% giá trị booking bằng điểm
                </div>
              </div>
            )}

            {/* Summary */}
            {selectedSlot && (
              <div style={{ background: "#f8fafc", borderRadius: 12, padding: 16, marginBottom: 20 }}>
                <div style={{ fontSize: 13, fontWeight: 700, color: "#1e293b", marginBottom: 8 }}>Tóm tắt</div>
                <div style={{ fontSize: 13, color: "#64748b", display: "flex", justifyContent: "space-between", marginBottom: 4 }}>
                  <span>Trụ sạc</span>
                  <span style={{ fontWeight: 600, color: "#1e293b" }}>{slots.find((s) => s.id === selectedSlot)?.slotName}</span>
                </div>
                <div style={{ fontSize: 13, color: "#64748b", display: "flex", justifyContent: "space-between", marginBottom: 4 }}>
                  <span>Thời lượng</span>
                  <span style={{ fontWeight: 600, color: "#1e293b" }}>{duration} giờ</span>
                </div>
                <div style={{ fontSize: 13, color: "#64748b", display: "flex", justifyContent: "space-between", marginBottom: 4 }}>
                  <span>Phí sạc</span>
                  <span style={{ fontWeight: 600, color: "#1e293b" }}>{calculateChargingAmount().toLocaleString("vi-VN")}đ</span>
                </div>
                {calculateServiceAmount() > 0 && (
                  <div style={{ fontSize: 13, color: "#64748b", display: "flex", justifyContent: "space-between", marginBottom: 4 }}>
                    <span>Dịch vụ bổ sung</span>
                    <span style={{ fontWeight: 600, color: "#7c3aed" }}>{calculateServiceAmount().toLocaleString("vi-VN")}đ</span>
                  </div>
                )}
                {pointsToUse > 0 && (
                  <div style={{ fontSize: 13, color: "#64748b", display: "flex", justifyContent: "space-between", marginBottom: 4 }}>
                    <span>🏆 Giảm từ điểm</span>
                    <span style={{ fontWeight: 600, color: "#7c3aed" }}>−{pointsToUse.toLocaleString("vi-VN")}đ</span>
                  </div>
                )}
                <div style={{ borderTop: "1px solid #e5e7eb", marginTop: 6, paddingTop: 6, fontSize: 13, color: "#64748b", display: "flex", justifyContent: "space-between" }}>
                  <span style={{ fontWeight: 700 }}>Tổng cộng</span>
                  <span style={{ fontWeight: 700, color: "#f97316", fontSize: 15 }}>
                    {finalAmount.toLocaleString("vi-VN")}đ
                  </span>
                </div>
              </div>
            )}

            {apiError && (
              <div style={{ background: "#fef2f2", color: "#dc2626", padding: "10px 14px", borderRadius: 10, fontSize: 13, marginBottom: 16 }}>
                ⚠️ {apiError}
              </div>
            )}

            <button
              type="submit"
              disabled={submitting || !selectedSlot || !!timeError}
              style={{
                width: "100%", padding: "14px 0", borderRadius: 14, border: "none",
                background: submitting || !selectedSlot || timeError ? "#d1d5db" : "linear-gradient(135deg, #f97316, #ea580c)",
                color: "#fff", fontWeight: 700, fontSize: 15, cursor: submitting || !selectedSlot || timeError ? "not-allowed" : "pointer",
              }}
            >
              {submitting ? "Đang xử lý..." : "Đặt lịch sạc"}
            </button>
          </form>
        </div>
      </div>
    </div>
  );
}

const labelStyle = { display: "block", fontSize: 13, fontWeight: 600, color: "#374151", marginBottom: 6 };
const inputStyle = { width: "100%", padding: "10px 14px", borderRadius: 10, border: "1.5px solid #e5e7eb", fontSize: 14, marginBottom: 16, outline: "none", boxSizing: "border-box" };
const btnBack = { marginTop: 16, padding: "10px 20px", borderRadius: 10, border: "none", background: "#f97316", color: "#fff", fontWeight: 600, cursor: "pointer" };
