import { useEffect, useState, useMemo, useRef } from "react";
import { useNavigate } from "react-router-dom";
import { MapContainer, TileLayer, Marker, Popup, useMap } from "react-leaflet";
import L from "leaflet";
import "leaflet/dist/leaflet.css";
import { publicStationApi, favoriteApi } from "@/services/api";
import { useAuthStore } from "@/stores/authStore";
import { showToast } from "@/components/Toast";
import TimePicker24h from "@/components/TimePicker24h";

/* ─── Fix leaflet default icon ─── */
delete L.Icon.Default.prototype._getIconUrl;
L.Icon.Default.mergeOptions({
  iconRetinaUrl: "https://cdnjs.cloudflare.com/ajax/libs/leaflet/1.9.4/images/marker-icon-2x.png",
  iconUrl: "https://cdnjs.cloudflare.com/ajax/libs/leaflet/1.9.4/images/marker-icon.png",
  shadowUrl: "https://cdnjs.cloudflare.com/ajax/libs/leaflet/1.9.4/images/marker-shadow.png",
});




/* ─── Custom marker icons ─── */
function makeIcon(gradient, pulseColor) {
  return new L.DivIcon({
    html: `
      <div style="position:relative;width:52px;height:64px;">
        <div style="position:absolute;top:6px;left:6px;width:40px;height:40px;border-radius:50%;background:${pulseColor};animation:stationPulse 2s ease-out infinite;"></div>
        <div style="position:absolute;top:0;left:2px;width:48px;height:48px;border-radius:50% 50% 50% 0;transform:rotate(-45deg);background:${gradient};border:3px solid #fff;box-shadow:0 4px 14px rgba(0,0,0,.35);"></div>
        <div style="position:absolute;top:6px;left:8px;width:36px;height:36px;display:flex;align-items:center;justify-content:center;">
          <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="#fff" stroke-width="2.5"><path d="M13 2L3 14h9l-1 8 10-12h-9l1-8z"/></svg>
        </div>
      </div>`,
    className: "",
    iconSize: [52, 64],
    iconAnchor: [26, 64],
    popupAnchor: [0, -56],
  });
}
const stationIcon = makeIcon("linear-gradient(135deg,#22c55e,#0f9d43)", "rgba(34,197,94,.35)");
const maintenanceIcon = makeIcon("linear-gradient(135deg,#f97316,#c2410c)", "rgba(249,115,22,.35)");

const userIcon = new L.DivIcon({
  html: `<div style="width:20px;height:20px;border-radius:50%;background:#3b82f6;border:3px solid #fff;box-shadow:0 0 0 6px rgba(59,130,246,.25),0 2px 8px rgba(0,0,0,.3);"></div>`,
  className: "",
  iconSize: [20, 20],
  iconAnchor: [10, 10],
});

/* ─── FlyTo ─── */
function FlyTo({ center, zoom }) {
  const map = useMap();
  useEffect(() => {
    if (center) map.flyTo(center, zoom || 15, { duration: 1.2 });
  }, [center, zoom, map]);
  return null;
}

/* ─── Status config ─── */
const statusConfig = {
  Open: { label: "Đang mở", color: "#22c55e", bg: "rgba(34,197,94,0.1)", dot: "#22c55e" },
  Closed: { label: "Đã đóng", color: "#ef4444", bg: "rgba(239,68,68,0.1)", dot: "#ef4444" },
  Maintenance: { label: "Bảo trì", color: "#f97316", bg: "rgba(249,115,22,0.1)", dot: "#f97316" },
};

/* ─── Remove Vietnamese diacritics ─── */
function removeDiacritics(str) {
  return str.normalize("NFD").replace(/[\u0300-\u036f]/g, "").replace(/đ/g, "d").replace(/Đ/g, "D");
}

/* ─── Haversine: tính khoảng cách 2 toạ độ (km) ─── */
function haversine([lat1, lon1], [lat2, lon2]) {
  const R = 6371;
  const dLat = ((lat2 - lat1) * Math.PI) / 180;
  const dLon = ((lon2 - lon1) * Math.PI) / 180;
  const a =
    Math.sin(dLat / 2) ** 2 +
    Math.cos((lat1 * Math.PI) / 180) *
    Math.cos((lat2 * Math.PI) / 180) *
    Math.sin(dLon / 2) ** 2;
  return R * 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1 - a));
}

/* ─── useIsMobile ─── */
function useIsMobile() {
  const [isMobile, setIsMobile] = useState(() => window.innerWidth <= 768);
  useEffect(() => {
    const fn = () => setIsMobile(window.innerWidth <= 768);
    window.addEventListener("resize", fn);
    return () => window.removeEventListener("resize", fn);
  }, []);
  return isMobile;
}

function pad2(value) {
  return String(value).padStart(2, "0");
}

function formatDateTimeLocal(date) {
  return `${date.getFullYear()}-${pad2(date.getMonth() + 1)}-${pad2(date.getDate())}T${pad2(date.getHours())}:${pad2(date.getMinutes())}`;
}

function formatDateKey(date) {
  return `${date.getFullYear()}-${pad2(date.getMonth() + 1)}-${pad2(date.getDate())}`;
}

function parseLocalDateTime(value) {
  return value ? new Date(value) : null;
}

function parseMinutes(value) {
  if (!value) return 0;
  const [hours = "0", minutes = "0"] = String(value).split(":");
  return Number(hours) * 60 + Number(minutes);
}

