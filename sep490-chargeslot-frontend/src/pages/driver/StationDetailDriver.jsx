import { useParams, useNavigate } from "react-router-dom";
import { useState, useEffect } from "react";
import { MapContainer, TileLayer, Marker } from "react-leaflet";
import L from "leaflet";
import "leaflet/dist/leaflet.css";
import { publicStationApi } from "@/services/api";

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
  Available: { label: "Trống", color: "#22c55e", bg: "#f0fdf4" },
  Active: { label: "Trống", color: "#22c55e", bg: "#f0fdf4" },
  Occupied: { label: "Đang dùng", color: "#ef4444", bg: "#fef2f2" },
  Maintenance: { label: "Bảo trì", color: "#f97316", bg: "#fff7ed" },
  Inactive: { label: "Ngưng", color: "#6b7280", bg: "#f3f4f6" },
};

export default function StationDetailDriver() {
  const { id } = useParams();
  const navigate = useNavigate();
  const [station, setStation] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    publicStationApi.getById(Number(id))
      .then((data) => {
        if (!cancelled) setStation(data);
      })
      .catch((err) => {
        if (!cancelled) setError(err.message);
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => { cancelled = true; };
  }, [id]);

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
  const availableSlots = (station.chargingSlots || []).filter(
    (s) => s.status === "Available" || s.status === "Active"
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
        <div className="grid grid-cols-3 gap-3 mb-6">
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
            <div className="text-lg font-bold text-amber-600">
              {(() => {
                const tiers = (station.pricingTiers || []).filter(t => t.isActive !== false);
                const prices = tiers.map(t => t.pricePerHour);
                if (prices.length === 0) return "Liên hệ";
                return Math.min(...prices).toLocaleString("vi-VN") + "đ";
              })()}
            </div>
            <div className="text-xs text-gray-500 mt-1">Giá từ/h</div>
          </div>
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
              const ss =
                slotStatusConfig[slot.status] || slotStatusConfig.Available;
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

        {/* Station images */}
        {station.images && station.images.length > 0 && (
          <div className="bg-white rounded-2xl shadow-sm p-5 mb-6">
            <h2 className="font-semibold text-gray-800 mb-3 flex items-center gap-2">
              <svg
                width="18"
                height="18"
                viewBox="0 0 24 24"
                fill="none"
                stroke="currentColor"
                strokeWidth="2"
              >
                <rect x="3" y="3" width="18" height="18" rx="2" ry="2" />
                <circle cx="8.5" cy="8.5" r="1.5" />
                <polyline points="21 15 16 10 5 21" />
              </svg>
              Hình ảnh
            </h2>
            <div className="grid grid-cols-2 gap-3">
              {station.images.map((img) => (
                <img
                  key={img.id}
                  src={img.imageUrl?.startsWith("http") ? img.imageUrl : `http://localhost:5162${img.imageUrl}`}
                  alt="Station"
                  className="w-full h-40 object-cover rounded-xl"
                />
              ))}
            </div>
          </div>
        )}

        {/* Book button */}
        <button
          onClick={() => navigate(`/driver/station/${station.id}/book`)}
          className="w-full py-4 text-white font-bold text-base rounded-2xl shadow-lg transition-all hover:shadow-xl hover:scale-[1.01] active:scale-[0.99] cursor-pointer flex items-center justify-center gap-2"
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
      </div>
    </div>
  );
}
