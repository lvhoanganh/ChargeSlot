import { useState, useRef, useEffect } from "react";
import { useNavigate, useParams, Link } from "react-router-dom";
import { Button } from "@/components/ui/button";
import { stationApi } from "@/services/api";
import { showToast } from "@/components/Toast";
import { MapContainer, TileLayer, Marker, useMapEvents } from "react-leaflet";
import L from "leaflet";
import "leaflet/dist/leaflet.css";

/* Fix default marker icon */
delete L.Icon.Default.prototype._getIconUrl;
L.Icon.Default.mergeOptions({
  iconRetinaUrl: "https://unpkg.com/leaflet@1.9.4/dist/images/marker-icon-2x.png",
  iconUrl: "https://unpkg.com/leaflet@1.9.4/dist/images/marker-icon.png",
  shadowUrl: "https://unpkg.com/leaflet@1.9.4/dist/images/marker-shadow.png",
});

/* ─── Map helpers ─── */
function MapFlyTo({ lat, lng }) {
  const map = useMapEvents({});
  useEffect(() => {
    if (lat && lng) map.flyTo([lat, lng], 17, { duration: 1.2 });
  }, [lat, lng, map]);
  return null;
}

function LocationMarker({ pos, onSelect }) {
  useMapEvents({
    click: async (e) => {
      const { lat, lng } = e.latlng;
      onSelect(lat, lng, null);
      try {
        const res = await fetch(
          `https://nominatim.openstreetmap.org/reverse?lat=${lat}&lon=${lng}&format=json&accept-language=vi`
        );
        const data = await res.json();
        onSelect(lat, lng, data.display_name || "");
      } catch {
        onSelect(lat, lng, "");
      }
    },
  });
  return pos ? <Marker position={pos} /> : null;
}