function isStationOperational(status) {
  return !["Closed", "Maintenance", "Inactive"].includes(status);
}

function isStationOpenForRange(station, start, end) {
  if (!station?.operatingHours?.length) return true;

  const dayOfWeek = start.getDay();
  const opHours = station.operatingHours.find((item) => Number(item.dayOfWeek) === dayOfWeek);
  if (!opHours || opHours.isClosed) return false;

  const bookStart = start.getHours() * 60 + start.getMinutes();
  const bookEnd = end.getHours() * 60 + end.getMinutes() + (formatDateKey(end) !== formatDateKey(start) ? 24 * 60 : 0);
  const opStart = parseMinutes(opHours.openTime);
  let opEnd = parseMinutes(opHours.closeTime);

  if (opStart === 0 && opEnd === 0) opEnd = 24 * 60;
  else if (opEnd <= opStart) opEnd += 24 * 60;

  return bookStart >= opStart && bookEnd <= opEnd;
}

function parseScheduleDateTime(raw, fallbackDateKey) {
  if (!raw) return null;
  if (typeof raw === "string" && !raw.includes("T")) {
    return new Date(`${fallbackDateKey}T${String(raw).slice(0, 5)}:00`);
  }
  return new Date(String(raw).replace("Z", ""));
}

function isRangeOverlap(startA, endA, startB, endB) {
  return startA < endB && endA > startB;
}

