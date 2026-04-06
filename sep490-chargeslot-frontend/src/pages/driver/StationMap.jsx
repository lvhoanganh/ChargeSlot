import { useEffect, useState, useMemo, useRef } from "react";
import { useNavigate } from "react-router-dom";
import { MapContainer, TileLayer, Marker, Popup, useMap } from "react-leaflet";
import L from "leaflet";
import "leaflet/dist/leaflet.css";
import { publicStationApi, favoriteApi } from "@/services/api";
import { useAuthStore } from "@/stores/authStore";
import { showToast } from "@/components/Toast";

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
  const [nearbyMode, setNearbyMode] = useState(false);
  const [maxRadius, setMaxRadius] = useState(10);
  const [showFilters, setShowFilters] = useState(false);
  const [favorites, setFavorites] = useState({});
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

  const filtered = useMemo(() => {
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

  function getAvailableSlots(station) {
    return station.chargingSlots.filter((s) => s.status === "Active").length;
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

  const hasActiveFilters = minRating || sortBy || nearbyMode;

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
                      background: "linear-gradient(135deg, #22c55e, #16a34a)",
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
                        <div style={{ width: 6, height: 6, borderRadius: "50%", background: st.dot }} />
                        <span style={{ fontSize: 11, fontWeight: 600, color: st.color }}>{st.label}</span>
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
                  onClick={() => { setMinRating(""); setSortBy(""); setNearbyMode(false); }}
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
        {loading ? (
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
            const st = statusConfig[s.operationalStatus] || statusConfig.Open;
            const isSelected = selectedStation === s.id;
            const img = getStationImage(s);

            return (
              <div
                key={s.id}
                className={`sm-card ${isSelected ? "sm-card--selected" : ""}`}
                onClick={() => handleStationClick(s)}
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
                    <div className="sm-card__status-dot" style={{ background: st.dot, boxShadow: `0 0 5px ${st.dot}` }} />
                    <span style={{ color: st.color }}>{st.label}</span>
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
