import { useParams, useNavigate } from "react-router-dom";
import { useState, useEffect, useRef } from "react";
import { MapContainer, TileLayer, Marker } from "react-leaflet";
import L from "leaflet";
import "leaflet/dist/leaflet.css";
import { publicStationApi, reviewApi, favoriteApi, chargingApi, stationApi } from "@/services/api";
import { useAuthStore } from "@/stores/authStore";

/* ─── Inject pulse animation (same as StationMap) ─── */
if (!document.getElementById("station-marker-pulse")) {
  const style = document.createElement("style");
  style.id = "station-marker-pulse";
  style.textContent = `
    @keyframes stationPulse {
      0%   { transform:scale(1);   opacity:.6; }
      50%  { transform:scale(1.8); opacity:0;  }
      100% { transform:scale(1);   opacity:0;  }
    }
  `;
  document.head.appendChild(style);
}

/* ─── Station marker (matching StationMap style) ─── */
const stationPin = new L.DivIcon({
  html: `
    <div style="position:relative;width:52px;height:64px;">
      <div style="
        position:absolute;top:6px;left:6px;
        width:40px;height:40px;border-radius:50%;
        background:rgba(34,197,94,.35);
        animation:stationPulse 2s ease-out infinite;
      "></div>
      <div style="
        position:absolute;top:0;left:2px;
        width:48px;height:48px;border-radius:50% 50% 50% 0;
        transform:rotate(-45deg);
        background:linear-gradient(135deg,#22c55e,#0f9d43);
        border:3px solid #fff;
        box-shadow:0 4px 14px rgba(0,0,0,.35);
      "></div>
      <div style="
        position:absolute;top:6px;left:8px;
        width:36px;height:36px;
        display:flex;align-items:center;justify-content:center;
      ">
        <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="#fff" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"><path d="M13 2L3 14h9l-1 8 10-12h-9l1-8z"/></svg>
      </div>
    </div>`,
  className: "",
  iconSize: [52, 64],
  iconAnchor: [26, 64],
});