function MapPicker({ lat, lng, address, onSelect }) {
  const [query, setQuery] = useState(address || "");
  const [results, setResults] = useState([]);
  const [searching, setSearching] = useState(false);
  const [markerPos, setMarkerPos] = useState(lat && lng ? [lat, lng] : null);
  const debounceRef = useRef(null);

  useEffect(() => {
    if (lat && lng) setMarkerPos([lat, lng]);
    if (address) setQuery(address);
  }, [lat, lng, address]);

  function handleQueryChange(value) {
    setQuery(value);
    if (debounceRef.current) clearTimeout(debounceRef.current);
    if (value.trim().length < 3) { setResults([]); return; }
    debounceRef.current = setTimeout(async () => {
      setSearching(true);
      try {
        const res = await fetch(
          `https://nominatim.openstreetmap.org/search?q=${encodeURIComponent(value)}&format=json&limit=5&accept-language=vi&countrycodes=vn`
        );
        const data = await res.json();
        setResults(data);
      } catch { setResults([]); }
      setSearching(false);
    }, 400);
  }

  function handleSelectResult(item) {
    const rLat = parseFloat(item.lat);
    const rLng = parseFloat(item.lon);
    setMarkerPos([rLat, rLng]);
    setQuery(item.display_name);
    setResults([]);
    onSelect(rLat, rLng, item.display_name);
  }

  function handleMapClick(cLat, cLng, addr) {
    setMarkerPos([cLat, cLng]);
    if (addr !== null) setQuery(addr);
    onSelect(cLat, cLng, addr);
  }

  const centerLat = markerPos?.[0] || lat || 10.7295;
  const centerLng = markerPos?.[1] || lng || 106.7218;

  return (
    <div style={{ position: "relative" }}>
      <div style={{ position: "relative", marginBottom: 8 }}>
        <input
          type="text"
          value={query}
          onChange={(e) => handleQueryChange(e.target.value)}
          placeholder="🔍 Tìm kiếm địa chỉ"
          style={{
            width: "100%", padding: "10px 14px", borderRadius: 12,
            border: "1.5px solid #e2e8f0", fontSize: 14, outline: "none",
            boxSizing: "border-box", background: "#fff",
          }}
          onFocus={(e) => (e.target.style.borderColor = "#f97316")}
          onBlur={(e) => (e.target.style.borderColor = "#e2e8f0")}
        />
        {searching && (
          <span style={{ position: "absolute", right: 12, top: "50%", transform: "translateY(-50%)", fontSize: 12, color: "#94a3b8" }}>
            Đang tìm...
          </span>
        )}
        {results.length > 0 && (
          <div style={{
            position: "absolute", top: "100%", left: 0, right: 0, zIndex: 1000,
            background: "#fff", borderRadius: 12, border: "1px solid #e2e8f0",
            boxShadow: "0 8px 24px rgba(0,0,0,0.12)", marginTop: 4,
            maxHeight: 220, overflowY: "auto",
          }}>
            {results.map((item, idx) => (
              <button
                key={item.place_id || idx}
                type="button"
                onClick={() => handleSelectResult(item)}
                style={{
                  width: "100%", textAlign: "left", padding: "10px 14px",
                  border: "none", background: "transparent", cursor: "pointer",
                  fontSize: 13, color: "#1e293b",
                  borderBottom: idx < results.length - 1 ? "1px solid #f1f5f9" : "none",
                  display: "flex", alignItems: "flex-start", gap: 8,
                }}
                onMouseEnter={(e) => (e.currentTarget.style.background = "#fff7ed")}
                onMouseLeave={(e) => (e.currentTarget.style.background = "transparent")}
              >
                <span>📍</span>
                <span style={{ lineHeight: 1.4 }}>{item.display_name}</span>
              </button>
            ))}
          </div>
        )}
      </div>
      <div style={{ height: 280, borderRadius: 16, overflow: "hidden", border: "2px solid #e2e8f0" }}>
        <MapContainer center={[centerLat, centerLng]} zoom={14} style={{ height: "100%", width: "100%" }} scrollWheelZoom>
          <TileLayer
            attribution='&copy; <a href="https://www.google.com/maps">Google Maps</a>'
            url="https://mt1.google.com/vt/lyrs=m&x={x}&y={y}&z={z}"
          />
          <LocationMarker pos={markerPos} onSelect={handleMapClick} />
          {markerPos && <MapFlyTo lat={markerPos[0]} lng={markerPos[1]} />}
        </MapContainer>
      </div>
    </div>
  );
}

const dayOptions = [
  { value: 0, label: "Chủ nhật" },
  { value: 1, label: "Thứ 2" },
  { value: 2, label: "Thứ 3" },
  { value: 3, label: "Thứ 4" },
  { value: 4, label: "Thứ 5" },
  { value: 5, label: "Thứ 6" },
  { value: 6, label: "Thứ 7" },
];

function FieldError({ message }) {
  if (!message) return null;
  return <p className="mt-1 text-sm text-red-600">{message}</p>;
}