export default function StationMap() {
  const navigate = useNavigate();
  const isMobile = useIsMobile();
  const [activeTab, setActiveTab] = useState("list"); // "list" | "map"
  const [userPos, setUserPos] = useState(null);
  const [search, setSearch] = useState("");
  const [flyTarget, setFlyTarget] = useState(null);
  const [selectedStation, setSelectedStation] = useState(null);
  const [imgErrors, setImgErrors] = useState({});
  const [stations, setStations] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [minRating, setMinRating] = useState("");
  const [sortBy, setSortBy] = useState("");
  const [selectedDate, setSelectedDate] = useState("");
  const [startHHMM, setStartHHMM] = useState("");
  const [duration, setDuration] = useState(1);
  const [nearbyMode, setNearbyMode] = useState(false);
  const [maxRadius, setMaxRadius] = useState(10);
  const [showFilters, setShowFilters] = useState(false);
  const [favorites, setFavorites] = useState({});
  const [availableStationIds, setAvailableStationIds] = useState(null);
  const [availabilityLoading, setAvailabilityLoading] = useState(false);
  const [availabilityError, setAvailabilityError] = useState("");
  const { token } = useAuthStore();
  const markerRefs = useRef({});
  const searchTimer = useRef(null);

  const defaultCenter = [21.0285, 105.8542];

  useEffect(() => {
    navigator.geolocation?.getCurrentPosition(
      (pos) => setUserPos([pos.coords.latitude, pos.coords.longitude]),
      () => { },
      { enableHighAccuracy: true, timeout: 8000 }
    );
  }, []);




  function fetchStations(rating, sort, pos) {
    setLoading(true);
    const opts = { minRating: rating || undefined, sortBy: sort || undefined };
    if (pos) {
      opts.lat = pos[0];
      opts.lng = pos[1];
      opts.radiusKm = maxRadius;
      if (!sort) opts.sortBy = "distance";
    }
    publicStationApi.getAll(opts)
      .then((data) => {
        const list = Array.isArray(data) ? data : (data?.items ?? []);
        setStations(list);
        setError(null);
      })
      .catch((err) => setError(err.message))
      .finally(() => setLoading(false));
  }

  // Lấy dữ liệu mỗi khi bộ lọc (không bao gồm từ khoá tìm kiếm) thay đổi
  useEffect(() => {
    fetchStations(minRating, sortBy, nearbyMode ? userPos : null);
  }, [minRating, sortBy, nearbyMode, maxRadius, userPos]);

  useEffect(() => {
    if (!token) return;
    favoriteApi.getMyFavorites()
      .then(list => {
        const map = {};
        (Array.isArray(list) ? list : []).forEach(f => { map[f.stationId] = true; });
        setFavorites(map);
      })
      .catch(() => { });
  }, [token]);

  async function toggleFavorite(e, stationId) {
    e.stopPropagation();
    if (!token) { showToast.error("Đăng nhập để thêm yêu thích"); return; }
    try {
      if (favorites[stationId]) {
        await favoriteApi.remove(stationId);
        setFavorites(prev => { const n = { ...prev }; delete n[stationId]; return n; });
      } else {
        await favoriteApi.add(stationId);
        setFavorites(prev => ({ ...prev, [stationId]: true }));
      }
    } catch (err) { showToast.error(err.message); }
  }

  const baseFiltered = useMemo(() => {
    let result = stations;
    if (search.trim()) {
      const q = removeDiacritics(search.toLowerCase());
      result = result.filter(
        (s) =>
          removeDiacritics((s.name || "").toLowerCase()).includes(q) ||
          removeDiacritics((s.address || "").toLowerCase()).includes(q)
      );
    }
    if (nearbyMode && userPos) {
      result = result
        .map((s) => ({
          ...s,
          _distance: haversine(userPos, [s.latitude, s.longitude]),
        }))
        .filter((s) => s._distance <= maxRadius)
        .sort((a, b) => a._distance - b._distance);
    } else {
      result = result.map(({ _distance, ...s }) => s);
    }
    return result;
  }, [search, stations, nearbyMode, userPos, maxRadius]);

  const timeRange = useMemo(() => {
    if (!selectedDate || !startHHMM) return { start: null, end: null, isValid: false };
    const startObj = new Date(`${selectedDate}T${startHHMM}:00`);
    const endObj = new Date(startObj.getTime() + duration * 3600000);
    return { start: startObj, end: endObj, isValid: endObj > startObj };
  }, [selectedDate, startHHMM, duration]);

  const isTimeFilterActive = Boolean(selectedDate && startHHMM);

  useEffect(() => {
    if (!isTimeFilterActive) {
      setAvailableStationIds(null);
      setAvailabilityLoading(false);
      setAvailabilityError("");
      return;
    }

    if (!timeRange.isValid) {
      setAvailableStationIds(new Set());
      setAvailabilityLoading(false);
      setAvailabilityError("");
      return;
    }

    const controller = new AbortController();
    const baseUrl = import.meta.env.VITE_BASE_URL || "https://chargeslot-api-f8b5brexe2b0ekhp.japaneast-01.azurewebsites.net/api";
    let cancelled = false;

    async function fetchSlotRanges(stationId, slotId, dateKey) {
      const headers = token ? { Authorization: `Bearer ${token}` } : {};
      const response = await fetch(
        `${baseUrl}/stations/${stationId}/slots/${slotId}/availability?date=${dateKey}`,
        { headers, signal: controller.signal }
      );

      if (!response.ok) {
        throw new Error(`Khong the tai lich slot cho tram #${stationId}`);
      }

      const data = await response.json();
      if (Array.isArray(data)) return data;
      if (Array.isArray(data?.bookedRanges)) return data.bookedRanges;
      if (Array.isArray(data?.items)) return data.items;
      return [];
    }

    async function stationHasFreeSlot(station) {
      if (!isStationOperational(station.operationalStatus)) return false;
      if (!isStationOpenForRange(station, timeRange.start, timeRange.end)) return false;

      const candidateSlots = (station.chargingSlots || []).filter((slot) => {
        const status = slot.status || "Active";
        return ["Active", "Available", "Booked"].includes(status);
      });

      if (candidateSlots.length === 0) return false;

      const dateKeys = [formatDateKey(timeRange.start)];
      const endDateKey = formatDateKey(timeRange.end);
      if (endDateKey !== dateKeys[0]) dateKeys.push(endDateKey);

      for (const slot of candidateSlots) {
        let bookedRanges = [];

        for (const dateKey of dateKeys) {
          const ranges = await fetchSlotRanges(station.id, slot.id, dateKey);
          bookedRanges = bookedRanges.concat(
            ranges
              .map((range) => ({
                start: parseScheduleDateTime(range.startTime, dateKey),
                end: parseScheduleDateTime(range.endTime, dateKey),
              }))
              .filter((range) => range.start && range.end)
          );
        }

        const hasConflict = bookedRanges.some((range) =>
          isRangeOverlap(timeRange.start, timeRange.end, range.start, range.end)
        );

        if (!hasConflict) return true;
      }

      return false;
    }

    setAvailabilityLoading(true);
    setAvailabilityError("");
    setAvailableStationIds(null);

    (async () => {
      const matches = await Promise.all(
        baseFiltered.map(async (station) => ({
          id: station.id,
          isAvailable: await stationHasFreeSlot(station),
        }))
      );

      if (cancelled) return;
      setAvailableStationIds(new Set(matches.filter((item) => item.isAvailable).map((item) => item.id)));
    })()
      .catch((err) => {
        if (cancelled || err?.name === "AbortError") return;
        setAvailabilityError(err.message || "Khong the loc theo thoi gian");
        setAvailableStationIds(new Set());
      })
      .finally(() => {
        if (!cancelled) setAvailabilityLoading(false);
      });

    return () => {
      cancelled = true;
      controller.abort();
    };
  }, [baseFiltered, isTimeFilterActive, timeRange, token]);

  const filtered = useMemo(() => {
    if (!isTimeFilterActive) return baseFiltered;
    if (!timeRange.isValid) return [];
    if (availableStationIds == null) return [];
    return baseFiltered.filter((station) => availableStationIds.has(station.id));
  }, [availableStationIds, baseFiltered, isTimeFilterActive, timeRange.isValid]);

  function getAvailableSlots(station) {
    if (station.availableSlotsCount !== undefined) return station.availableSlotsCount;
    return station.chargingSlots ? station.chargingSlots.filter((s) => s.status === "Active").length : 0;
  }

  function handleStationClick(station) {
    setFlyTarget([station.latitude, station.longitude]);
    setSelectedStation(station.id);
    if (isMobile) setActiveTab("map");
    setTimeout(() => {
      markerRefs.current[station.id]?.openPopup();
    }, 1300);
  }

  function getStationImage(s) {
    if (imgErrors[s.id]) return null;
    const url = s.images?.[0]?.imageUrl || s.images?.[0] || null;
    if (!url) return null;
    return url.startsWith("http") ? url : `https://chargeslot-api-f8b5brexe2b0ekhp.japaneast-01.azurewebsites.net${url}`;
  }

  const hasActiveFilters = minRating || sortBy || nearbyMode || isTimeFilterActive;

  /* ══════ MAP PANEL (shared between mobile & desktop) ══════ */
  const MapPanel = (
    <div className="sm-map-view">
      {/* Locate me btn */}
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
              () => showToast.error("Không thể lấy vị trí của bạn"),
              { enableHighAccuracy: true, timeout: 8000 }
            );
        }}
        className="sm-locate-btn"
        title="Vị trí của tôi"
      >
        <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
          <circle cx="12" cy="12" r="3" />
          <path d="M12 2v4M12 18v4M2 12h4M18 12h4" />
        </svg>
      </button>

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
        {userPos && <Marker position={userPos} icon={userIcon} />}

        {filtered.map((station) => {
          const available = getAvailableSlots(station);
          const isFull = available === 0;
          const st = statusConfig[station.operationalStatus] || statusConfig.Open;
          const icon = station.operationalStatus === "Maintenance" ? maintenanceIcon : stationIcon;

          return (
            <Marker
              key={station.id}
              position={[station.latitude, station.longitude]}
              icon={icon}
              ref={(ref) => { if (ref) markerRefs.current[station.id] = ref; }}
            >
              <Popup maxWidth={300} minWidth={260}>
                <div style={{ fontFamily: "Inter, system-ui, sans-serif" }}>
                  <div style={{ display: "flex", alignItems: "center", gap: 8, marginBottom: 8 }}>
                    <div style={{
                      width: 36, height: 36, borderRadius: 10,
                      background: isFull ? "linear-gradient(135deg, #ef4444, #dc2626)" : "linear-gradient(135deg, #22c55e, #16a34a)",
                      display: "flex", alignItems: "center", justifyContent: "center", flexShrink: 0,
                    }}>
                      <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="#fff" strokeWidth="2.5">
                        <path d="M13 2L3 14h9l-1 8 10-12h-9l1-8z" />
                      </svg>
                    </div>
                    <div style={{ flex: 1 }}>
                      <div style={{ fontWeight: 700, fontSize: 15, color: "#1f2937", lineHeight: 1.3 }}>
                        {station.name}
                      </div>
                      <div style={{ display: "flex", alignItems: "center", gap: 4, marginTop: 2 }}>
                        {isFull ? (
                          <>
                            <div style={{ width: 6, height: 6, borderRadius: "50%", background: "#ef4444" }} />
                            <span style={{ fontSize: 11, fontWeight: 700, color: "#ef4444" }}>Kín lịch</span>
                          </>
                        ) : (
                          <>
                            <div style={{ width: 6, height: 6, borderRadius: "50%", background: st.dot }} />
                            <span style={{ fontSize: 11, fontWeight: 600, color: st.color }}>{st.label}</span>
                          </>
                        )}
                      </div>
                    </div>
                  </div>

                  <div style={{ fontSize: 13, color: "#6b7280", marginBottom: 10, lineHeight: 1.4 }}>
                    📍 {station.address}
                  </div>

                  <div style={{ display: "flex", gap: 6, marginBottom: 12 }}>
                    <div style={{ flex: 1, background: "#f0fdf4", borderRadius: 10, padding: "8px 10px", textAlign: "center" }}>
                      <div style={{ fontSize: 18, fontWeight: 700, color: "#22c55e" }}>{available}</div>
                      <div style={{ fontSize: 10, color: "#6b7280", marginTop: 2 }}>Slot trống</div>
                    </div>
                    <div style={{ flex: 1, background: "#eff6ff", borderRadius: 10, padding: "8px 10px", textAlign: "center" }}>
                      <div style={{ fontSize: 18, fontWeight: 700, color: "#3b82f6" }}>{station.chargingSlots.length}</div>
                      <div style={{ fontSize: 10, color: "#6b7280", marginTop: 2 }}>Tổng slot</div>
                    </div>
                    {station.chargingSlots.length > 0 && (
                      <div style={{ flex: 1, background: "#fef3c7", borderRadius: 10, padding: "8px 10px", textAlign: "center" }}>
                        <div style={{ fontSize: 14, fontWeight: 700, color: "#d97706" }}>
                          {(() => {
                            const prices = (station.pricingTiers || []).filter(t => t.isActive !== false).map(t => t.pricePerHour);
                            return prices.length > 0 ? Math.min(...prices).toLocaleString("vi-VN") : "---";
                          })()}đ
                        </div>
                        <div style={{ fontSize: 10, color: "#6b7280", marginTop: 2 }}>Giá từ/h</div>
                      </div>
                    )}
                  </div>

                  <div style={{ display: "flex", gap: 8 }}>
                    <button
                      onClick={() => {
                        const url = userPos
                          ? `https://www.google.com/maps/dir/${userPos[0]},${userPos[1]}/${station.latitude},${station.longitude}`
                          : `https://www.google.com/maps/dir/Current+Location/${station.latitude},${station.longitude}`;
                        window.open(url, "_blank");
                      }}
                      style={{
                        flex: 1, padding: "10px 12px",
                        background: "linear-gradient(135deg, #3b82f6, #2563eb)",
                        color: "#fff", border: "none", borderRadius: 10,
                        fontWeight: 600, fontSize: 13, cursor: "pointer",
                        display: "flex", alignItems: "center", justifyContent: "center", gap: 5,
                      }}
                    >
                      <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5">
                        <path d="M3 11l19-9-9 19-2-8-8-2z" />
                      </svg>
                      Chỉ đường
                    </button>
                    <button
                      onClick={() => navigate(`/driver/station/${station.id}`)}
                      style={{
                        flex: 1, padding: "10px 12px",
                        background: "linear-gradient(135deg, #f97316, #ea580c)",
                        color: "#fff", border: "none", borderRadius: 10,
                        fontWeight: 600, fontSize: 13, cursor: "pointer",
                        display: "flex", alignItems: "center", justifyContent: "center", gap: 5,
                      }}
                    >
                      Chi tiết
                      <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5">
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

  /* ══════ LIST PANEL (header + cards) ══════ */
  const ListPanel = (
    <div className="sm-list-panel">
      {/* ── Header ── */}
      <div className="sm-list-header">
        <div className="sm-list-header__top">
          <div className="sm-list-header__title-group">
            <div className="sm-list-header__icon">
              <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="#fff" strokeWidth="2.5">
                <path d="M13 2L3 14h9l-1 8 10-12h-9l1-8z" />
              </svg>
            </div>
            <div>
              <div className="sm-list-header__title">Trạm sạc</div>
              <div className="sm-list-header__subtitle">
                {filtered.length} trạm
                {search && ` · "${search}"`}
                {nearbyMode && userPos && ` · ${maxRadius}km`}
                {nearbyMode && !userPos && ` · đang lấy vị trí...`}
              </div>
            </div>
          </div>

          {/* Filter toggle btn */}
          <button
            className={`sm-filter-toggle ${hasActiveFilters ? "active" : ""}`}
            onClick={() => setShowFilters(v => !v)}
          >
            <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5">
              <path d="M22 3H2l8 9.46V19l4 2v-8.54L22 3z" />
            </svg>
            Lọc
            {hasActiveFilters && <span className="sm-filter-badge" />}
          </button>
        </div>

        {/* Search bar */}
        <div className="sm-search-bar">
          <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="#9ca3af" strokeWidth="2">
            <circle cx="11" cy="11" r="8" />
            <path d="m21 21-4.3-4.3" />
          </svg>
          <input
            className="sm-search-input"
            type="text"
            placeholder="Tìm theo tên, địa chỉ..."
            value={search}
            onChange={(e) => setSearch(e.target.value)}
          />
          {search && (
            <button className="sm-search-clear" onClick={() => setSearch("")}>
              <svg width="11" height="11" viewBox="0 0 24 24" fill="none" stroke="#6b7280" strokeWidth="3">
                <path d="M18 6L6 18M6 6l12 12" />
              </svg>
            </button>
          )}
        </div>

        {/* ── Collapsible Filter Panel ── */}
        {showFilters && (
          <div className="sm-filter-panel">
            <div className="sm-filter-row">
              <select
                value={minRating}
                onChange={(e) => setMinRating(e.target.value)}
                className="sm-select"
              >
                <option value="">⭐ Đánh giá</option>
                <option value="1">≥ 1 ⭐</option>
                <option value="2">≥ 2 ⭐</option>
                <option value="3">≥ 3 ⭐</option>
                <option value="4">≥ 4 ⭐</option>
                <option value="4.5">≥ 4.5 ⭐</option>
              </select>
              <select
                value={sortBy}
                onChange={(e) => { setSortBy(e.target.value); setNearbyMode(false); }}
                className="sm-select"
              >
                <option value="">Sắp xếp</option>
                <option value="rating">Đánh giá cao nhất</option>
                <option value="reviews">Nhiều đánh giá nhất</option>
              </select>
            </div>

            <div className="sm-time-filter-card">
              <div className="sm-time-filter-head">
                <div className="sm-time-filter-title">Lọc trạm có slot trống theo thời gian</div>
              </div>

              {/* ===== DATE PICKER ===== */}
              <div style={{ marginBottom: 16 }}>
                <label style={{display: "block", fontSize: 13, fontWeight: 700, color: "#1e293b", marginBottom: 8}}>Chọn ngày</label>
                <div style={{ display: "flex", gap: 8, overflowX: "auto", paddingBottom: 6 }}>
                  {Array.from({ length: 7 }, (_, i) => {
                    const d = new Date();
                    d.setDate(d.getDate() + i);
                    const yyyy = d.getFullYear();
                    const mm = String(d.getMonth() + 1).padStart(2, "0");
                    const dd2 = String(d.getDate()).padStart(2, "0");
                    const dateStr = `${yyyy}-${mm}-${dd2}`;
                    const isSel = selectedDate === dateStr;
                    const DAY_NAMES = ["CN", "T2", "T3", "T4", "T5", "T6", "T7"];
                    const label = i === 0 ? "Hôm nay" : i === 1 ? "Ngày mai" : DAY_NAMES[d.getDay()];
                    return (
                      <button key={dateStr} type="button" onClick={() => {
                        setSelectedDate(dateStr);
                        if (!startHHMM) {
                          const now = new Date();
                          now.setMinutes(now.getMinutes() < 30 ? 30 : 0);
                          if (now.getMinutes() === 0) now.setHours(now.getHours() + 1);
                          setStartHHMM(`${String(now.getHours()).padStart(2, "0")}:${String(now.getMinutes()).padStart(2, "0")}`);
                        }
                      }} style={{
                        flexShrink: 0, minWidth: 68, padding: "8px 10px", borderRadius: 14,
                        border: isSel ? "2px solid #f97316" : "1.5px solid #e5e7eb",
                        background: isSel ? "linear-gradient(135deg,#fff7ed,#ffedd5)" : "#fff",
                        color: isSel ? "#ea580c" : "#374151",
                        cursor: "pointer", textAlign: "center",
                        boxShadow: isSel ? "0 2px 8px rgba(249,115,22,0.2)" : "none",
                        transition: "all 0.15s",
                      }}>
                        <div style={{ fontSize: 10, fontWeight: 600, color: isSel ? "#ea580c" : "#94a3b8" }}>{label}</div>
                        <div style={{ fontSize: 15, fontWeight: 800 }}>{dd2}/{mm}</div>
                      </button>
                    );
                  })}

                  {/* ===== DATE PICKER BUTTON ===== */}
                  {(() => {
                    let isCustomActive = false;
                    let customSelDate = null;
                    if (selectedDate) {
                      const today = new Date();
                      today.setHours(0, 0, 0, 0);
                      const sel = new Date(selectedDate);
                      sel.setHours(0, 0, 0, 0);
                      const diffDays = Math.round((sel - today) / (1000 * 60 * 60 * 24));
                      if (diffDays < 0 || diffDays >= 7) {
                        isCustomActive = true;
                        customSelDate = sel;
                      }
                    }

                    return (
                      <div style={{
                        position: "relative", flexShrink: 0, minWidth: 68,
                        display: "flex", flexDirection: "column", alignItems: "center", justifyContent: "center",
                        padding: "8px 10px", borderRadius: 14,
                        border: isCustomActive ? "2px solid #f97316" : "1.5px dashed #cbd5e1",
                        background: isCustomActive ? "linear-gradient(135deg,#fff7ed,#ffedd5)" : "#f8fafc",
                        cursor: "pointer", transition: "all 0.15s",
                        boxShadow: isCustomActive ? "0 2px 8px rgba(249,115,22,0.2)" : "none",
                      }}>
                        <input
                          type="date"
                          min={new Date().toISOString().split("T")[0]}
                          value={isCustomActive && customSelDate ? selectedDate : ""}
                          onClick={(e) => { try { e.target.showPicker(); } catch (err) {} }}
                          onChange={(e) => {
                            if (!e.target.value) return;
                            setSelectedDate(e.target.value);
                            if (!startHHMM) {
                              const now = new Date();
                              now.setMinutes(now.getMinutes() < 30 ? 30 : 0);
                              if (now.getMinutes() === 0) now.setHours(now.getHours() + 1);
                              setStartHHMM(`${String(now.getHours()).padStart(2, "0")}:${String(now.getMinutes()).padStart(2, "0")}`);
                            }
                          }}
                          style={{
                            position: "absolute", top: 0, left: 0, right: 0, bottom: 0,
                            opacity: 0, cursor: "pointer", width: "100%", height: "100%"
                          }}
                        />
                        {isCustomActive && customSelDate ? (
                          <>
                            <div style={{ fontSize: 10, fontWeight: 600, color: "#ea580c" }}>Đã chọn</div>
                            <div style={{ fontSize: 15, fontWeight: 800, color: "#ea580c" }}>
                              {String(customSelDate.getDate()).padStart(2, "0")}/{String(customSelDate.getMonth() + 1).padStart(2, "0")}
                            </div>
                          </>
                        ) : (
                          <>
                            <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="#64748b" strokeWidth="2" style={{ marginBottom: 2 }}>
                              <rect x="3" y="4" width="18" height="18" rx="2" ry="2"/>
                              <line x1="16" y1="2" x2="16" y2="6"/>
                              <line x1="8" y1="2" x2="8" y2="6"/>
                              <line x1="3" y1="10" x2="21" y2="10"/>
                            </svg>
                            <div style={{ fontSize: 10, fontWeight: 600, color: "#64748b" }}>Ngày khác</div>
                          </>
                        )}
                      </div>
                    );
                  })()}
                </div>
              </div>

              {/* ===== TIME INPUTS ===== */}
              <div style={{ display: "flex", gap: 12, alignItems: "flex-end", marginBottom: 12 }}>
                <div style={{ flex: 1 }}>
                  <div style={{ fontSize: 13, color: "#1e293b", fontWeight: 700, marginBottom: 8 }}>Giờ bắt đầu</div>
                  <TimePicker24h
                    value={startHHMM}
                    onChange={setStartHHMM}
                    className="w-full text-center"
                  />
                </div>
                <div style={{ flex: 1, paddingBottom: 1 }}>
                  <div style={{ fontSize: 13, color: "#1e293b", fontWeight: 700, marginBottom: 8 }}>Thời lượng (giờ)</div>
                  <div style={{ display: "flex", alignItems: "center", background: "#f8fafc", borderRadius: 10, border: "1.5px solid #e5e7eb", overflow: "hidden", height: 42 }}>
                    <button 
                      type="button" 
                      onClick={() => setDuration(d => Math.max(0.5, d - 0.5))} 
                      style={{ width: 44, height: 42, background: "#fff", border: "none", cursor: "pointer", fontSize: 20, color: duration > 0.5 ? "#ea580c" : "#cbd5e1", borderRight: "1px solid #e5e7eb", transition: "all 0.2s", display: "flex", alignItems: "center", justifyContent: "center" }}
                      disabled={duration <= 0.5}
                    >−</button>

                    <div style={{ flex: 1, textAlign: "center", fontSize: 14, fontWeight: 700, color: "#1e293b", userSelect: "none" }}>
                      {duration} 
                    </div>

                    <button 
                      type="button" 
                      onClick={() => setDuration(d => Math.min(24, d + 0.5))} 
                      style={{ width: 44, height: 42, background: "#fff", border: "none", cursor: "pointer", fontSize: 20, color: duration < 24 ? "#ea580c" : "#cbd5e1", borderLeft: "1px solid #e5e7eb", transition: "all 0.2s", display: "flex", alignItems: "center", justifyContent: "center" }}
                      disabled={duration >= 24}
                    >+</button>
                  </div>
                </div>
              </div>

              {isTimeFilterActive && timeRange.isValid && (
                <div className={`sm-time-filter-status ${availabilityLoading ? "sm-time-filter-status--loading" : "sm-time-filter-status--active"}`}>
                  {availabilityLoading
                    ? "Đang lọc trạm..."
                    : `Lọc trạm từ ${timeRange.start.toLocaleTimeString("vi-VN", {hour: "2-digit", minute: "2-digit"})} đến ${timeRange.end.toLocaleTimeString("vi-VN", {hour: "2-digit", minute: "2-digit"})}`}
                </div>
              )}

              {availabilityError && (
                <div className="sm-time-filter-status sm-time-filter-status--error">
                  {availabilityError}
                </div>
              )}
            </div>

            <div className="sm-filter-nearby-row">
              <button
                className={`sm-nearby-btn ${nearbyMode ? "active" : ""}`}
                onClick={() => {
                  if (!userPos) {
                    navigator.geolocation?.getCurrentPosition(
                      (pos) => {
                        setUserPos([pos.coords.latitude, pos.coords.longitude]);
                        setNearbyMode(true);
                        setSortBy("");
                      },
                      () => showToast.error("Không thể lấy vị trí. Vui lòng cho phép GPS."),
                      { enableHighAccuracy: true, timeout: 8000 }
                    );
                  } else {
                    setNearbyMode((v) => { if (!v) setSortBy(""); return !v; });
                  }
                }}
              >
                <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5">
                  <circle cx="12" cy="12" r="3" />
                  <path d="M12 2v4M12 18v4M2 12h4M18 12h4" />
                </svg>
                {nearbyMode ? "Đang xem gần tôi" : "Gần tôi"}
              </button>

              {nearbyMode && (
                <select
                  value={maxRadius}
                  onChange={(e) => setMaxRadius(Number(e.target.value))}
                  className="sm-select sm-select--blue"
                >
                  <option value={2}>Trong 2 km</option>
                  <option value={5}>Trong 5 km</option>
                  <option value={10}>Trong 10 km</option>
                  <option value={20}>Trong 20 km</option>
                  <option value={50}>Trong 50 km</option>
                </select>
              )}

              {hasActiveFilters && (
                <button
                  className="sm-clear-filter"
                  onClick={() => {
                    setMinRating("");
                    setSortBy("");
                    setNearbyMode(false);
                    setSelectedDate("");
                    setStartHHMM("");
                    setDuration(1);
                  }}
                >
                  Xóa lọc
                </button>
              )}
            </div>
          </div>
        )}
      </div>

      {/* ── Cards ── */}
      <div className="sm-card-list">
        {loading || availabilityLoading ? (
          <div className="sm-state-box">
            <div style={{ fontSize: 32, marginBottom: 8, animation: "spin 1s linear infinite" }}>⚡</div>
            <div style={{ fontSize: 14, color: "#6b7280" }}>Đang tải trạm sạc...</div>
          </div>
        ) : error ? (
          <div className="sm-state-box">
            <div style={{ fontSize: 40, marginBottom: 8, opacity: 0.5 }}>⚠️</div>
            <div style={{ fontSize: 14, color: "#ef4444", marginBottom: 8 }}>{error}</div>
            <button className="sm-retry-btn" onClick={() => window.location.reload()}>Thử lại</button>
          </div>
        ) : filtered.length === 0 ? (
          <div className="sm-state-box">
            <div style={{ fontSize: 48, marginBottom: 12, opacity: 0.4 }}>🔍</div>
            <div style={{ fontSize: 15, fontWeight: 600, color: "#6b7280" }}>Không tìm thấy trạm sạc</div>
            <div style={{ fontSize: 13, color: "#9ca3af", marginTop: 4 }}>Thử nhập từ khóa khác</div>
          </div>
        ) : (
          filtered.map((s) => {
            const available = getAvailableSlots(s);
            const isFull = available === 0;
            const st = statusConfig[s.operationalStatus] || statusConfig.Open;
            const isSelected = selectedStation === s.id;
            const img = getStationImage(s);

            return (
              <div
                key={s.id}
                className={`sm-card ${isSelected ? "sm-card--selected" : ""}`}
                onClick={() => handleStationClick(s)}
                style={{ opacity: isFull ? 0.65 : 1, transition: "opacity 0.2s" }}
              >
                {/* Image area */}
                <div className="sm-card__img-wrap">
                  {img ? (
                    <img
                      src={img}
                      alt={s.name}
                      className="sm-card__img"
                      onError={() => setImgErrors((prev) => ({ ...prev, [s.id]: true }))}
                    />
                  ) : (
                    <div className="sm-card__img-fallback">
                      <svg width="26" height="26" viewBox="0 0 24 24" fill="none" stroke="#22c55e" strokeWidth="2">
                        <path d="M13 2L3 14h9l-1 8 10-12h-9l1-8z" />
                      </svg>
                    </div>
                  )}

                  {/* Favorite */}
                  <button
                    className="sm-card__fav"
                    onClick={(e) => toggleFavorite(e, s.id)}
                    title={favorites[s.id] ? "Bỏ yêu thích" : "Thêm yêu thích"}
                  >
                    <svg width="15" height="15" viewBox="0 0 24 24"
                      fill={favorites[s.id] ? "#ef4444" : "none"}
                      stroke={favorites[s.id] ? "#ef4444" : "#9ca3af"} strokeWidth="2">
                      <path d="M20.84 4.61a5.5 5.5 0 0 0-7.78 0L12 5.67l-1.06-1.06a5.5 5.5 0 0 0-7.78 7.78l1.06 1.06L12 21.23l7.78-7.78 1.06-1.06a5.5 5.5 0 0 0 0-7.78z" />
                    </svg>
                  </button>

                  {/* Status badge */}
                  <div className="sm-card__status-badge">
                    {isFull ? (
                      <>
                        <div className="sm-card__status-dot" style={{ background: "#ef4444", boxShadow: `0 0 5px #ef4444` }} />
                        <span style={{ color: "#ef4444", fontWeight: 700 }}>Kín lịch</span>
                      </>
                    ) : (
                      <>
                        <div className="sm-card__status-dot" style={{ background: st.dot, boxShadow: `0 0 5px ${st.dot}` }} />
                        <span style={{ color: st.color }}>{st.label}</span>
                      </>
                    )}
                  </div>
                </div>

                {/* Info */}
                <div className="sm-card__body">
                  <div className="sm-card__name">{s.name}</div>
                  <div className="sm-card__address">
                    <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="#94a3b8" strokeWidth="2">
                      <path d="M21 10c0 7-9 13-9 13s-9-6-9-13a9 9 0 0 1 18 0z" />
                      <circle cx="12" cy="10" r="3" />
                    </svg>
                    <span>{s.address}</span>
                  </div>

                  {/* Distance badge */}
                  {nearbyMode && s._distance !== undefined && (
                    <div className="sm-card__distance-badge">
                      <svg width="11" height="11" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5">
                        <circle cx="12" cy="12" r="3" />
                        <path d="M12 2v4M12 18v4M2 12h4M18 12h4" />
                      </svg>
                      {s._distance < 1 ? `${Math.round(s._distance * 1000)} m` : `${s._distance.toFixed(1)} km`}
                    </div>
                  )}

                  {/* Stats row */}
                  <div className="sm-card__stats">
                    <div className="sm-card__stat sm-card__stat--yellow">
                      <div className="sm-card__stat-val">⭐ {s.totalReviews > 0 ? Number(s.averageRating).toFixed(1) : "—"}</div>
                      <div className="sm-card__stat-lbl">{s.totalReviews > 0 ? `${s.totalReviews} đánh giá` : "Chưa có"}</div>
                    </div>
                    <div className="sm-card__stat sm-card__stat--green">
                      <div className="sm-card__stat-val">{available}</div>
                      <div className="sm-card__stat-lbl">Slot trống</div>
                    </div>
                    <div className="sm-card__stat sm-card__stat--blue">
                      <div className="sm-card__stat-val">{s.chargingSlots.length}</div>
                      <div className="sm-card__stat-lbl">Tổng slot</div>
                    </div>
                    {s.chargingSlots.length > 0 && (
                      <div className="sm-card__stat sm-card__stat--amber">
                        <div className="sm-card__stat-val" style={{ fontSize: 12 }}>
                          {(() => {
                            const prices = (s.pricingTiers || []).filter(t => t.isActive !== false).map(t => t.pricePerHour);
                            return prices.length > 0 ? Math.min(...prices).toLocaleString("vi-VN") : "---";
                          })()}đ
                        </div>
                        <div className="sm-card__stat-lbl">Giá/h</div>
                      </div>
                    )}
                  </div>
                </div>
              </div>
            );
          })
        )}
      </div>
    </div>
  );

  /* ══════════════════════════════════
     DESKTOP LAYOUT (side by side)
  ══════════════════════════════════ */
  if (!isMobile) {
    return (
      <div className="sm-root sm-root--desktop">
        {ListPanel}
        {MapPanel}
      </div>
    );
  }

  /* ══════════════════════════════════
     MOBILE LAYOUT (tab-based)
  ══════════════════════════════════ */
  return (
    <div className="sm-root sm-root--mobile">
      {/* Tab switcher */}
      <div className="sm-tab-bar">
        <button
          className={`sm-tab-btn ${activeTab === "list" ? "sm-tab-btn--active" : ""}`}
          onClick={() => setActiveTab("list")}
        >
          <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5">
            <rect x="3" y="3" width="7" height="7" rx="1" />
            <rect x="14" y="3" width="7" height="7" rx="1" />
            <rect x="3" y="14" width="7" height="7" rx="1" />
            <rect x="14" y="14" width="7" height="7" rx="1" />
          </svg>
          Danh sách
          {filtered.length > 0 && (
            <span className="sm-tab-count">{filtered.length}</span>
          )}
        </button>
        <button
          className={`sm-tab-btn ${activeTab === "map" ? "sm-tab-btn--active" : ""}`}
          onClick={() => setActiveTab("map")}
        >
          <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5">
            <polygon points="3 6 9 3 15 6 21 3 21 18 15 21 9 18 3 21" />
            <line x1="9" y1="3" x2="9" y2="18" />
            <line x1="15" y1="6" x2="15" y2="21" />
          </svg>
          Bản đồ
        </button>
      </div>

      {/* Content */}
      <div className="sm-mobile-content">
        <div className={`sm-mobile-pane ${activeTab === "list" ? "sm-mobile-pane--active" : ""}`}>
          {ListPanel}
        </div>
        <div className={`sm-mobile-pane ${activeTab === "map" ? "sm-mobile-pane--active" : ""}`}>
          {MapPanel}
        </div>
      </div>
    </div>
  );
}