const dayNames = [
  "Chủ nhật",
  "Thứ hai",
  "Thứ ba",
  "Thứ tư",
  "Thứ năm",
  "Thứ sáu",
  "Thứ bảy",
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

export default function StationDetailDriver() {
  const { id } = useParams();
  const navigate = useNavigate();
  const [station, setStation] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [loginToast, setLoginToast] = useState(false);
  const toastTimer = useRef(null);
  const { token } = useAuthStore();

  const [reviewSummary, setReviewSummary] = useState(null);
  const [reviews, setReviews] = useState([]);
  const [reviewPage, setReviewPage] = useState(1);
  const [hasMoreReviews, setHasMoreReviews] = useState(false);
  const [isFavorite, setIsFavorite] = useState(false);
  const [favLoading, setFavLoading] = useState(false);
  const [occupiedSlots, setOccupiedSlots] = useState(new Set());   // Đang sạc (InProgress/Charging)
  const [checkedInSlots, setCheckedInSlots] = useState(new Set()); // Đã check-in, chưa sạc
  const [unavailableDates, setUnavailableDates] = useState([]); // ["YYYY-MM-DD"]

  function handleBooking() {
    if (!token) {
      setLoginToast(true);
      clearTimeout(toastTimer.current);
      toastTimer.current = setTimeout(() => setLoginToast(false), 3500);
      return;
    }
    navigate(`/driver/station/${station.id}/book`);
  }

  useEffect(() => {
    if (!id) return;
    let cancelled = false;
    setLoading(true);

    (async () => {
      try {
        const data = await publicStationApi.getById(Number(id));
        if (cancelled) return;

        // BE limitation: /public/stations/{id} không include ExtraServices trong DB query
        // (ChargingStationRepository.GetByIdAsync thiếu .Include(s=>s.ExtraServices))
        // → trả về extraServices: [] rỗng.
        // Workaround: gọi thêm list endpoint (có include ExtraServices) và tìm theo ID.
        try {
          const listResult = await publicStationApi.getAll({ keyword: data.name, pageSize: 10 });
          const items = Array.isArray(listResult) ? listResult : (listResult?.items || []);
          const found = items.find(s => s.id === Number(id));
          if (found?.extraServices?.length > 0) {
            data.extraServices = found.extraServices;
          }
        } catch { /* Giữ extraServices rỗng nếu list fetch lỗi */ }

        setStation(data);

        // Fetch unavailable dates song song
        try {
          const udData = await stationApi.getUnavailableDates(Number(id));
          if (!cancelled && Array.isArray(udData)) {
            setUnavailableDates(udData.map(item => String(item?.date ?? item).substring(0, 10)));
          }
        } catch { /* không block UI */ }

      } catch (err) {
        if (!cancelled) setError(err.message);
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();

    return () => { cancelled = true; };
  }, [id]);

  // Fetch active sessions — phân biệt đang sạc (InProgress) và đã check-in (chưa sạc)
  useEffect(() => {
    if (!id) return;
    function parseSessions(response) {
      const occupied = new Set();
      const checkedIn = new Set();
      const sessions = Array.isArray(response) ? response : (response?.items || []);
      sessions
        .filter(s => !s.actualEndTime && s.bookingStatus !== "Completed")
        .forEach(session => {
          if (!session.slotId) return;
          const now = Date.now();
          let isTimeStarted = false;
          if (session.bookingStartTime || session.actualStartTime) {
            const timeStr = String(session.bookingStartTime || session.actualStartTime);
            const ms = new Date(timeStr.endsWith("Z") || /[+-]\d{2}:\d{2}$/.test(timeStr) ? timeStr : timeStr + "+07:00").getTime();
            if (!isNaN(ms) && now >= ms) isTimeStarted = true;
          }

          const st = session.status || session.bookingStatus || "";
          const isInProgressRaw = st === "InProgress" || st === "Charging" || session.bookingStatus === "InProgress" || session.bookingStatus === "Charging";
          const isCheckedInRaw = st === "CheckedIn" || session.bookingStatus === "CheckedIn";

          const isOccupied = isInProgressRaw || (isCheckedInRaw && isTimeStarted);
          const isCheckedIn = isCheckedInRaw && !isTimeStarted;
          
          if (isOccupied) {
            occupied.add(Number(session.slotId)); // Đang sạc thực sự
          } else if (isCheckedIn) {
            checkedIn.add(Number(session.slotId)); // Đã check-in, chưa bắt đầu sạc
          }
        });
      return { occupied, checkedIn };
    }

    chargingApi.getByStationId(Number(id))
      .then((response) => {
        const { occupied, checkedIn } = parseSessions(response);
        setOccupiedSlots(occupied);
        setCheckedInSlots(checkedIn);
      })
      .catch(() => { setOccupiedSlots(new Set()); setCheckedInSlots(new Set()); });

    const interval = setInterval(() => {
      chargingApi.getByStationId(Number(id))
        .then((response) => {
          const { occupied, checkedIn } = parseSessions(response);
          setOccupiedSlots(occupied);
          setCheckedInSlots(checkedIn);
        })
        .catch(() => {});
    }, 10000);
    return () => clearInterval(interval);
  }, [id]);

  // Fetch reviews
  useEffect(() => {
    if (!id) return;
    reviewApi.getSummary(Number(id)).then(setReviewSummary).catch(() => {});
    reviewApi.getByStation(Number(id), 1, 5).then((data) => {
      const list = Array.isArray(data) ? data : (data?.items || []);
      setReviews(list);
      setHasMoreReviews(list.length >= 5);
    }).catch(() => {});
  }, [id]);

  // Check favorite status
  useEffect(() => {
    if (!id || !token) return;
    favoriteApi.check(Number(id))
      .then(data => setIsFavorite(data?.isFavorite || false))
      .catch(() => {});
  }, [id, token]);

  async function toggleFavorite() {
    if (!token) {
      setLoginToast(true);
      clearTimeout(toastTimer.current);
      toastTimer.current = setTimeout(() => setLoginToast(false), 3500);
      return;
    }
    setFavLoading(true);
    try {
      if (isFavorite) {
        await favoriteApi.remove(Number(id));
        setIsFavorite(false);
      } else {
        await favoriteApi.add(Number(id));
        setIsFavorite(true);
      }
    } catch { /* ignore */ }
    setFavLoading(false);
  }

  async function loadMoreReviews() {
    const nextPage = reviewPage + 1;
    try {
      const data = await reviewApi.getByStation(Number(id), nextPage, 5);
      const list = Array.isArray(data) ? data : (data?.items || []);
      setReviews((prev) => [...prev, ...list]);
      setReviewPage(nextPage);
      setHasMoreReviews(list.length >= 5);
    } catch { /* ignore */ }
  }

  if (loading) {
    return (
      <div className="min-h-screen bg-[#f3f4f5] pt-24 px-4">
        <div className="max-w-3xl mx-auto text-center">
          <div className="text-5xl mb-4">⚡</div>
          <p className="text-gray-500">Đang tải thông tin trạm sạc...</p>
        </div>
      </div>
    );
  }

  if (error || !station) {
    return (
      <div className="min-h-screen bg-[#f3f4f5] pt-24 px-4">
        <div className="max-w-3xl mx-auto text-center">
          <div className="text-6xl mb-4">🔌</div>
          <h2 className="text-xl font-bold text-gray-800 mb-2">
            Không tìm thấy trạm sạc
          </h2>
          <p className="text-gray-500 mb-6">
            {error || "Trạm sạc không tồn tại hoặc đã bị xóa."}
          </p>
          <button
            onClick={() => navigate("/driver/map")}
            className="px-6 py-3 bg-orange-500 hover:bg-orange-600 text-white font-semibold rounded-xl transition-colors cursor-pointer"
          >
            ← Quay lại bản đồ
          </button>
        </div>
      </div>
    );
  }

  const st = statusConfig[station.operationalStatus] || statusConfig.Open;
  // Tính số slot có thể đặt được: loại trừ Occupied (active session), Maintenance, Inactive, Booked
  const availableSlots = (station.chargingSlots || []).filter(
    (s) => (s.status === "Available" || s.status === "Active") && !occupiedSlots.has(s.id)
  ).length;

  return (
    <div className="min-h-screen bg-[#f3f4f5]">
      {/* ── Hero section with mini map ── */}
      <div className="relative h-[280px] w-full">
        <MapContainer
          center={[station.latitude, station.longitude]}
          zoom={16}
          style={{ width: "100%", height: "100%" }}
          zoomControl={false}
          dragging={false}
          scrollWheelZoom={false}
          doubleClickZoom={false}
          touchZoom={false}
        >
          <TileLayer
            attribution='&copy; <a href="https://www.google.com/maps">Google Maps</a>'
            url="https://mt1.google.com/vt/lyrs=m&x={x}&y={y}&z={z}"
          />
          <Marker
            position={[station.latitude, station.longitude]}
            icon={stationPin}
          />
        </MapContainer>

        {/* Gradient overlay */}
        <div
          className="absolute bottom-0 left-0 right-0 h-24"
          style={{
            background:
              "linear-gradient(transparent, rgba(243,244,245,1))",
          }}
        />

        {/* Back button */}
        <button
          onClick={() => navigate("/driver/map")}
          className="absolute top-4 left-4 z-[1000] w-10 h-10 rounded-xl bg-white/90 backdrop-blur-sm shadow-lg flex items-center justify-center hover:bg-white transition-colors cursor-pointer"
        >
          <svg
            width="20"
            height="20"
            viewBox="0 0 24 24"
            fill="none"
            stroke="#374151"
            strokeWidth="2"
          >
            <path d="M19 12H5M12 19l-7-7 7-7" />
          </svg>
        </button>

        {/* Open in map button */}
        <button
          onClick={() => navigate("/driver/map")}
          className="absolute top-4 right-4 z-[1000] px-4 py-2 rounded-xl bg-white/90 backdrop-blur-sm shadow-lg text-sm font-medium text-gray-700 hover:bg-white transition-colors flex items-center gap-2 cursor-pointer"
        >
          <svg
            width="16"
            height="16"
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            strokeWidth="2"
          >
            <polygon points="1 6 1 22 8 18 16 22 23 18 23 2 16 6 8 2 1 6" />
            <line x1="8" y1="2" x2="8" y2="18" />
            <line x1="16" y1="6" x2="16" y2="22" />
          </svg>
          Mở bản đồ
        </button>
      </div>

      {/* ── Content ── */}
      <div className="max-w-3xl mx-auto px-4 -mt-8 pb-8 relative z-10">
        {/* Station name & status */}
        <div className="mb-6">
          <div className="flex items-center gap-3 mb-2">
            <h1 className="text-2xl font-bold text-gray-900">
              {station.name}
            </h1>
            <span
              className="text-xs font-semibold px-3 py-1 rounded-full"
              style={{ color: st.color, background: st.bg }}
            >
              {st.label}
            </span>
            <button
              onClick={toggleFavorite}
              disabled={favLoading}
              className="ml-auto p-2 rounded-full hover:bg-red-50 transition-colors cursor-pointer"
              title={isFavorite ? "Bỏ yêu thích" : "Thêm yêu thích"}
            >
              <svg width="22" height="22" viewBox="0 0 24 24" fill={isFavorite ? "#ef4444" : "none"} stroke={isFavorite ? "#ef4444" : "#9ca3af"} strokeWidth="2">
                <path d="M20.84 4.61a5.5 5.5 0 0 0-7.78 0L12 5.67l-1.06-1.06a5.5 5.5 0 0 0-7.78 7.78l1.06 1.06L12 21.23l7.78-7.78 1.06-1.06a5.5 5.5 0 0 0 0-7.78z" />
              </svg>
            </button>
          </div>
          <p className="text-gray-500 flex items-center gap-1">
            <svg
              width="16"
              height="16"
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              strokeWidth="2"
            >
              <path d="M21 10c0 7-9 13-9 13s-9-6-9-13a9 9 0 0 1 18 0z" />
              <circle cx="12" cy="10" r="3" />
            </svg>
            {station.address}
          </p>
        </div>

        {/* Quick stats */}
        <div className={`grid ${reviewSummary ? 'grid-cols-4' : 'grid-cols-3'} gap-3 mb-6`}>
          <div className="bg-white rounded-2xl shadow-sm p-4 text-center">
            <div className="text-2xl font-bold text-green-500">
              {availableSlots}
            </div>
            <div className="text-xs text-gray-500 mt-1">Slot trống</div>
          </div>
          <div className="bg-white rounded-2xl shadow-sm p-4 text-center">
            <div className="text-2xl font-bold text-blue-500">
              {station.chargingSlots.length}
            </div>
            <div className="text-xs text-gray-500 mt-1">Tổng slot</div>
          </div>
          <div className="bg-white rounded-2xl shadow-sm p-4 text-center">
            <div className="text-2xl font-bold text-amber-600">
              {(() => {
                const tiers = (station.pricingTiers || []).filter(t => t.isActive !== false);
                const prices = tiers.map(t => t.pricePerHour);
                if (prices.length === 0) return "Liên hệ";
                return Math.min(...prices).toLocaleString("vi-VN") + "đ";
              })()}
            </div>
            <div className="text-xs text-gray-500 mt-1">Giá từ/h</div>
          </div>
          {reviewSummary && (
            <div className="bg-white rounded-2xl shadow-sm p-4 text-center">
              <div className="flex items-center justify-center gap-1">
                <span className="text-2xl font-bold text-amber-500">{reviewSummary.averageRating?.toFixed(1)}</span>
                <span className="text-amber-400 text-lg">⭐</span>
              </div>
              <div className="text-xs text-gray-500 mt-1">{reviewSummary.totalReviews} đánh giá</div>
            </div>
          )}
        </div>

        {/* Description */}
        {station.description && (
          <div className="bg-white rounded-2xl shadow-sm p-5 mb-4">
            <h2 className="font-semibold text-gray-800 mb-2 flex items-center gap-2">
              <svg
                width="18"
                height="18"
                viewBox="0 0 24 24"
                fill="none"
                stroke="currentColor"
                strokeWidth="2"
              >
                <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z" />
                <polyline points="14 2 14 8 20 8" />
                <line x1="16" y1="13" x2="8" y2="13" />
                <line x1="16" y1="17" x2="8" y2="17" />
              </svg>
              Mô tả
            </h2>
            <p className="text-gray-600 text-sm leading-relaxed">
              {station.description}
            </p>
          </div>
        )}

        {/* ExtraServices */}
        {station.extraServices && station.extraServices.filter(es => es.isActive).length > 0 && (
          <div className="bg-white rounded-2xl shadow-sm p-5 mb-4">
            <h2 className="font-semibold text-gray-800 mb-3 flex items-center gap-2">
              <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                <path d="M20 7h-9" /><path d="M14 17H5" />
                <circle cx="17" cy="17" r="3" /><circle cx="7" cy="7" r="3" />
              </svg>
              Dịch vụ bổ sung
            </h2>
            <div className="space-y-2">
              {station.extraServices.filter(es => es.isActive).map((es, idx) => (
                <div key={idx} className="flex items-center justify-between p-3 rounded-xl bg-purple-50 border border-purple-100">
                  <div className="flex items-center gap-3">
                    <div className="w-9 h-9 rounded-lg bg-purple-100 flex items-center justify-center">
                      <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="#7c3aed" strokeWidth="2">
                        <path d="M12 2v20M2 12h20" />
                      </svg>
                    </div>
                    <div>
                      <div className="text-sm font-semibold text-gray-800">{es.serviceName}</div>
                      {es.description && <div className="text-xs text-gray-500">{es.description}</div>}
                    </div>
                  </div>
                  <div className="text-right">
                    <span className={`text-sm font-bold ${es.price > 0 ? "text-purple-600" : "text-green-600"}`}>
                      {es.price > 0 ? `${es.price.toLocaleString("vi-VN")}đ` : "Miễn phí"}
                    </span>
                    {es.totalStock != null && (
                      <div className="text-[10px] text-gray-400 mt-0.5">Còn {es.totalStock} sản phẩm</div>
                    )}
                  </div>
                </div>
              ))}
            </div>
          </div>
        )}

        {/* Charging slots */}
        <div className="bg-white rounded-2xl shadow-sm p-5 mb-4">
          <h2 className="font-semibold text-gray-800 mb-3 flex items-center gap-2">
            <svg
              width="18"
              height="18"
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              strokeWidth="2"
            >
              <path d="M13 2L3 14h9l-1 8 10-12h-9l1-8z" />
            </svg>
            Các slot sạc ({station.chargingSlots.length})
          </h2>

          <div className="space-y-3">
            {station.chargingSlots.map((slot) => {
              // Ưu tiên: Đang sạc > Đã check-in > trạng thái gốc từ BE
              const isOccupied  = occupiedSlots.has(slot.id);
              const isCheckedIn = checkedInSlots.has(slot.id);
              const displayStatus = isOccupied ? "Occupied" : isCheckedIn ? "CheckedIn" : slot.status;
              const ss = slotStatusConfig[displayStatus] || slotStatusConfig[slot.status] || slotStatusConfig.Available;
              return (
                <div
                  key={slot.id}
                  className="flex items-center justify-between p-3 rounded-xl border border-gray-100 hover:border-gray-200 transition-colors"
                  style={{ background: `${ss.bg}` }}
                >
                  <div className="flex items-center gap-3">
                    <div
                      className="w-10 h-10 rounded-lg flex items-center justify-center"
                      style={{ background: `${ss.color}20` }}
                    >
                      <svg
                        width="18"
                        height="18"
                        viewBox="0 0 24 24"
                        fill="none"
                        stroke={ss.color}
                        strokeWidth="2.5"
                      >
                        <path d="M13 2L3 14h9l-1 8 10-12h-9l1-8z" />
                      </svg>
                    </div>
                    <div>
                      <div className="font-semibold text-gray-900 text-sm">
                        {slot.slotName}
                      </div>
                      <div className="text-xs text-gray-500">
                        Vị trí: {slot.positionY && slot.positionX ? `${String.fromCharCode(64 + Number(slot.positionY))}${slot.positionX}` : "—"}
                      </div>
                    </div>
                  </div>
                  <div className="text-right">
                    <span
                      className="text-xs font-medium px-2 py-0.5 rounded-full"
                      style={{ color: ss.color, background: `${ss.color}15` }}
                    >
                      {ss.label}
                    </span>
                  </div>
                </div>
              );
            })}
          </div>

          {/* Station-level pricing tiers */}
          {(() => {
            const tiers = (station.pricingTiers || []).filter(t => t.isActive !== false);
            if (tiers.length === 0) return null;
            return (
              <div className="mt-4 pt-4 border-t border-gray-100">
                <h3 className="text-sm font-semibold text-gray-700 mb-2">⏰ Giá theo khung giờ</h3>
                <div className="space-y-1">
                  {tiers.map((tier, idx) => (
                    <div key={idx} className="flex justify-between text-xs bg-gray-50 rounded-lg px-3 py-2">
                      <span className="text-gray-500">{String(tier.startTime).substring(0,5)}–{String(tier.endTime).substring(0,5)}</span>
                      <span className="font-bold text-amber-600">{tier.pricePerHour?.toLocaleString("vi-VN")}đ/h</span>
                    </div>
                  ))}
                </div>
              </div>
            );
          })()}
        </div>

        {/* Operating hours */}
        <div className="bg-white rounded-2xl shadow-sm p-5 mb-4">
          <h2 className="font-semibold text-gray-800 mb-3 flex items-center gap-2">
            <svg
              width="18"
              height="18"
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              strokeWidth="2"
            >
              <circle cx="12" cy="12" r="10" />
              <polyline points="12 6 12 12 16 14" />
            </svg>
            Giờ hoạt động
          </h2>

          <div className="space-y-2">
            {[0, 1, 2, 3, 4, 5, 6].map((day) => {
              const hours = station.operatingHours?.find(
                (h) => h.dayOfWeek === day
              );
              const isToday = new Date().getDay() === day;
              return (
                <div
                  key={day}
                  className={`flex justify-between text-sm px-3 py-2 rounded-lg ${
                    isToday
                      ? "bg-orange-50 border border-orange-100"
                      : ""
                  }`}
                >
                  <span
                    className={`font-medium ${
                      isToday ? "text-orange-600" : "text-gray-700"
                    }`}
                  >
                    {dayNames[day]}
                    {isToday && (
                      <span className="ml-2 text-xs bg-orange-500 text-white px-2 py-0.5 rounded-full">
                        Hôm nay
                      </span>
                    )}
                  </span>
                  <span
                    className={
                      hours && !hours.isClosed
                        ? "text-gray-900 font-medium"
                        : "text-red-400"
                    }
                  >
                    {hours && !hours.isClosed
                      ? `${hours.openTime} - ${hours.closeTime}`
                      : "Đóng cửa"}
                  </span>
                </div>
              );
            })}
          </div>
        </div>

        {/* Unavailable dates */}
        {unavailableDates.length > 0 && (() => {
          const todayStr = new Date().toISOString().split("T")[0];
          const isTodayBlocked = unavailableDates.includes(todayStr);
          const upcoming = unavailableDates
            .filter(d => d >= todayStr)
            .sort()
            .slice(0, 5);
          const fmtDate = (s) => { const [y,m,d] = s.split("-"); return `${d}/${m}/${y}`; };
          return (
            <div className="bg-red-50 border border-red-200 rounded-2xl p-5 mb-4">
              <h2 className="font-semibold text-red-700 mb-2 flex items-center gap-2">
                <span>🚫</span> Ngày không hoạt động
              </h2>
              {isTodayBlocked && (
                <div className="bg-red-100 border border-red-300 rounded-xl px-4 py-2.5 mb-3 text-sm font-bold text-red-700">
                  ⚠️ Hôm nay ({fmtDate(todayStr)}) trạm không hoạt động — không thể đặt lịch!
                </div>
              )}
              <div className="flex flex-wrap gap-2">
                {upcoming.map(d => (
                  <span key={d} className={`text-xs font-semibold px-3 py-1 rounded-full ${
                    d === todayStr
                      ? "bg-red-500 text-white"
                      : "bg-red-100 text-red-700"
                  }`}>
                    📅 {fmtDate(d)}
                  </span>
                ))}
              </div>
            </div>
          );
        })()}
        {/* Station images */}
        {(() => {
          const BASE_URL = "https://chargeslot-api-f8b5brexe2b0ekhp.japaneast-01.azurewebsites.net";
          // Hỗ trợ nhiều property name: images, stationImages
          const rawImages = station.images || station.stationImages || [];
          if (!rawImages || rawImages.length === 0) return null;

          const buildUrl = (raw) => {
            if (!raw) return null;
            // Nếu đã là URL đầy đủ
            if (raw.startsWith("http://") || raw.startsWith("https://")) return raw;
            // Tương đối → ghép base
            return `${BASE_URL}${raw.startsWith("/") ? "" : "/"}${raw}`;
          };

          const images = rawImages.map((img, idx) => {
            // item có thể là object { id, imageUrl, url } hoặc string
            if (typeof img === "string") {
              return { key: idx, src: buildUrl(img) };
            }
            const rawUrl = img.imageUrl || img.url || img.imagePath || img.path || "";
            return { key: img.id ?? idx, src: buildUrl(rawUrl) };
          }).filter(i => i.src);

          if (images.length === 0) return null;

          return (
            <div className="bg-white rounded-2xl shadow-sm p-5 mb-6">
              <h2 className="font-semibold text-gray-800 mb-3 flex items-center gap-2">
                <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                  <rect x="3" y="3" width="18" height="18" rx="2" ry="2" />
                  <circle cx="8.5" cy="8.5" r="1.5" />
                  <polyline points="21 15 16 10 5 21" />
                </svg>
                Hình ảnh
              </h2>
              <div className="grid grid-cols-2 gap-3">
                {images.map((img) => (
                  <img
                    key={img.key}
                    src={img.src}
                    alt="Hình ảnh trạm sạc"
                    className="w-full h-40 object-cover rounded-xl"
                    onError={(e) => { e.target.style.display = "none"; }}
                  />
                ))}
              </div>
            </div>
          );
        })()}

        {/* Reviews section */}
        <div className="bg-white rounded-2xl shadow-sm p-5 mb-4">
          <h2 className="font-semibold text-gray-800 mb-3 flex items-center gap-2">
            ⭐ Đánh giá {reviewSummary ? `(${reviewSummary.averageRating?.toFixed(1)}/5 — ${reviewSummary.totalReviews} lượt)` : ""}
          </h2>

          {/* Star breakdown */}
          {reviewSummary && reviewSummary.totalReviews > 0 && (
            <div className="mb-4 space-y-1">
              {[5, 4, 3, 2, 1].map((star) => {
                const count = reviewSummary[`star${star}`] || 0;
                const pct = reviewSummary.totalReviews > 0 ? (count / reviewSummary.totalReviews) * 100 : 0;
                return (
                  <div key={star} className="flex items-center gap-2 text-xs">
                    <span className="w-6 text-right text-gray-500 font-medium">{star}⭐</span>
                    <div className="flex-1 h-2 bg-gray-100 rounded-full overflow-hidden">
                      <div className="h-full bg-amber-400 rounded-full transition-all" style={{ width: `${pct}%` }} />
                    </div>
                    <span className="w-8 text-gray-400">{count}</span>
                  </div>
                );
              })}
            </div>
          )}

          {/* Review list */}
          {reviews.length > 0 ? (
            <div className="space-y-3">
              {reviews.map((r) => (
                <div key={r.id} className="border-b border-gray-50 pb-3 last:border-0">
                  <div className="flex items-center gap-2 mb-1">
                    {r.driverAvatarUrl ? (
                      <img src={r.driverAvatarUrl.startsWith("http") ? r.driverAvatarUrl : `https://chargeslot-api-f8b5brexe2b0ekhp.japaneast-01.azurewebsites.net${r.driverAvatarUrl.startsWith("/") ? "" : "/"}${r.driverAvatarUrl}`} alt="" className="w-7 h-7 rounded-full object-cover" />
                    ) : (
                      <div className="w-7 h-7 rounded-full bg-gray-200 flex items-center justify-center text-xs">👤</div>
                    )}
                    <span className="text-sm font-semibold text-gray-800">{r.driverName || "Driver"}</span>
                    <span className="text-xs text-amber-500">{"⭐".repeat(r.rating)}</span>
                    <span className="text-xs text-gray-400 ml-auto">{new Date(r.createdAt).toLocaleDateString("vi-VN")}</span>
                  </div>
                  {r.comment && <p className="text-sm text-gray-600 ml-9">{r.comment}</p>}
                  {r.ownerReply && (
                    <div className="ml-9 mt-2 pl-3 border-l-2 border-orange-200 bg-orange-50 rounded-r-lg py-2 px-3">
                      <span className="text-xs font-semibold text-orange-600">Phản hồi Owner:</span>
                      <p className="text-xs text-gray-600 mt-0.5">{r.ownerReply}</p>
                    </div>
                  )}
                </div>
              ))}
              {hasMoreReviews && (
                <button
                  onClick={loadMoreReviews}
                  className="w-full text-sm text-orange-500 font-semibold py-2 hover:text-orange-700 cursor-pointer"
                >
                  Xem thêm đánh giá →
                </button>
              )}
            </div>
          ) : (
            <p className="text-sm text-gray-400 italic text-center py-4">Chưa có đánh giá nào.</p>
          )}
        </div>

        {/* Book button */}
        <button
          onClick={handleBooking}
          disabled={unavailableDates.includes(new Date().toISOString().split("T")[0])}
          className="w-full py-4 text-white font-bold text-base rounded-2xl shadow-lg transition-all hover:shadow-xl hover:scale-[1.01] active:scale-[0.99] cursor-pointer flex items-center justify-center gap-2 disabled:opacity-60 disabled:cursor-not-allowed disabled:hover:scale-100 disabled:hover:shadow-lg"
          style={{
            background: "linear-gradient(135deg, #f97316, #ea580c)",
          }}
        >
          <svg
            width="20"
            height="20"
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            strokeWidth="2.5"
          >
            <rect x="3" y="4" width="18" height="18" rx="2" ry="2" />
            <line x1="16" y1="2" x2="16" y2="6" />
            <line x1="8" y1="2" x2="8" y2="6" />
            <line x1="3" y1="10" x2="21" y2="10" />
          </svg>
          Đặt lịch sạc
        </button>

        {/* Login toast */}
        <div
          className={`fixed top-24 left-1/2 -translate-x-1/2 z-50 transition-all duration-500 ${loginToast
            ? "opacity-100 translate-y-0"
            : "opacity-0 -translate-y-4 pointer-events-none"
          }`}
        >
          <div className="flex items-center gap-3 bg-white border border-orange-200 shadow-2xl rounded-xl px-5 py-4 min-w-[360px]">
            <div className="w-10 h-10 rounded-full bg-orange-100 flex items-center justify-center flex-shrink-0">
              <svg className="w-5 h-5 text-orange-500" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-2.5L13.732 4c-.77-.833-1.964-.833-2.732 0L4.082 16.5c-.77.833.192 2.5 1.732 2.5z" />
              </svg>
            </div>
            <div>
              <p className="text-sm font-semibold text-gray-800">Yêu cầu đăng nhập</p>
              <p className="text-xs text-gray-500 mt-0.5">Bạn phải đăng nhập vào hệ thống mới có thể đặt lịch sạc</p>
            </div>
            <button onClick={() => setLoginToast(false)} className="ml-auto text-gray-400 hover:text-gray-600 cursor-pointer">
              <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
              </svg>
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}