export default function EditChargingStation() {
  const { id } = useParams();
  const navigate = useNavigate();

  const [loadingStation, setLoadingStation] = useState(true);
  const [saving, setSaving] = useState(false);
  const [serverError, setServerError] = useState("");
  const [mapData, setMapData] = useState({ lat: 10.7295, lng: 106.7218, address: "" });
  const [operatingHours, setOperatingHours] = useState(
    dayOptions.map((d) => ({ dayOfWeek: d.value, isClosed: false, openTime: "06:00", closeTime: "23:00" }))
  );
  const [stationName, setStationName] = useState("");
  const [description, setDescription] = useState("");
  const [layoutImageFile, setLayoutImageFile] = useState(null);
  const [imageFiles, setImageFiles] = useState([]);

  // Load existing station data
  useEffect(() => {
    if (!id) return;
    setLoadingStation(true);
    stationApi.getById(id)
      .then((data) => {
        setStationName(data.name || "");
        setDescription(data.description || "");
        setMapData({
          lat: data.latitude || 10.7295,
          lng: data.longitude || 106.7218,
          address: data.address || "",
        });
        // Merge operating hours — fill with defaults for any missing days
        const oh = dayOptions.map((d) => {
          const found = (data.operatingHours || []).find((h) => h.dayOfWeek === d.value);
          if (found) {
            return {
              dayOfWeek: d.value,
              isClosed: found.isClosed || false,
              openTime: String(found.openTime || "06:00").substring(0, 5),
              closeTime: String(found.closeTime || "23:00").substring(0, 5),
            };
          }
          return { dayOfWeek: d.value, isClosed: false, openTime: "06:00", closeTime: "23:00" };
        });
        setOperatingHours(oh);
      })
      .catch(() => {
        setServerError("Không thể tải thông tin trạm sạc");
      })
      .finally(() => setLoadingStation(false));
  }, [id]);

  function handleHoursChange(index, field, value) {
    setOperatingHours((prev) => {
      const updated = [...prev];
      updated[index] = { ...updated[index], [field]: value };
      return updated;
    });
  }

  async function handleSubmit(e) {
    e.preventDefault();
    setServerError("");

    if (!stationName.trim()) {
      setServerError("Vui lòng nhập tên trạm sạc");
      return;
    }
    if (!mapData.address) {
      setServerError("Vui lòng chọn địa chỉ trên bản đồ");
      return;
    }

    // Build multipart/form-data — BE dùng [FromForm] UpdateStationFormDto
    const fd = new FormData();
    fd.append("name", stationName.trim());
    fd.append("address", mapData.address);
    if (description.trim()) fd.append("description", description.trim());
    if (mapData.lat) fd.append("latitude", String(mapData.lat));
    if (mapData.lng) fd.append("longitude", String(mapData.lng));
    
    if (layoutImageFile) fd.append("LayoutImage", layoutImageFile);
    imageFiles.forEach(file => {
      fd.append("Images", file);
    });

    // Operating hours — dùng index-based key giống CreateChargingStation
    operatingHours.forEach((h, i) => {
      fd.append(`operatingHours[${i}].dayOfWeek`, String(h.dayOfWeek));
      fd.append(`operatingHours[${i}].isClosed`, String(h.isClosed));
      if (!h.isClosed && h.openTime)  fd.append(`operatingHours[${i}].openTime`, h.openTime);
      if (!h.isClosed && h.closeTime) fd.append(`operatingHours[${i}].closeTime`, h.closeTime);
    });

    // Không có file ảnh mới → gửi danh sách URL cũ để giữ lại
    // (existingImageUrls = null → BE giữ nguyên ảnh cũ)

    setSaving(true);
    try {
      await stationApi.update(id, fd);
      showToast.success("Cập nhật trạm sạc thành công!");
      navigate("/stations");
    } catch (err) {
      setServerError(err?.message || "Lưu thất bại");
    } finally {
      setSaving(false);
    }
  }

  if (loadingStation) {
    return (
      <div className="min-h-screen bg-slate-100 px-6 pt-20 pb-8 flex items-center justify-center">
        <div className="text-center">
          <div className="w-10 h-10 border-4 border-orange-200 border-t-orange-500 rounded-full animate-spin mx-auto mb-4" />
          <p className="text-slate-500">Đang tải thông tin trạm sạc...</p>
        </div>
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-slate-100 px-4 sm:px-6 pt-20 pb-8 text-slate-900">
      <div className="mx-auto max-w-5xl">
        {/* Header */}
        <div className="mb-6 flex flex-wrap items-center justify-between gap-3 rounded-2xl bg-white px-5 py-5 shadow-sm ring-1 ring-slate-200">
          <div>
            <p className="text-sm font-medium uppercase tracking-[0.18em] text-orange-500">Owner Dashboard</p>
            <h1 className="mt-2 text-2xl sm:text-3xl font-bold">Chỉnh sửa trạm sạc</h1>
            <p className="mt-1 text-sm text-slate-600">Cập nhật tên, địa chỉ và giờ hoạt động.</p>
          </div>
          <Link
            to="/stations"
            className="rounded-xl border border-slate-300 px-4 py-2 text-sm font-medium text-slate-700 transition hover:border-slate-400 hover:bg-slate-50"
          >
            ← Quay lại
          </Link>
        </div>

        <form onSubmit={handleSubmit} className="space-y-6">
          {/* Basic Info */}
          <section className="rounded-2xl bg-white p-5 sm:p-6 shadow-sm ring-1 ring-slate-200">
            <h2 className="text-xl font-semibold mb-5">Thông tin cơ bản</h2>
            <div className="space-y-5">
              <div>
                <label className="mb-2 block text-sm font-medium text-slate-700">Tên trạm <span className="text-red-500">*</span></label>
                <input
                  type="text"
                  value={stationName}
                  onChange={(e) => setStationName(e.target.value)}
                  className="h-11 w-full rounded-xl border border-slate-300 px-4 outline-none transition focus:border-orange-400 focus:ring-2 focus:ring-orange-100"
                  placeholder="Ví dụ: Trạm Sạc Xe Máy Quận 7"
                />
              </div>
              <div>
                <label className="mb-2 block text-sm font-medium text-slate-700">Mô tả</label>
                <textarea
                  value={description}
                  onChange={(e) => setDescription(e.target.value)}
                  rows={3}
                  className="w-full rounded-xl border border-slate-300 px-4 py-3 outline-none transition focus:border-orange-400 focus:ring-2 focus:ring-orange-100"
                  placeholder="Mô tả vị trí, tiện ích..."
                />
              </div>
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-5">
                <div>
                  <label className="mb-2 block text-sm font-medium text-slate-700">Ảnh Layout (Sơ đồ trạm)</label>
                  <label className="flex flex-col items-center justify-center h-32 w-full rounded-xl border-2 border-dashed border-slate-300 bg-slate-50 cursor-pointer hover:bg-slate-100 transition">
                    <span className="text-2xl mb-1">🗺️</span>
                    <span className="text-xs text-slate-500 font-medium px-4 text-center line-clamp-2">
                      {layoutImageFile ? layoutImageFile.name : "Bấm để chọn 1 ảnh layout..."}
                    </span>
                    <input type="file" accept="image/*" className="hidden" onChange={(e) => setLayoutImageFile(e.target.files[0])} />
                  </label>
                </div>
                <div>
                  <label className="mb-2 block text-sm font-medium text-slate-700">Ảnh trạm sạc thực tế</label>
                  <label className="flex flex-col items-center justify-center h-32 w-full rounded-xl border-2 border-dashed border-slate-300 bg-slate-50 cursor-pointer hover:bg-slate-100 transition">
                    <span className="text-2xl mb-1">📸</span>
                    <span className="text-xs text-slate-500 font-medium">{imageFiles.length > 0 ? `Đã chọn ${imageFiles.length} ảnh` : "Bấm để chọn nhiều ảnh..."}</span>
                    <input type="file" accept="image/*" multiple className="hidden" onChange={(e) => setImageFiles(Array.from(e.target.files))} />
                  </label>
                </div>
              </div>
              <div>
                <label className="mb-2 block text-sm font-medium text-slate-700">
                  📍 Vị trí trên bản đồ <span className="text-red-500">*</span>
                </label>
                <MapPicker
                  lat={mapData.lat}
                  lng={mapData.lng}
                  address={mapData.address}
                  onSelect={(lat, lng, addr) => {
                    setMapData((prev) => ({
                      lat,
                      lng,
                      address: addr !== null ? (addr || prev.address) : prev.address,
                    }));
                  }}
                />
                {mapData.address && (
                  <div className="mt-2 flex items-start gap-2 text-sm text-slate-600">
                    <span className="text-slate-400 flex-shrink-0">📍</span>
                    <span>{mapData.address}</span>
                  </div>
                )}
              </div>
            </div>
          </section>

          {/* Operating Hours */}
          <section className="rounded-2xl bg-white p-5 sm:p-6 shadow-sm ring-1 ring-slate-200">
            <h2 className="text-xl font-semibold mb-1">Giờ hoạt động</h2>
            <p className="text-sm text-slate-500 mb-5">Cấu hình cho từng ngày trong tuần.</p>
            <div className="space-y-3">
              {dayOptions.map((day, index) => {
                const h = operatingHours[index];
                return (
                  <div
                    key={day.value}
                    className="grid gap-3 rounded-xl border border-slate-200 p-3 items-center"
                    style={{ gridTemplateColumns: "1fr auto auto auto" }}
                  >
                    <div>
                      <p className="font-medium text-slate-800 text-sm">{day.label}</p>
                      <label className="mt-1 inline-flex items-center gap-2 text-xs text-slate-500 cursor-pointer">
                        <input
                          type="checkbox"
                          checked={h.isClosed}
                          onChange={(e) => handleHoursChange(index, "isClosed", e.target.checked)}
                          className="rounded"
                        />
                        Đóng cửa
                      </label>
                    </div>
                    <div>
                      <label className="block text-[10px] text-slate-400 mb-1">Mở cửa</label>
                      <input
                        type="time"
                        value={h.openTime}
                        disabled={h.isClosed}
                        onChange={(e) => handleHoursChange(index, "openTime", e.target.value)}
                        className="h-9 w-full rounded-lg border border-slate-300 px-2 text-sm outline-none disabled:bg-slate-100 disabled:text-slate-400"
                      />
                    </div>
                    <div>
                      <label className="block text-[10px] text-slate-400 mb-1">Đóng cửa</label>
                      <input
                        type="time"
                        value={h.closeTime}
                        disabled={h.isClosed}
                        onChange={(e) => handleHoursChange(index, "closeTime", e.target.value)}
                        className="h-9 w-full rounded-lg border border-slate-300 px-2 text-sm outline-none disabled:bg-slate-100 disabled:text-slate-400"
                      />
                    </div>
                    <div className="flex items-center self-end pb-1">
                      {h.isClosed ? (
                        <span className="text-xs font-semibold text-red-400 bg-red-50 px-2 py-1 rounded-lg">Nghỉ</span>
                      ) : (
                        <span className="text-xs font-semibold text-green-600 bg-green-50 px-2 py-1 rounded-lg">Mở</span>
                      )}
                    </div>
                  </div>
                );
              })}
            </div>
          </section>

          {/* Submit */}
          <div className="rounded-2xl bg-white p-5 sm:p-6 shadow-sm ring-1 ring-slate-200">
            {serverError && (
              <div className="mb-4 rounded-xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700 flex items-start gap-2">
                <span className="flex-shrink-0">⚠️</span>
                <span>{serverError}</span>
              </div>
            )}
            <div className="flex flex-col sm:flex-row items-center justify-end gap-3">
              <Link
                to="/stations"
                className="w-full sm:w-auto text-center rounded-xl border border-slate-300 px-6 py-2.5 text-sm font-medium text-slate-700 transition hover:border-slate-400 hover:bg-slate-50"
              >
                Hủy
              </Link>
              <Button
                type="submit"
                disabled={saving}
                className="w-full sm:w-auto h-11 bg-orange-500 px-8 text-white hover:bg-orange-600 rounded-xl font-semibold"
              >
                {saving ? (
                  <span className="flex items-center gap-2">
                    <span className="w-4 h-4 border-2 border-white/40 border-t-white rounded-full animate-spin" />
                    Đang lưu...
                  </span>
                ) : (
                  "💾 Lưu thay đổi"
                )}
              </Button>
            </div>
          </div>
        </form>
      </div>
    </div>
  );
}
