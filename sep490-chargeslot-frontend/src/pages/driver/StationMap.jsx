import { useEffect, useState, useMemo, useRef } from "react";
import { useNavigate } from "react-router-dom";
import { MapContainer, TileLayer, Marker, Popup, useMap } from "react-leaflet";
import L from "leaflet";
import "leaflet/dist/leaflet.css";
import { mockStations } from "@/data/mockStations";

/* ─── Fix leaflet default icon in bundlers ─── */
delete L.Icon.Default.prototype._getIconUrl;
L.Icon.Default.mergeOptions({
  iconRetinaUrl:
    "https://cdnjs.cloudflare.com/ajax/libs/leaflet/1.9.4/images/marker-icon-2x.png",
  iconUrl:
    "https://cdnjs.cloudflare.com/ajax/libs/leaflet/1.9.4/images/marker-icon.png",
  shadowUrl:
    "https://cdnjs.cloudflare.com/ajax/libs/leaflet/1.9.4/images/marker-shadow.png",
});

/* ─── Inject pulse animation ─── */
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

/* ─── Custom Station marker icon (pin shape) ─── */
const stationIcon = new L.DivIcon({
  html: `
    <div style="position:relative;width:52px;height:64px;">
      <!-- pulse ring -->
      <div style="
        position:absolute;top:6px;left:6px;
        width:40px;height:40px;border-radius:50%;
        background:rgba(34,197,94,.35);
        animation:stationPulse 2s ease-out infinite;
      "></div>
      <!-- pin body -->
      <div style="
        position:absolute;top:0;left:2px;
        width:48px;height:48px;border-radius:50% 50% 50% 0;
        transform:rotate(-45deg);
        background:linear-gradient(135deg,#22c55e,#0f9d43);
        border:3px solid #fff;
        box-shadow:0 4px 14px rgba(0,0,0,.35);
      "></div>
      <!-- icon -->
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
  popupAnchor: [0, -56],
});

const maintenanceIcon = new L.DivIcon({
  html: `
    <div style="position:relative;width:52px;height:64px;">
      <!-- pulse ring -->
      <div style="
        position:absolute;top:6px;left:6px;
        width:40px;height:40px;border-radius:50%;
        background:rgba(249,115,22,.35);
        animation:stationPulse 2s ease-out infinite;
      "></div>
      <!-- pin body -->
      <div style="
        position:absolute;top:0;left:2px;
        width:48px;height:48px;border-radius:50% 50% 50% 0;
        transform:rotate(-45deg);
        background:linear-gradient(135deg,#f97316,#c2410c);
        border:3px solid #fff;
        box-shadow:0 4px 14px rgba(0,0,0,.35);
      "></div>
      <!-- icon -->
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
  popupAnchor: [0, -56],
});

/* ─── User location marker ─── */
const userIcon = new L.DivIcon({
  html: `<div style="
    width:20px;height:20px;border-radius:50%;
    background:#3b82f6;border:3px solid #fff;
    box-shadow:0 0 0 6px rgba(59,130,246,.25),0 2px 8px rgba(0,0,0,.3);
  "></div>`,
  className: "",
  iconSize: [20, 20],
  iconAnchor: [10, 10],
});

/* ─── Component to fly map to position ─── */
function FlyTo({ center, zoom }) {
  const map = useMap();
  useEffect(() => {
    if (center) map.flyTo(center, zoom || 14, { duration: 1.2 });
  }, [center, zoom, map]);
  return null;
}

/* ─── Status helpers ─── */
const statusConfig = {
  Open: { label: "Đang mở", color: "#22c55e", bg: "#f0fdf4" },
  Closed: { label: "Đã đóng", color: "#ef4444", bg: "#fef2f2" },
  Maintenance: { label: "Bảo trì", color: "#f97316", bg: "#fff7ed" },
};

const slotStatusConfig = {
  Available: { label: "Trống", color: "#22c55e" },
  Occupied: { label: "Đang dùng", color: "#ef4444" },
  Maintenance: { label: "Bảo trì", color: "#f97316" },
};

export default function StationMap() {
  const navigate = useNavigate();
  const [userPos, setUserPos] = useState(null);
  const [search, setSearch] = useState("");
  const [flyTarget, setFlyTarget] = useState(null);
  const [selectedStation, setSelectedStation] = useState(null);
  const [showList, setShowList] = useState(false);
  const markerRefs = useRef({});

  // Hanoi center
  const defaultCenter = [21.0285, 105.8542];

  useEffect(() => {
    navigator.geolocation?.getCurrentPosition(
      (pos) => setUserPos([pos.coords.latitude, pos.coords.longitude]),
      () => {},
      { enableHighAccuracy: true, timeout: 8000 }
    );
  }, []);

  const filtered = useMemo(() => {
    if (!search.trim()) return mockStations;
    const q = search.toLowerCase();
    return mockStations.filter(
      (s) =>
        s.name.toLowerCase().includes(q) ||
        s.address.toLowerCase().includes(q)
    );
  }, [search]);

  function getAvailableSlots(station) {
    return station.chargingSlots.filter((s) => s.status === "Available").length;
  }

  function handleListItemClick(station) {
    setFlyTarget([station.latitude, station.longitude]);
    setSelectedStation(station.id);
    setShowList(false);
    // Open the marker popup
    setTimeout(() => {
      markerRefs.current[station.id]?.openPopup();
    }, 1300);
  }

  return (
    <div
      style={{
        position: "relative",
        width: "100%",
        height: "calc(100vh - 80px)",
        marginTop: 80,
      }}
    >
      {/* ── Search & controls overlay ── */}
      <div
        style={{
          position: "absolute",
          top: 12,
          left: "50%",
          transform: "translateX(-50%)",
          zIndex: 1000,
          width: "min(460px, calc(100% - 32px))",
        }}
      >
        {/* Search bar */}
        <div
          style={{
            display: "flex",
            alignItems: "center",
            background: "rgba(255,255,255,0.97)",
            backdropFilter: "blur(16px)",
            borderRadius: 16,
            boxShadow: "0 4px 24px rgba(0,0,0,0.12)",
            padding: "6px 6px 6px 16px",
            gap: 8,
          }}
        >
          <input
            type="text"
            placeholder="Tìm trạm sạc..."
            value={search}
            onChange={(e) => {
              setSearch(e.target.value);
              setShowList(true);
            }}
            onFocus={() => setShowList(true)}
            style={{
              flex: 1,
              border: "none",
              outline: "none",
              fontSize: 15,
              background: "transparent",
              color: "#1f2937",
              padding: "8px 0",
            }}
          />

          {search && (
            <button
              onClick={() => {
                setSearch("");
                setShowList(false);
              }}
              style={{
                background: "none",
                border: "none",
                cursor: "pointer",
                color: "#9ca3af",
                padding: 4,
              }}
            >
              <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                <path d="M18 6L6 18M6 6l12 12" />
              </svg>
            </button>
          )}

          <button
            onClick={() => setShowList((v) => !v)}
            style={{
              background: "linear-gradient(135deg, #f97316, #ea580c)",
              border: "none",
              borderRadius: 12,
              padding: "10px 14px",
              cursor: "pointer",
              display: "flex",
              alignItems: "center",
              gap: 6,
              color: "#fff",
              fontWeight: 600,
              fontSize: 13,
            }}
          >
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5">
              <circle cx="11" cy="11" r="8" />
              <path d="m21 21-4.3-4.3" />
            </svg>
          </button>
        </div>

        {/* Search results / Station list */}
        {showList && (
          <div
            style={{
              marginTop: 8,
              background: "rgba(255,255,255,0.97)",
              backdropFilter: "blur(16px)",
              borderRadius: 16,
              boxShadow: "0 4px 24px rgba(0,0,0,0.12)",
              maxHeight: 360,
              overflowY: "auto",
            }}
          >
            {filtered.length === 0 ? (
              <div
                style={{
                  padding: "24px 16px",
                  textAlign: "center",
                  color: "#9ca3af",
                  fontSize: 14,
                }}
              >
                Không tìm thấy trạm sạc nào
              </div>
            ) : (
              filtered.map((s) => {
                const available = getAvailableSlots(s);
                const st = statusConfig[s.operationalStatus] || statusConfig.Open;
                return (
                  <button
                    key={s.id}
                    onClick={() => handleListItemClick(s)}
                    style={{
                      width: "100%",
                      display: "flex",
                      alignItems: "flex-start",
                      gap: 12,
                      padding: "14px 16px",
                      background:
                        selectedStation === s.id
                          ? "rgba(249,115,22,0.06)"
                          : "transparent",
                      border: "none",
                      borderBottom: "1px solid #f3f4f6",
                      cursor: "pointer",
                      textAlign: "left",
                      transition: "background .15s",
                    }}
                    onMouseEnter={(e) =>
                      (e.currentTarget.style.background = "rgba(249,115,22,0.06)")
                    }
                    onMouseLeave={(e) =>
                      (e.currentTarget.style.background =
                        selectedStation === s.id
                          ? "rgba(249,115,22,0.06)"
                          : "transparent")
                    }
                  >
                    {/* Icon */}
                    <div
                      style={{
                        width: 40,
                        height: 40,
                        borderRadius: 10,
                        background: "linear-gradient(135deg, #22c55e, #16a34a)",
                        display: "flex",
                        alignItems: "center",
                        justifyContent: "center",
                        flexShrink: 0,
                        marginTop: 2,
                      }}
                    >
                      <svg
                        width="20"
                        height="20"
                        viewBox="0 0 24 24"
                        fill="none"
                        stroke="#fff"
                        strokeWidth="2.5"
                      >
                        <path d="M13 2L3 14h9l-1 8 10-12h-9l1-8z" />
                      </svg>
                    </div>

                    <div style={{ flex: 1, minWidth: 0 }}>
                      <div
                        style={{
                          display: "flex",
                          alignItems: "center",
                          gap: 8,
                          marginBottom: 2,
                        }}
                      >
                        <span
                          style={{
                            fontWeight: 600,
                            fontSize: 14,
                            color: "#1f2937",
                          }}
                        >
                          {s.name}
                        </span>
                        <span
                          style={{
                            fontSize: 11,
                            fontWeight: 600,
                            color: st.color,
                            background: st.bg,
                            padding: "2px 8px",
                            borderRadius: 20,
                          }}
                        >
                          {st.label}
                        </span>
                      </div>
                      <div
                        style={{
                          fontSize: 12,
                          color: "#6b7280",
                          overflow: "hidden",
                          textOverflow: "ellipsis",
                          whiteSpace: "nowrap",
                        }}
                      >
                        📍 {s.address}
                      </div>
                      <div
                        style={{
                          fontSize: 12,
                          color: "#6b7280",
                          marginTop: 2,
                        }}
                      >
                        ⚡ {available}/{s.chargingSlots.length} slot trống
                      </div>
                    </div>
                  </button>
                );
              })
            )}
          </div>
        )}
      </div>

      {/* ── Locate me button ── */}
      <button
        onClick={() => {
          if (userPos) setFlyTarget(userPos);
          else
            navigator.geolocation?.getCurrentPosition(
              (pos) => {
                const p = [pos.coords.latitude, pos.coords.longitude];
                setUserPos(p);
                setFlyTarget(p);
              },
              () => alert("Không thể lấy vị trí của bạn"),
              { enableHighAccuracy: true, timeout: 8000 }
            );
        }}
        style={{
          position: "absolute",
          bottom: 24,
          right: 20,
          zIndex: 1000,
          width: 48,
          height: 48,
          borderRadius: "50%",
          background: "rgba(255,255,255,0.97)",
          backdropFilter: "blur(8px)",
          border: "none",
          boxShadow: "0 2px 12px rgba(0,0,0,0.15)",
          cursor: "pointer",
          display: "flex",
          alignItems: "center",
          justifyContent: "center",
          color: "#3b82f6",
        }}
        title="Vị trí của tôi"
      >
        <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
          <circle cx="12" cy="12" r="3" />
          <path d="M12 2v4M12 18v4M2 12h4M18 12h4" />
        </svg>
      </button>

      {/* ── Map ── */}
      <MapContainer
        center={userPos || defaultCenter}
        zoom={13}
        style={{ width: "100%", height: "100%", zIndex: 1 }}
        zoomControl={false}
      >
        <TileLayer
          attribution='&copy; <a href="https://www.google.com/maps">Google Maps</a>'
          url="https://mt1.google.com/vt/lyrs=m&x={x}&y={y}&z={z}"
        />

        {flyTarget && <FlyTo center={flyTarget} zoom={15} />}

        {/* User location */}
        {userPos && <Marker position={userPos} icon={userIcon} />}

        {/* Station markers */}
        {filtered.map((station) => {
          const available = getAvailableSlots(station);
          const st =
            statusConfig[station.operationalStatus] || statusConfig.Open;
          const icon =
            station.operationalStatus === "Maintenance"
              ? maintenanceIcon
              : stationIcon;

          return (
            <Marker
              key={station.id}
              position={[station.latitude, station.longitude]}
              icon={icon}
              ref={(ref) => {
                if (ref) markerRefs.current[station.id] = ref;
              }}
            >
              <Popup maxWidth={320} minWidth={280}>
                <div style={{ fontFamily: "Inter, system-ui, sans-serif" }}>
                  {/* Header */}
                  <div
                    style={{
                      display: "flex",
                      alignItems: "center",
                      gap: 8,
                      marginBottom: 8,
                    }}
                  >
                    <div
                      style={{
                        width: 36,
                        height: 36,
                        borderRadius: 8,
                        background:
                          "linear-gradient(135deg, #22c55e, #16a34a)",
                        display: "flex",
                        alignItems: "center",
                        justifyContent: "center",
                        flexShrink: 0,
                      }}
                    >
                      <svg
                        width="18"
                        height="18"
                        viewBox="0 0 24 24"
                        fill="none"
                        stroke="#fff"
                        strokeWidth="2.5"
                      >
                        <path d="M13 2L3 14h9l-1 8 10-12h-9l1-8z" />
                      </svg>
                    </div>
                    <div style={{ flex: 1 }}>
                      <div
                        style={{
                          fontWeight: 700,
                          fontSize: 15,
                          color: "#1f2937",
                          lineHeight: 1.3,
                        }}
                      >
                        {station.name}
                      </div>
                      <span
                        style={{
                          fontSize: 11,
                          fontWeight: 600,
                          color: st.color,
                          background: st.bg,
                          padding: "2px 8px",
                          borderRadius: 20,
                        }}
                      >
                        {st.label}
                      </span>
                    </div>
                  </div>

                  {/* Address */}
                  <div
                    style={{
                      fontSize: 13,
                      color: "#6b7280",
                      marginBottom: 10,
                      lineHeight: 1.4,
                    }}
                  >
                    📍 {station.address}
                  </div>

                  {/* Stats */}
                  <div
                    style={{
                      display: "flex",
                      gap: 8,
                      marginBottom: 12,
                    }}
                  >
                    <div
                      style={{
                        flex: 1,
                        background: "#f0fdf4",
                        borderRadius: 10,
                        padding: "8px 10px",
                        textAlign: "center",
                      }}
                    >
                      <div
                        style={{
                          fontSize: 18,
                          fontWeight: 700,
                          color: "#22c55e",
                        }}
                      >
                        {available}
                      </div>
                      <div
                        style={{
                          fontSize: 11,
                          color: "#6b7280",
                          marginTop: 2,
                        }}
                      >
                        Slot trống
                      </div>
                    </div>
                    <div
                      style={{
                        flex: 1,
                        background: "#eff6ff",
                        borderRadius: 10,
                        padding: "8px 10px",
                        textAlign: "center",
                      }}
                    >
                      <div
                        style={{
                          fontSize: 18,
                          fontWeight: 700,
                          color: "#3b82f6",
                        }}
                      >
                        {station.chargingSlots.length}
                      </div>
                      <div
                        style={{
                          fontSize: 11,
                          color: "#6b7280",
                          marginTop: 2,
                        }}
                      >
                        Tổng slot
                      </div>
                    </div>
                    <div
                      style={{
                        flex: 1,
                        background: "#fef3c7",
                        borderRadius: 10,
                        padding: "8px 10px",
                        textAlign: "center",
                      }}
                    >
                      <div
                        style={{
                          fontSize: 14,
                          fontWeight: 700,
                          color: "#d97706",
                        }}
                      >
                        {Math.min(
                          ...station.chargingSlots.map(
                            (s) => s.pricePerHour
                          )
                        ).toLocaleString("vi-VN")}đ
                      </div>
                      <div
                        style={{
                          fontSize: 11,
                          color: "#6b7280",
                          marginTop: 2,
                        }}
                      >
                        Giá từ/h
                      </div>
                    </div>
                  </div>

                  {/* Slots preview */}
                  <div
                    style={{
                      display: "flex",
                      flexWrap: "wrap",
                      gap: 4,
                      marginBottom: 12,
                    }}
                  >
                    {station.chargingSlots.map((slot) => {
                      const ss = slotStatusConfig[slot.status] || slotStatusConfig.Available;
                      return (
                        <span
                          key={slot.id}
                          style={{
                            fontSize: 11,
                            fontWeight: 500,
                            padding: "3px 8px",
                            borderRadius: 6,
                            border: `1px solid ${ss.color}30`,
                            color: ss.color,
                            background: `${ss.color}10`,
                          }}
                        >
                          {slot.slotName} • {slot.powerOutput}kW
                        </span>
                      );
                    })}
                  </div>

                  {/* Action buttons */}
                  <div
                    style={{
                      display: "flex",
                      gap: 8,
                    }}
                  >
                    {/* Directions button */}
                    <button
                      onClick={() => {
                        const destLat = station.latitude;
                        const destLng = station.longitude;
                        const url = userPos
                          ? `https://www.google.com/maps/dir/${userPos[0]},${userPos[1]}/${destLat},${destLng}`
                          : `https://www.google.com/maps/dir/Current+Location/${destLat},${destLng}`;
                        window.open(url, "_blank");
                      }}
                      style={{
                        flex: 1,
                        padding: "10px 12px",
                        background: "linear-gradient(135deg, #3b82f6, #2563eb)",
                        color: "#fff",
                        border: "none",
                        borderRadius: 10,
                        fontWeight: 600,
                        fontSize: 13,
                        cursor: "pointer",
                        display: "flex",
                        alignItems: "center",
                        justifyContent: "center",
                        gap: 5,
                      }}
                    >
                      <svg
                        width="15"
                        height="15"
                        viewBox="0 0 24 24"
                        fill="none"
                        stroke="currentColor"
                        strokeWidth="2.5"
                      >
                        <path d="M3 11l19-9-9 19-2-8-8-2z" />
                      </svg>
                      Chỉ đường
                    </button>

                    {/* Detail button */}
                    <button
                      onClick={() =>
                        navigate(`/driver/station/${station.id}`)
                      }
                      style={{
                        flex: 1,
                        padding: "10px 12px",
                        background:
                          "linear-gradient(135deg, #f97316, #ea580c)",
                        color: "#fff",
                        border: "none",
                        borderRadius: 10,
                        fontWeight: 600,
                        fontSize: 13,
                        cursor: "pointer",
                        display: "flex",
                        alignItems: "center",
                        justifyContent: "center",
                        gap: 5,
                      }}
                    >
                      Xem chi tiết
                      <svg
                        width="15"
                        height="15"
                        viewBox="0 0 24 24"
                        fill="none"
                        stroke="currentColor"
                        strokeWidth="2.5"
                      >
                        <path d="M5 12h14M12 5l7 7-7 7" />
                      </svg>
                    </button>
                  </div>
                </div>
              </Popup>
            </Marker>
          );
        })}
      </MapContainer>
    </div>
  );
}
